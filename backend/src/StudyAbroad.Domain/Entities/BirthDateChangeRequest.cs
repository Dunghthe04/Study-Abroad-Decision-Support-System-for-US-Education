using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-107 / UC-150] Yêu cầu sửa ngày sinh do Học sinh / Phụ huynh gửi, Administrator duyệt hoặc từ chối.
/// Ngày sinh trong <c>users</c> chỉ đổi khi được duyệt. Mỗi tài khoản tối đa một yêu cầu Chờ duyệt (chỉ mục có điều kiện).
/// </summary>
public class BirthDateChangeRequest : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Ngày sinh lúc gửi yêu cầu (để Admin so sánh và ghi nhật ký).</summary>
    public DateOnly? CurrentBirthDate { get; set; }
    public DateOnly RequestedBirthDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Xem BirthDateChangeStatuses: pending | approved | rejected | cancelled</summary>
    public string Status { get; set; } = Constants.BirthDateChangeStatuses.Pending;

    /// <summary>Loại giấy tờ minh chứng (giữ lại sau khi xóa tệp vì nhật ký phải lưu loại giấy tờ).</summary>
    public string? EvidenceType { get; set; }
    /// <summary>Đường dẫn tệp trên máy chủ; về null khi tệp bị xóa (duyệt, từ chối hoặc hủy).</summary>
    public string? EvidencePath { get; set; }
    public string? EvidenceFileName { get; set; }
    public string? EvidenceContentType { get; set; }
    public long? EvidenceSize { get; set; }

    /// <summary>[UC-107 3b] Email người đại diện khi ngày mới làm học sinh dưới 16 mà chưa có đồng ý.</summary>
    public string? RepresentativeEmail { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
}
