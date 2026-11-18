using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-92 / UC-93] Yêu cầu xóa tài khoản và dữ liệu do trung tâm gửi; Administrator xử lý ở UC-93.
/// </summary>
public class CenterDeletionRequest : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>pending | completed | rejected (<see cref="Constants.CenterDeletionRequestStatuses"/>).</summary>
    public string Status { get; set; } = Constants.CenterDeletionRequestStatuses.Pending;

    public Guid? HandledByUserId { get; set; }
    public DateTime? HandledAt { get; set; }
}
