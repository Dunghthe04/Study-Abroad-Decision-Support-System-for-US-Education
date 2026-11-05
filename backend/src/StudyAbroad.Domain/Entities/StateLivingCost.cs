namespace StudyAbroad.Domain.Entities;

/// <summary>Chỉ số chi phí sinh hoạt giữa các bang tại Mỹ (UC-28).</summary>
public class StateLivingCost
{
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public decimal CostIndex { get; set; } = 100.00m;
    public decimal? AvgLivingUsdYearly { get; set; }
    public string? Notes { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? RetrievedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
