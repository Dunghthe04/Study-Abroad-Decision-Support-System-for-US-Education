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
    Task<DraftTranscriptInfoDto> SaveDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<string?> CommitDraftTranscriptAsync(Guid userId, string draftRelativePath, CancellationToken ct = default);
    Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default);
}
