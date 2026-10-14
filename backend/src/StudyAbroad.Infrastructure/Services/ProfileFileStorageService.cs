using Microsoft.Extensions.Hosting;
using StudyAbroad.Application.Profile.Flow;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Services;

/// <summary>
/// [USAS-640] Quản lý lưu trữ tệp bảng điểm nháp (trong thư mục uploads/drafts)
/// và chuyển sang thư mục uploads/transcripts khi xác nhận lưu chính thức.
/// Thư mục uploads không được phục vụ công khai: tệp chỉ đọc được qua code phía server.
/// </summary>
public class ProfileFileStorageService : IProfileFileStorageService
{
    public const long MaxFileSize = ProfileFileStorageLimits.MaxFileSizeBytes;

    private readonly string _baseUploadDir;

    /// <summary>Loại tệp được nhận: đuôi tệp, chữ ký đầu tệp (magic bytes) và content type lưu lại</summary>
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

    public async Task<DraftTranscriptInfoDto> SaveDraftTranscriptAsync(
        Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        if (length <= 0 || length > MaxFileSize)
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
        var header = new byte[allowed.Signature.Length];
        var headerLength = await fileStream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (headerLength < header.Length || !header.AsSpan().SequenceEqual(allowed.Signature))
        {
            throw new ArgumentException("Nội dung tệp không đúng định dạng PDF, PNG hoặc JPG. Bạn kiểm tra lại tệp giúp nhé.");
        }

        var userDraftDir = Path.Combine(_baseUploadDir, "drafts", userId.ToString());
        Directory.CreateDirectory(userDraftDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(userDraftDir, safeFileName);

        await using (var outputStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await outputStream.WriteAsync(header, ct);
            await fileStream.CopyToAsync(outputStream, ct);
        }

        var relativePath = $"/uploads/drafts/{userId}/{safeFileName}";

        return new DraftTranscriptInfoDto(
            OriginalFileName: Path.GetFileName(fileName),
            StoredFileName: safeFileName,
            RelativePath: relativePath,
            FileSizeBytes: length,
            ContentType: allowed.ContentType,
            UploadedAt: DateTime.UtcNow
        );
    }

    public Task<string?> CommitDraftTranscriptAsync(Guid userId, string draftRelativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draftRelativePath))
            return Task.FromResult<string?>(null);

        var storedFileName = Path.GetFileName(draftRelativePath);
        var draftFullPath = Path.Combine(_baseUploadDir, "drafts", userId.ToString(), storedFileName);

        if (!File.Exists(draftFullPath))
            return Task.FromResult<string?>(null);

        var officialDir = Path.Combine(_baseUploadDir, "transcripts", userId.ToString());
        Directory.CreateDirectory(officialDir);

        var officialFullPath = Path.Combine(officialDir, storedFileName);
        File.Move(draftFullPath, officialFullPath, overwrite: true);

        return Task.FromResult<string?>($"/uploads/transcripts/{userId}/{storedFileName}");
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
