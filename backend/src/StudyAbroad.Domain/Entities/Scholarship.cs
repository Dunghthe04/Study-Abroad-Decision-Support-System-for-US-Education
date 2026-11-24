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
    /// <summary>official | aggregated | self_reported</summary>
    public string? SourceReliability { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public bool IsActive { get; set; } = true;

    // ---- [USAS-2067] Cột của bộ dữ liệu học bổng. Null = chưa rõ. ----

    /// <summary>merit | need | merit_and_need | athletic | program | external | other</summary>
    public string? Kind { get; set; }
    /// <summary>fixed | range | percent_tuition | full_tuition | full_ride | varies</summary>
    public string? AmountType { get; set; }
    public decimal? AmountMinUsd { get; set; }
    public decimal? AmountMaxUsd { get; set; }
    public int? DurationYears { get; set; }
    public bool? Renewable { get; set; }
    public string? RenewalCriteria { get; set; }
    public bool? IntlEligible { get; set; }
    public bool? VnEligible { get; set; }
    public bool? NeedBased { get; set; }
    public bool? MeritBased { get; set; }
    public int? MinSat { get; set; }
    public int? MinAct { get; set; }
    public decimal? MinToefl { get; set; }
    public string? MajorRestriction { get; set; }
    /// <summary>automatic | separate_application | nomination | unknown</summary>
    public string? ApplicationMethod { get; set; }
    public string? ApplyUrl { get; set; }
}
