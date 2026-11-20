using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Một đơn kèm các tên và email đã nối sẵn, đọc không theo dõi. Người mua đã bị xóa hẳn thì BuyerName / BuyerEmail null.</summary>
public sealed record AdminOrderRow(
    PaymentTransaction Order, string? PlanCode, UserSubscription? Cycle, string? BuyerName, string? BuyerEmail, string? HandledByName);

/// <summary>Đơn cho admin (UC-132..134). Khớp AdminOrderDto của frontend: các trường của OrderDto cộng người mua, ghi chú, người xử lý.</summary>
public record AdminOrderDto(
    long OrderCode, string PlanCode, string PlanName, string PlanKind, long AmountVnd, string Description, string Status,
    DateTime CreatedAt, DateTime ExpiresAt, DateTime? PaidAt, string CheckoutUrl, string QrCode, OrderCycleDto? Cycle,
    string? BuyerName, string? BuyerEmail, string? AdminNote, string? HandledBy)
{
    public static AdminOrderDto From(AdminOrderRow r)
    {
        var o = r.Order;
        return new AdminOrderDto(
            o.OrderCode, r.PlanCode ?? string.Empty, o.PlanName, o.PlanKind, (long)o.AmountVnd, o.Description, o.Status,
            o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CheckoutUrl, o.QrCode,
            r.Cycle is null ? null : new OrderCycleDto(r.Cycle.StartsAt, r.Cycle.EndsAt, r.Cycle.Status == CycleStatuses.Queued),
            r.BuyerName, r.BuyerEmail, o.AdminNote, r.HandledByName);
    }
}

/// <summary>AdminOrderDto kèm dữ liệu webhook payOS đã lọc (PayloadJson), cho màn chi tiết.</summary>
public sealed record AdminOrderDetailDto : AdminOrderDto
{
    public AdminOrderDetailDto(AdminOrderDto order, string? payloadJson) : base(order) => PayloadJson = payloadJson;

    public string? PayloadJson { get; init; }
}

/// <summary>Body của kích hoạt và từ chối: ghi chú bắt buộc.</summary>
public sealed record AdminOrderNoteRequest(string? Note);
