using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Yêu cầu xóa tài khoản và dữ liệu do trung tâm gửi từ trang hồ sơ (UC-92), Administrator xử lý (UC-93).
/// Tên và email trung tâm được chụp lại lúc gửi, vì dữ liệu gốc bị xóa khi yêu cầu hoàn tất.
/// </summary>
public class CenterDeletionRequest : BaseEntity
{
    public Guid? StudyCenterId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string CenterName { get; set; } = string.Empty;
    /// <summary>Email nhận kết quả xử lý (email liên lạc của trung tâm hoặc email đăng nhập lúc gửi yêu cầu).</summary>
    public string? ContactEmail { get; set; }
    public string? Reason { get; set; }
    /// <summary>pending | completed | rejected | cancelled</summary>
    public string Status { get; set; } = "pending";
    /// <summary>Hạn xử lý đã công bố, tính từ lúc gửi yêu cầu.</summary>
    public DateTime DueAt { get; set; }
    /// <summary>Administrator xác nhận đã kiểm tra danh tính người yêu cầu trước khi xóa.</summary>
    public bool IdentityVerified { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? AdminNote { get; set; }
}
