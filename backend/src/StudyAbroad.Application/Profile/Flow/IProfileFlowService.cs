using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service xử lý nghiệp vụ luồng hồ sơ một cửa (Unified Profile Flow):
/// - Tự động lưu nháp từng bước vào DraftFlowStateJson
/// - Khôi phục đúng bước đang làm dở khi quay lại
/// - Tải file bảng điểm nháp (xem trước / điền nháp)
/// - Xác nhận lưu chính thức (Commit) bóc tách toàn bộ 5 bước vào CSDL chính
/// - Xóa bản nháp làm lại từ đầu
/// </summary>
public interface IProfileFlowService
{
    Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default);
    Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default);
    Task<DraftTranscriptInfoDto> UploadDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default);
    Task ClearDraftAsync(Guid userId, CancellationToken ct = default);
}
