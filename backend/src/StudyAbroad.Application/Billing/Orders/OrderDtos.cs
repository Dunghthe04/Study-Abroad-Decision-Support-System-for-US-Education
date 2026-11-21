using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Orders;

/// <summary>Kỳ gói của đơn đã thanh toán.</summary>
public sealed record OrderCycleDto(DateTime? StartsAt, DateTime? EndsAt);

/// <summary>Đơn hàng cho khách (UC-78, UC-112). Khớp OrderDto của frontend; không có số token.</summary>
public sealed record OrderDto(
    long OrderCode, string PlanCode, string PlanName, string PlanKind, long AmountVnd, string Description, string Status,
    DateTime CreatedAt, DateTime ExpiresAt, DateTime? PaidAt, string CheckoutUrl, string QrCode, OrderCycleDto? Cycle)
{
    public static OrderDto From(PaymentTransaction o, string planCode, UserSubscription? cycle) => new(
        o.OrderCode, planCode, o.PlanName, o.PlanKind, (long)o.AmountVnd, o.Description, o.Status,
        o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CheckoutUrl, o.QrCode,
        cycle is null ? null : new OrderCycleDto(cycle.StartsAt, cycle.EndsAt));
}
