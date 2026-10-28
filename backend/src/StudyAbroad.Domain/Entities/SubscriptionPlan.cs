using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Gói dịch vụ hệ thống Free / Premium (FE-19: UC-77, UC-79).</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; } = 0;
    public int DurationDays { get; set; } = 30;
    public int DailyAiChatQuota { get; set; } = 5;
    public int MaxLeadsQuota { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}
