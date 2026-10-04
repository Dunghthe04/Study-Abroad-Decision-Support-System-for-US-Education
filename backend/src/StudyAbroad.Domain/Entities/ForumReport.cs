using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Báo cáo vi phạm một bài viết (backlog #28).</summary>
public class ForumReport : BaseEntity
{
    public Guid ForumPostId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>open | actioned | dismissed</summary>
    public string Status { get; set; } = "open";
    public Guid? HandledByUserId { get; set; }
    public DateTime? HandledAt { get; set; }
}
