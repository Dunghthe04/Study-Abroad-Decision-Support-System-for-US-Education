using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Luật tư vấn chuyên gia R01-R29 dùng trong mô hình DSS (UC-88, UC-89).</summary>
public class ExpertRule : BaseEntity
{
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string DegreeLevel { get; set; } = "all";
    public string? TargetDirection { get; set; }
    public string? ConditionSummary { get; set; }
    public string? ConditionJson { get; set; }
    public string? ActionRecommendation { get; set; }
    public int SupportingCentersCount { get; set; } = 1;
    public string? SourceReference { get; set; }
    public decimal CertaintyFactor { get; set; } = 0.90m;
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public string VerifiedBy { get; set; } = "USAS Advisory Board";
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
