namespace StudyAbroad.Domain.Entities;

/// <summary>Chi tiết từng trường trong kết quả gợi ý DSS (UC-22, 23, 24).</summary>
public class RecommendationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecommendationId { get; set; }
    public Guid UniversityId { get; set; }
    public string? ProgramName { get; set; }
    public string? TierAi { get; set; }
    public string? TierRule { get; set; }
    public bool IsTierDivergent { get; set; } = false;
    public decimal? MatchStrength { get; set; }
    public string? RuleCodesTriggered { get; set; }
    public string FinancialFitStatus { get; set; } = "affordable";
    public string? GapReasons { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
