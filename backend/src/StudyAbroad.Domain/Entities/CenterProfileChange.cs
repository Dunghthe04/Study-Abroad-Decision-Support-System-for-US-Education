using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Một thay đổi trung tâm tự sửa trên hồ sơ (UC-92) và được đánh dấu để Moderator kiểm tra (UC-114): website, fanpage.
/// Giá trị mới đã được lưu ngay lên hồ sơ; bản ghi này giữ giá trị cũ và mới để đối chiếu.
/// </summary>
public class CenterProfileChange : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    /// <summary>website | fanpage</summary>
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>pending | passed | failed | withdrawn (trung tâm tự đổi lại giá trị cũ trước khi được kiểm tra)</summary>
    public string Status { get; set; } = "pending";
    public Guid? ChangedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    /// <summary>Lý do Moderator ghi khi chọn Không đạt (bắt buộc), hoặc ghi chú khi Đạt.</summary>
    public string? ReviewReason { get; set; }
}
