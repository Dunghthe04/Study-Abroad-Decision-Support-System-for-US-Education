using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service xử lý nghiệp vụ luồng hồ sơ một cửa (Unified Profile Flow):
/// - Tự động lưu nháp từng bước vào DraftFlowStateJson
/// - Chat ghi các thông tin vừa đổi vào cùng bản nháp (PatchDraftAsync)
/// - Khôi phục đúng bước đang làm dở khi quay lại
/// - Tải file bảng điểm nháp (xem trước / điền nháp)
/// - Xác nhận lưu chính thức (Commit) bóc tách toàn bộ 5 bước vào CSDL chính
/// - Xóa bản nháp làm lại từ đầu
/// </summary>
public interface IProfileFlowService
{
    Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default);
    Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default);
    Task<ProfileFlowStateDto> PatchDraftAsync(Guid userId, JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default);
    Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default);
    Task ClearDraftAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đủ 5 bước của hồ sơ ĐÃ GỬI, dựng từ DB, bỏ qua nháp đang làm dở; chưa gửi thì null.
    /// Thẻ hồ sơ ở Tài khoản và "Con của tôi" đọc để hiện đủ các trường. Mặc định null để các bản giả trong test không phải sửa.</summary>
    Task<ProfileFlowDraftDto?> GetCommittedDetailsAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<ProfileFlowDraftDto?>(null);

    /// <summary>Người này có được nhập / lưu hồ sơ của chính mình không (tài khoản phụ huynh thì không, 1.5d).
    /// Chat đọc để thay các lượt về hồ sơ (ParentChatGate). Mặc định true để các bản giả trong test không phải sửa.</summary>
    Task<bool> CanEditOwnProfileAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(true);
}
