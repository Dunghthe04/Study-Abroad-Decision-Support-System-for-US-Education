using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Tin nhắn trao đổi trong yêu cầu liên hệ trung tâm tư vấn (UC-44, UC-58, UC-74).</summary>
public class LeadMessage : BaseEntity
{
    public Guid LeadId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
    /// <summary>visible | removed</summary>
    public string Status { get; set; } = "visible";
    public Guid? RemovedByUserId { get; set; }
}
