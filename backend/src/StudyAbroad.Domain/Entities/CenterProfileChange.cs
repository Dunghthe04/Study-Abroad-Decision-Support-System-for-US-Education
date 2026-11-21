using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-92 / UC-114] Một thay đổi do trung tâm tự sửa (website, fanpage) được lưu ngay nhưng đánh dấu để Moderator kiểm tra.
/// </summary>
public class CenterProfileChange : BaseEntity
{
    public Guid StudyCenterId { get; set; }

    /// <summary>Tên trường thay đổi, xem <see cref="Constants.CenterProfileChangeFields"/>.</summary>
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>pending | passed | failed | withdrawn (<see cref="Constants.CenterProfileChangeStatuses"/>).</summary>
    public string Status { get; set; } = Constants.CenterProfileChangeStatuses.Pending;

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Lý do bắt buộc khi Không đạt (UC-114).</summary>
    public string? ReviewReason { get; set; }
}
