using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Yêu cầu liên hệ người dùng gửi cho trung tâm (backlog #24).</summary>
public class Lead : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid StudyCenterId { get; set; }
    public Guid? ConsentId { get; set; }
    public string? StudyLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? Message { get; set; }
    public bool SharedProfile { get; set; }
    /// <summary>new | contacted | closed | spam</summary>
    public string Status { get; set; } = "new";
}
