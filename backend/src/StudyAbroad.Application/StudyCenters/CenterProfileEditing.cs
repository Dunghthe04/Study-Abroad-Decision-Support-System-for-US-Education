using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>
/// [UC-92] Kết quả phân loại một lần trung tâm tự sửa hồ sơ, để giao diện báo đúng từng trường:
/// <list type="bullet">
/// <item><see cref="SavedFields"/>: lưu và hiển thị ngay (mô tả, dịch vụ, logo, địa chỉ, chi nhánh, mức phí...).</item>
/// <item><see cref="ReviewFields"/>: lưu ngay nhưng chờ Moderator kiểm tra (website, fanpage, UC-114).</item>
/// <item><see cref="LegalFields"/>: thông tin pháp lý; với trung tâm đã xác minh thì hồ sơ về Chờ duyệt (UC-59).</item>
/// </list>
/// </summary>
public record CenterProfileUpdateOutcome(
    IReadOnlyList<string> SavedFields,
    IReadOnlyList<string> ReviewFields,
    IReadOnlyList<string> LegalFields,
    bool VerificationReset,
    string Message);

/// <summary>[UC-92] Phản hồi của PUT /api/v1/center/profile: hồ sơ sau khi lưu kèm phân loại thay đổi.</summary>
public record CenterProfileUpdateResponse(StudyCenterDto Center, CenterProfileUpdateOutcome Outcome);

/// <summary>[UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu. Lý do bắt buộc.</summary>
public record CreateCenterDeletionRequest(string? Reason);

/// <summary>[UC-92 / UC-93] Yêu cầu xóa tài khoản trung tâm.</summary>
public record CenterDeletionRequestDto(
    Guid Id,
    Guid StudyCenterId,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? HandledAt,
    /// <summary>
    /// Hạn xử lý của USAS (20 ngày kể từ khi nhận, có thể gia hạn một lần). Null khi trung tâm đang bị giữ pháp lý:
    /// hệ thống không thể hứa mốc xử lý, và không để lộ việc đang giữ.
    /// </summary>
    DateTime? DueAt = null,
    /// <summary>Thời điểm gia hạn (null nếu chưa gia hạn).</summary>
    DateTime? ExtendedAt = null);

public record CenterDeletionRequestResult(CenterDeletionRequestDto? Request, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && Request is not null;
    public static CenterDeletionRequestResult Ok(CenterDeletionRequestDto request) => new(request);
    public static CenterDeletionRequestResult Fail(string error) => new(null, error);
}

/// <summary>
/// [UC-92 / UC-114] Lưu các thay đổi website / fanpage cần Moderator kiểm tra.
/// UC-114 dùng <see cref="SearchAsync"/>, <see cref="GetByIdAsync"/> và <see cref="UpdateAsync"/> để duyệt Đạt / Không đạt.
/// </summary>
public interface ICenterProfileChangeRepository
{
    /// <summary>Thay đổi đang chờ kiểm tra của một trường (mỗi trường tối đa một bản ghi pending).</summary>
    Task<CenterProfileChange?> GetPendingAsync(Guid studyCenterId, string field, CancellationToken ct = default);
    Task<CenterProfileChange?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CenterProfileChange change, CancellationToken ct = default);
    Task UpdateAsync(CenterProfileChange change, CancellationToken ct = default);

    /// <summary>Danh sách theo trạng thái (null = tất cả), cũ nhất trước với pending.</summary>
    Task<PagedResult<CenterProfileChange>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>[UC-114] Tên, chủ sở hữu, trạng thái xác minh và ẩn/hiện của các trung tâm để Moderator đối chiếu.</summary>
    Task<IReadOnlyList<CenterChangeCenterInfo>> GetCenterInfosAsync(IReadOnlyCollection<Guid> centerIds, CancellationToken ct = default);

    /// <summary>[UC-114] Ghi thông báo trong hệ thống cho trung tâm khi thay đổi Không đạt.</summary>
    Task AddNotificationAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>
/// [UC-92 / UC-93] Yêu cầu xóa tài khoản trung tâm. UC-93 dùng <see cref="SearchAsync"/>, <see cref="GetByIdAsync"/>
/// và <see cref="UpdateAsync"/> để Administrator xử lý.
/// </summary>
public interface ICenterDeletionRequestRepository
{
    /// <summary>Yêu cầu đang mở (pending) của trung tâm; mỗi trung tâm tối đa một yêu cầu mở.</summary>
    Task<CenterDeletionRequest?> GetOpenByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default);

    /// <summary>Yêu cầu mới nhất của trung tâm (bất kể trạng thái) để hiện trên trang hồ sơ.</summary>
    Task<CenterDeletionRequest?> GetLatestByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default);

    Task<CenterDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default);
    Task UpdateAsync(CenterDeletionRequest request, CancellationToken ct = default);
    Task<PagedResult<CenterDeletionRequest>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
}
