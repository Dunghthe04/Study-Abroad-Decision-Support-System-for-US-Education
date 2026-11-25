using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Một đơn kèm các tên, email và mã người mua đã xóa (nếu có) đã nối sẵn, đọc không theo dõi. Người mua đã bị xóa hẳn thì BuyerName / BuyerEmail null.</summary>
public sealed record AdminOrderRow(
    PaymentTransaction Order, string? PlanCode, UserSubscription? Cycle, string? BuyerName, string? BuyerEmail, string? HandledByName,
    string? DeletedBuyerCode = null);

/// <summary>Đơn cho admin (UC-124..126). Khớp AdminOrderDto của frontend: các trường của OrderDto cộng mã GD payOS, số tiền nhận, người mua, ghi chú, người xử lý.</summary>
public record AdminOrderDto(
    long OrderCode, string PlanCode, string PlanName, string PlanKind, long AmountVnd, string Description, string Status,
    DateTime CreatedAt, DateTime ExpiresAt, DateTime? PaidAt, string CheckoutUrl, string QrCode, OrderCycleDto? Cycle,
    string? BuyerName, string? BuyerEmail, string? AdminNote, string? HandledBy,
    string? GatewayTransactionId = null, long? ReceivedAmountVnd = null, string? DeletedBuyerCode = null)
{
    public static AdminOrderDto From(AdminOrderRow r)
    {
        var o = r.Order;
        return new AdminOrderDto(
            o.OrderCode, r.PlanCode ?? string.Empty, o.PlanName, o.PlanKind, (long)o.AmountVnd, o.Description, o.Status,
            o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CheckoutUrl, o.QrCode,
            r.Cycle is null ? null : new OrderCycleDto(r.Cycle.StartsAt, r.Cycle.EndsAt),
            r.BuyerName, r.BuyerEmail, o.AdminNote, r.HandledByName,
            o.GatewayTransactionId, o.ReceivedAmountVnd is { } rec ? (long)rec : null, r.DeletedBuyerCode);
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

/// <summary>[UC-126] Kết quả kiểm tra trạng thái đơn với payOS (chỉ đọc, không tự kích hoạt).</summary>
public sealed record AdminOrderRecheckDto(
    long OrderCode, string OrderStatus, string GatewayStatus, long GatewayAmountPaid, bool CanActivate, DateTime CheckedAt);

/// <summary>[UC-124 2a, UC-75, UC-126 E3] Thông tin người mua của tài khoản đã xóa (lưu 10 năm kế toán), chỉ trả khi Admin bấm xem và có ghi nhật ký UC-91.</summary>
public sealed record AdminOrderBuyerInfoDto(string BuyerCode, string FullName, string Email, DateTime RetainUntilUtc);
