using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Học bổng, có thể gắn với một trường (backlog #19, #26).</summary>
public class Scholarship : BaseEntity
{
    public Guid? UniversityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string StudyLevel { get; set; } = string.Empty;
    public decimal? AmountUsd { get; set; }
    /// <summary>full | partial | tuition | stipend</summary>
    public string? CoverageType { get; set; }
    public decimal? MinGpa4 { get; set; }
    public decimal? MinIelts { get; set; }
    public DateOnly? Deadline { get; set; }
    public string? EligibilityNotes { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? RetrievedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
