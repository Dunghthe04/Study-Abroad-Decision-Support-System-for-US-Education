using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Bước trong lộ trình du học theo bậc học (backlog #21). Chưa lưu tiến độ từng người.</summary>
public class RoadmapStep : BaseEntity
{
    public string StudyLevel { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int? MonthsBeforeStart { get; set; }
    /// <summary>tài liệu tham khảo: title, url</summary>
    public string ResourcesJson { get; set; } = "[]";
}
