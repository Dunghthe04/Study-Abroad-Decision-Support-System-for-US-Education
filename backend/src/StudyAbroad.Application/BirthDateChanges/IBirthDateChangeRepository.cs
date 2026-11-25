using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>[UC-150] Một dòng trong danh sách yêu cầu sửa ngày sinh phía Admin (kèm tên, email, vai trò người gửi).</summary>
public record BirthDateRequestListRow(
    BirthDateChangeRequest Request,
    string FullName,
    string Email,
    string Role);

/// <summary>[UC-107 / UC-150] Lưu và đọc yêu cầu sửa ngày sinh.</summary>
public interface IBirthDateChangeRepository
{
    Task<BirthDateChangeRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Yêu cầu đang Chờ duyệt của tài khoản (tối đa một).</summary>
    Task<BirthDateChangeRequest?> GetPendingByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Yêu cầu mới nhất của tài khoản, mọi trạng thái (trang Tài khoản hiện trạng thái, lý do từ chối).</summary>
    Task<BirthDateChangeRequest?> GetLatestByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Thêm yêu cầu. Trả false khi tài khoản đã có yêu cầu Chờ duyệt (đụng chỉ mục duy nhất, UC-107 E5).</summary>
    Task<bool> TryAddAsync(BirthDateChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Đóng yêu cầu chỉ khi còn Chờ duyệt (duyệt, từ chối, hủy). Bỏ đường dẫn tệp minh chứng, giữ loại giấy tờ.
    /// Trả false khi yêu cầu không còn Chờ duyệt (đã hủy hoặc người khác đã xử lý, UC-150 E1).
    /// </summary>
    Task<bool> TryCloseAsync(Guid id, string newStatus, Guid? reviewedByUserId, string? rejectReason, DateTime at, CancellationToken ct = default);

    /// <summary>[UC-150] Danh sách theo trạng thái; Chờ duyệt cũ nhất trước, trạng thái khác mới nhất trước.</summary>
    Task<PagedResult<BirthDateRequestListRow>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
}
