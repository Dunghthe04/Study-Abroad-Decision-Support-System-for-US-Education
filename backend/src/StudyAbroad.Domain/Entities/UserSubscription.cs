using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Gói dịch vụ người dùng sở hữu (FE-19: UC-77, UC-78).</summary>
public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>active | expired | cancelled</summary>
    public string Status { get; set; } = "active";
    public bool AutoRenew { get; set; } = false;
}
