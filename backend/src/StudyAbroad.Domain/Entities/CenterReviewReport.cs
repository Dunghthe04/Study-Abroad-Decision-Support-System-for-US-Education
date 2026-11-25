using StudyAbroad.Domain.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Domain.Entities;

/// <summary>[UC-151 / UC-53] Báo cáo đánh giá trung tâm vi phạm để Moderator xem xét.</summary>
public class CenterReviewReport : BaseEntity
{
    public Guid CenterReviewId { get; set; }
    public Guid StudyCenterId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterRole { get; set; } = string.Empty;
    /// <summary>True khi chính trung tâm báo cáo đánh giá về mình (để Moderator lưu ý xung đột lợi ích).</summary>
    public bool IsCenterSelfReport { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>open | resolved_removed | dismissed</summary>
    public string Status { get; set; } = CenterReviewReportStatuses.Open;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
