using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Interface lưu trữ tệp bảng điểm nháp và chuyển sang chính thức khi hoàn tất.
/// </summary>
public interface IProfileFileStorageService
{
    /// <summary>Lưu tệp nháp. Tệp sai định dạng (đuôi hoặc nội dung) hay quá dung lượng thì ném ArgumentException.</summary>
    Task<DraftTranscriptInfoDto> SaveDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<string?> CommitDraftTranscriptAsync(Guid userId, string draftRelativePath, CancellationToken ct = default);
    /// <summary>Xoá 1 tệp nháp của user (tệp cũ bị thay bằng lần tải mới).</summary>
    Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default);
    /// <summary>Xoá 1 tệp bảng điểm chính thức của user (bản cũ khi xác nhận hồ sơ mới).</summary>
    Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default);
    Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Giới hạn tệp bảng điểm dùng chung cho controller (chặn sớm) và storage (chặn lần cuối).</summary>
public static class ProfileFileStorageLimits
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
}
