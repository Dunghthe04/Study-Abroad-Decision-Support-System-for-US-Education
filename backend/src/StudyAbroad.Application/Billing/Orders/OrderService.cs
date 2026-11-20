using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Orders;

/// <summary>
/// Đơn hàng gói dịch vụ (UC-78, UC-112): tạo đơn và link payOS, xem, hủy. Phần áp dụng thanh toán nằm ở OrderService.Payment.cs.
/// Gói, giá, hạn mức được chụp vào đơn lúc đặt nên admin sửa gói sau đó không ảnh hưởng đơn đang chờ (P7, P8).
/// </summary>
public sealed partial class OrderService(
    IOrderRepository orders,
    IBillingRepository billing,
    IEntitlementService entitlements,
    IUserRepository users,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IEmailSender email,
    INotificationService notifications,
    BillingOptions billingOptions,
    PayOsOptions payOsOptions,
    ILogger<OrderService>? logger = null) : IOrderService
{
    private const string GatewayName = "payos";
    private const int MaxInsertTries = 3;
    public const int MaxPageSize = 50;

    public async Task<OrderDto> CreateAsync(Guid userId, string role, string planCode, DateTime nowUtc, CancellationToken ct = default)
    {
        // 1. Chỉ học sinh và phụ huynh mua gói (C10)
        if (role is not (UserRoles.Student or UserRoles.Parent))
            throw Rule("role_not_allowed", 403, "Tài khoản của bạn không thể mua gói.");

        // 2. Dưới 16 tuổi chưa mua được (D9). Chưa có ngày sinh thì không giới hạn; tuổi chỉ kiểm lúc tạo đơn (P20)
        var user = await users.GetUserById(userId, ct) ?? throw Rule("role_not_allowed", 403, "Tài khoản của bạn không thể mua gói.");
        if (user.DateOfBirth is { } dob && AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(nowUtc)) < AgeGroups.ChildMaxAgeExclusive)
            throw Rule("age_restricted", 403, "Tài khoản dưới 16 tuổi chưa thể mua gói.");

        // 3. Gói đang bán (không phải gói Free)
        var plan = (await billing.GetPlansAsync(ct)).FirstOrDefault(p => p.Code == planCode);
        if (plan is null || !plan.IsActive || plan.Kind == PlanKinds.Free || plan.DurationDays is not { } durationDays)
            throw Rule("plan_not_for_sale", 400, "Gói này hiện không bán.");

        // 4. Mỗi người một đơn chờ thanh toán (P5)
        if (await billing.FindPendingOrderCodeAsync(userId, ct) is { } pendingCode)
            throw PendingExists(pendingCode);

        // 5. Đã có kỳ gia hạn xếp hàng thì chưa gia hạn thêm (R6)
        var lane = await entitlements.AdvanceLaneAsync(userId, plan.Kind, nowUtc, ct);
        if (lane.Queued is not null)
            throw Rule("already_renewed", 409, "Bạn đã gia hạn rồi, kỳ tiếp theo sẽ tự bắt đầu.");

        // 6. Đang là thành viên của gói Gia đình khác còn hiệu lực thì không mua gói Gia đình riêng (F29, C9)
        if (plan.Kind == PlanKinds.Family
            && await billing.FindActiveMemberGroupAsync(userId, ct) is { } other
            && (await entitlements.AdvanceLaneAsync(other.OwnerUserId, PlanKinds.Family, nowUtc, ct)).Active is not null)
            throw Rule("already_in_family", 409, "Bạn đang là thành viên của một gói Gia đình khác. Hãy rời nhóm đó trước khi mua gói Gia đình.");

        // 7. Ghi đơn
        var order = new PaymentTransaction
        {
            UserId = userId,
            SubscriptionPlanId = plan.Id,
            PlanKind = plan.Kind,
            PlanName = plan.Name,
            PlanDurationDays = durationDays,
            PlanTokenQuota = plan.TokenQuota,
            AmountVnd = plan.PriceVnd,
            Gateway = GatewayName,
            Status = OrderStatuses.Pending,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(billingOptions.OrderExpiryMinutes),
        };
        await InsertAsync(order, userId, nowUtc, ct);

        // 8. Link thanh toán payOS. Lỗi: hủy đơn để người dùng thử lại ngay (P9)
        var returnUrl = $"{payOsOptions.ReturnUrlBase.TrimEnd('/')}/orders/{order.OrderCode}";
        GatewayLink link;
        try
        {
            link = await gateway.CreateLinkAsync(order.OrderCode, (long)order.AmountVnd, order.Description, order.ExpiresAt, returnUrl, returnUrl, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không tạo được link thanh toán cho đơn {OrderCode}", order.OrderCode);
            order.Status = OrderStatuses.Cancelled;
            await orders.SaveAsync(CancellationToken.None);
            if (ex is OperationCanceledException)
                throw;
            throw Rule("gateway_unavailable", 502, "Không tạo được liên kết thanh toán, vui lòng thử lại sau.");
        }
        order.GatewayTransactionId = link.PaymentLinkId;
        order.CheckoutUrl = link.CheckoutUrl;
        order.QrCode = link.QrCode;
        await orders.SaveAsync(ct);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> GetMineAsync(Guid userId, long orderCode, CancellationToken ct = default)
    {
        var order = await orders.FindByCodeAsync(orderCode, ct);
        if (order is null || order.UserId != userId)
            throw NotFound();
        return await ToDtoAsync(order, ct);
    }

    public async Task<PagedResult<OrderDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await orders.ListByUserAsync(userId, page, pageSize, ct);
        var plans = await billing.GetPlansAsync(ct);
        var cycles = await orders.GetCyclesAsync(items.Where(o => o.UserSubscriptionId is not null).Select(o => o.UserSubscriptionId!.Value).ToList(), ct);
        var dtos = items.Select(o => ToDto(o, plans, o.UserSubscriptionId is { } id && cycles.TryGetValue(id, out var c) ? c : null)).ToList();
        return new PagedResult<OrderDto>(dtos, page, pageSize, total);
    }

    public async Task<OrderDto> CancelAsync(Guid userId, long orderCode, DateTime nowUtc, CancellationToken ct = default)
    {
        PaymentTransaction? order = null;
        // Khóa dòng đơn: hủy và webhook thanh toán không ghi đè lẫn nhau (đơn không có xmin)
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            order = await orders.LockByCodeAsync(orderCode, txCt);
            if (order is null || order.UserId != userId)
                throw NotFound();
            if (order.Status != OrderStatuses.Pending)
                throw Rule("not_pending", 409, "Đơn này không còn chờ thanh toán nên không hủy được.");
            order.Status = OrderStatuses.Cancelled;
            await orders.SaveAsync(txCt);
        }, ct);

        await TryCancelLinkAsync(orderCode, "Người dùng hủy đơn", ct);
        return await ToDtoAsync(order!, ct);
    }

    // ---- dùng chung

    /// <summary>Hủy link payOS làm hết sức: lỗi chỉ ghi log vì đơn đã hủy ở phía mình (link cũ hết hạn sẽ tự đóng).</summary>
    private async Task TryCancelLinkAsync(long orderCode, string reason, CancellationToken ct)
    {
        try
        {
            await gateway.CancelAsync(orderCode, reason, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Không hủy được link thanh toán của đơn {OrderCode}", orderCode);
        }
    }

    // Mã đơn = Unix ms; trùng mã thì thử mã kế (tối đa 3 lần). Index một-đơn-chờ-mỗi-người chặn bấm mua hai lần (P6)
    private async Task InsertAsync(PaymentTransaction order, Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var baseCode = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        for (var attempt = 0; attempt < MaxInsertTries; attempt++)
        {
            order.OrderCode = baseCode + attempt;
            order.Description = "USAS" + (order.OrderCode % 100_000).ToString("D5");
            var result = await orders.TryAddAsync(order, ct);
            if (result == OrderInsertResult.Added)
                return;
            // Đơn chờ kia đã xong ngay sau đó (không tìm thấy nữa) thì thử lại như một lần trùng mã
            if (result == OrderInsertResult.PendingExists && await billing.FindPendingOrderCodeAsync(userId, ct) is { } existing)
                throw PendingExists(existing);
        }
        throw Rule("order_busy", 503, "Hệ thống đang bận, vui lòng thử lại.");
    }

    private async Task<OrderDto> ToDtoAsync(PaymentTransaction order, CancellationToken ct)
    {
        var plans = await billing.GetPlansAsync(ct);
        UserSubscription? cycle = null;
        if (order.UserSubscriptionId is { } id)
            (await orders.GetCyclesAsync([id], ct)).TryGetValue(id, out cycle);
        return ToDto(order, plans, cycle);
    }

    private static OrderDto ToDto(PaymentTransaction order, IReadOnlyList<SubscriptionPlan> plans, UserSubscription? cycle) =>
        OrderDto.From(order, plans.FirstOrDefault(p => p.Id == order.SubscriptionPlanId)?.Code ?? string.Empty, cycle);

    private static BillingRuleException Rule(string code, int status, string message) => new(code, status, message);

    private static BillingRuleException NotFound() => Rule("order_not_found", 404, "Không tìm thấy đơn hàng.");

    private static BillingRuleException PendingExists(long orderCode) => new(
        "pending_order_exists", 409, "Bạn đang có một đơn chờ thanh toán.", new Dictionary<string, object?> { ["orderCode"] = orderCode });
}
