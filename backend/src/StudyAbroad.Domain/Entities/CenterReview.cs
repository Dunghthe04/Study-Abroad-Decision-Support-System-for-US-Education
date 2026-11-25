using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Đánh giá trung tâm tư vấn từ 1 đến 5 sao (UC-41, UC-45, UC-57).</summary>
public class CenterReview : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    /// <summary>[UC-41] Null khi người đánh giá đã xóa tài khoản (đánh giá giữ lại, hiển thị "Người dùng đã xóa tài khoản").</summary>
    public Guid? UserId { get; set; }
    public Guid? LeadId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVerified { get; set; } = false;
    /// <summary>visible | hidden | removed</summary>
    public string Status { get; set; } = "visible";

    // --- [UC-152] Phản hồi công khai của trung tâm (tối đa 1 phản hồi / đánh giá; sửa được trong 24 giờ đầu) ---
    public string? CenterReplyContent { get; set; }
    public Guid? CenterReplyAuthorUserId { get; set; }
    public DateTime? CenterReplyCreatedAt { get; set; }
    public DateTime? CenterReplyEditedAt { get; set; }
    /// <summary>visible | removed (null khi chưa có phản hồi)</summary>
    public string? CenterReplyStatus { get; set; }
    public Guid? CenterReplyRemovedByUserId { get; set; }
    public string? CenterReplyRemoveReason { get; set; }
}
