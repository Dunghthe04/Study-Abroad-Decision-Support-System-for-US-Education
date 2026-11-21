using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Một kỳ gói đã thanh toán = một pool token (UC-77, UC-78). Làn = mọi kỳ cùng chủ và cùng loại: tối đa 1 active và 1 queued.
/// Gói, giá, hạn mức là bản chụp lúc mua nên admin sửa gói không ảnh hưởng kỳ đang chạy.
/// </summary>
public class UserSubscription : BaseEntity
{
    /// <summary>Người mua (chủ gói Gia đình).</summary>
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    /// <summary>personal | family (PlanKinds)</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Nhóm gia đình của kỳ loại family.</summary>
    public Guid? FamilyGroupId { get; set; }
    /// <summary>queued | active | ended (CycleStatuses)</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Null khi đang xếp hàng.</summary>
    public DateTime? StartsAt { get; set; }
    /// <summary>Null khi đang xếp hàng. Cột ends_at (trước đây expires_at).</summary>
    public DateTime? EndsAt { get; set; }
    public long TokenQuota { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    /// <summary>Bản chụp chu kỳ cấp lại token (ngày) từ đơn/gói lúc mua; null hoặc bằng DurationDays = cả kỳ.</summary>
    public int? QuotaPeriodDays { get; set; }
    /// <summary>CycleEndReasons</summary>
    public string? EndedReason { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? Warned80At { get; set; }
    public DateTime? ExhaustedNotifiedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    /// <summary>Đơn hàng đã kích hoạt kỳ này (không có khóa ngoại để tránh vòng khóa với payment_transactions).</summary>
    public Guid? ActivatedFromOrderId { get; set; }
}
