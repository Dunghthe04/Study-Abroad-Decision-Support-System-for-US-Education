using Microsoft.Extensions.Hosting;
using StudyAbroad.Application.Profile.Flow;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Services;

/// <summary>
/// [USAS-640] Kiểm tra tệp bảng điểm người dùng tải lên. Tệp KHÔNG được lưu xuống ổ đĩa:
/// bảng điểm chỉ dùng để OCR điền điểm một lần, sau đó không có chức năng nào xem lại tệp,
/// nên giữ tệp chỉ thêm dữ liệu cá nhân thừa và cần quyền ghi/volume trên server.
/// Các hàm xoá vẫn giữ để dọn tệp cũ của bản trước (uploads/drafts, uploads/transcripts).
/// </summary>
public class ProfileFileStorageService : IProfileFileStorageService
{
    public const long MaxFileSize = ProfileFileStorageLimits.MaxFileSizeBytes;

    private readonly string _baseUploadDir;

    /// <summary>Loại tệp được nhận: đuôi tệp, chữ ký đầu tệp (magic bytes) và content type ghi lại</summary>
    private static readonly (string[] Extensions, byte[] Signature, string ContentType)[] AllowedTypes =
    [
        ([".pdf"], "%PDF"u8.ToArray(), "application/pdf"),
        ([".png"], [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png"),
        ([".jpg", ".jpeg"], [0xFF, 0xD8, 0xFF], "image/jpeg"),
    ];

    public ProfileFileStorageService(IHostEnvironment? env = null)
    {
        var root = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
        _baseUploadDir = Path.Combine(root, "uploads");
    }

    public DraftTranscriptInfoDto ValidateTranscriptFile(byte[] fileBytes, string fileName)
    {
        if (fileBytes.Length <= 0 || fileBytes.Length > MaxFileSize)
        {
            throw new ArgumentException("Kích thước tệp phải lớn hơn 0 và không vượt quá 10MB.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowed = AllowedTypes.FirstOrDefault(t => t.Extensions.Contains(ext));
        if (allowed.Extensions is null)
        {
            throw new ArgumentException("Định dạng tệp không hợp lệ. Chỉ chấp nhận tệp PDF, PNG, JPG hoặc JPEG.");
        }

        // Không tin đuôi tệp và content type của trình duyệt: đầu tệp phải đúng chữ ký của loại đó
        if (!fileBytes.AsSpan().StartsWith(allowed.Signature))
        {
            throw new ArgumentException("Nội dung tệp không đúng định dạng PDF, PNG hoặc JPG. Bạn kiểm tra lại tệp giúp nhé.");
        }

        return new DraftTranscriptInfoDto(
            OriginalFileName: Path.GetFileName(fileName),
            StoredFileName: null,
            RelativePath: null,
            FileSizeBytes: fileBytes.Length,
            ContentType: allowed.ContentType,
            UploadedAt: DateTime.UtcNow
        );
    }

    public Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default)
    {
        DeleteOwnFile(userId, "drafts", draftRelativePath);
        return Task.CompletedTask;
    }

    public Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default)
    {
        DeleteOwnFile(userId, "transcripts", transcriptRelativePath);
        return Task.CompletedTask;
    }

    public Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default)
    {
        var userDraftDir = Path.Combine(_baseUploadDir, "drafts", userId.ToString());
        if (Directory.Exists(userDraftDir))
        {
            try
            {
                Directory.Delete(userDraftDir, recursive: true);
            }
            catch
            {
                // Bỏ qua lỗi dọn dẹp nếu có tiến trình đang đọc
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAllUserFilesAsync(Guid userId, CancellationToken ct = default)
    {
        foreach (var folder in new[] { "drafts", "transcripts" })
        {
            var userDir = Path.Combine(_baseUploadDir, folder, userId.ToString());
            if (Directory.Exists(userDir))
                Directory.Delete(userDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    /// <summary>Chỉ xoá đúng 1 tệp trong thư mục của chính user đó: tên tệp lấy qua GetFileName nên không đi ra ngoài được</summary>
    private void DeleteOwnFile(Guid userId, string folder, string? relativePath)
    {
        var storedFileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(storedFileName))
            return;

        var fullPath = Path.Combine(_baseUploadDir, folder, userId.ToString(), storedFileName);
        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException)
        {
            // Tệp đang bị đọc thì để lần dọn sau
        }
    }
}
