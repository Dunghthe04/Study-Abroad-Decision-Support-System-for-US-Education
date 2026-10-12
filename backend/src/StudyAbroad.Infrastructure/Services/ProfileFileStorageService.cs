using Microsoft.Extensions.Hosting;
using StudyAbroad.Application.Profile.Flow;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Services;

/// <summary>
/// [USAS-640] Quản lý lưu trữ tệp bảng điểm nháp (trong thư mục uploads/drafts) 
/// và chuyển sang thư mục uploads/transcripts khi xác nhận lưu chính thức.
/// </summary>
public class ProfileFileStorageService : IProfileFileStorageService
{
    private readonly string _baseUploadDir;
    private static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

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
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            throw new ArgumentException("Định dạng tệp không hợp lệ. Chỉ chấp nhận tệp PDF, PNG, JPG hoặc JPEG.");
        }

        var userDraftDir = Path.Combine(_baseUploadDir, "drafts", userId.ToString());
        if (!Directory.Exists(userDraftDir))
        {
            Directory.CreateDirectory(userDraftDir);
        }

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(userDraftDir, safeFileName);

        using (var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        var relativePath = $"/uploads/drafts/{userId}/{safeFileName}";

        return new DraftTranscriptInfoDto(
            OriginalFileName: Path.GetFileName(fileName),
            StoredFileName: safeFileName,
            RelativePath: relativePath,
            FileSizeBytes: length,
            ContentType: contentType,
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
        if (!Directory.Exists(officialDir))
        {
            Directory.CreateDirectory(officialDir);
        }

        var officialFullPath = Path.Combine(officialDir, storedFileName);
        File.Move(draftFullPath, officialFullPath, overwrite: true);

        return Task.FromResult<string?>($"/uploads/transcripts/{userId}/{storedFileName}");
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
}
