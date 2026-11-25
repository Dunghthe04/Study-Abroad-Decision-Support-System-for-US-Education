using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>
/// Áp dụng trạng thái payOS vào một đơn chưa trả. Dùng chung cho job đối soát (mọi đơn đang chờ) và admin kiểm lại (một đơn).
/// Chỉ ghi qua khóa dòng đơn: webhook thanh toán tới cùng lúc không bị ghi đè thành hết hạn / hủy.
/// </summary>
public sealed class OrderReconciler(
    IOrderRepository orders,
    IOrderService orderService,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    BillingOptions options,
    ILogger<OrderReconciler>? logger = null,
    INotificationService? notifications = null)
{
    /// <summary>
    /// Hỏi payOS rồi áp dụng. Lỗi cổng được ghi log và coi như không biết trạng thái: đơn chờ chưa bị coi là hết hạn tới ExpiresAt + UnknownStatusExpiryHours
    /// (chưa biết khách đã trả chưa). Đơn đã hết hạn cũng đi qua đây: payOS báo PAID thì kích hoạt (trả muộn, C6), còn lại giữ nguyên.
    /// </summary>
    public async Task ReconcileAsync(long orderCode, DateTime nowUtc, CancellationToken ct = default)
    {
        GatewayStatus? status = null;
        try
        {
            status = await gateway.GetStatusAsync(orderCode, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Không lấy được trạng thái payOS của đơn {OrderCode}", orderCode);
        }
        await ApplyStatusAsync(orderCode, status, nowUtc, adminUserId: null, ct);
    }

    /// <summary>
    /// Bảng ánh xạ trạng thái payOS → đơn. PAID áp dụng cho mọi đơn chưa trả (hết hạn / hủy / lệch tiền vẫn được kích hoạt như webhook, P16);
    /// các trạng thái còn lại chỉ đổi đơn đang <c>pending</c>: đơn <c>mismatch</c> nghĩa là đã có tiền về, chỉ admin xử lý.
    /// <paramref name="status"/> null = không hỏi được payOS.
    /// LƯU Ý: gọi <see cref="IOrderService.ApplyPaymentAsync"/> xóa theo dõi của DbContext, nên hàm này không để thực thể nào cho người gọi.
    /// </summary>
    public async Task ApplyStatusAsync(long orderCode, GatewayStatus? status, DateTime nowUtc, Guid? adminUserId, CancellationToken ct = default)
    {
        var order = await orders.FindByCodeAsync(orderCode, ct);
        if (order is null || order.Status == OrderStatuses.Paid)
            return;

        if (status is not null)
        {
            switch (status.Status)
            {
                case GatewayStatuses.Paid:
                    await orderService.ApplyPaymentAsync(orderCode, status.AmountPaid, null, nowUtc, ct, new PaymentApplyOptions(AdminUserId: adminUserId));
                    return;
                case GatewayStatuses.Underpaid when order.Status == OrderStatuses.Pending:
                    // Đi qua ApplyPaymentAsync để có khóa dòng, trạng thái mismatch, lưu ReceivedAmountVnd thật và thông báo cho người mua
                    await orderService.ApplyPaymentAsync(orderCode, status.AmountPaid == (long)order.AmountVnd ? status.AmountPaid - 1 : status.AmountPaid, null, nowUtc, ct,
                        new PaymentApplyOptions(AdminUserId: adminUserId));
                    return;
                case GatewayStatuses.Cancelled when order.Status == OrderStatuses.Pending:
                    await MovePendingAsync(orderCode, OrderStatuses.Cancelled, ct);
                    return;
                case GatewayStatuses.Expired or GatewayStatuses.Failed when order.Status == OrderStatuses.Pending:
                    await ExpirePendingAndNotifyAsync(orderCode, ct);
                    return;
            }
        }

        // PENDING / PROCESSING: chờ thêm ân hạn cho webhook tới muộn, rồi hết hạn và đóng link.
        // Không hỏi được payOS (status null): có thể khách đã trả mà ta không biết, nên chỉ hết hạn tới hạn cứng ExpiresAt + UnknownStatusExpiryHours
        var expireAfter = status is null ? order.ExpiresAt.AddHours(options.UnknownStatusExpiryHours) : order.ExpiresAt.AddMinutes(options.PendingGraceMinutes);
        if (order.Status == OrderStatuses.Pending && nowUtc > expireAfter
            && await ExpirePendingAndNotifyAsync(orderCode, ct))
        {
            try
            {
                await gateway.CancelAsync(orderCode, "Đơn hết hạn", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "Không hủy được link thanh toán của đơn hết hạn {OrderCode}", orderCode);
            }
        }
    }

    /// <summary>[UC-128] Chuyển đơn pending sang expired dưới khóa dòng rồi báo in-app cho người mua.</summary>
    private async Task<bool> ExpirePendingAndNotifyAsync(long orderCode, CancellationToken ct)
    {
        var (moved, buyerId, planName) = await MovePendingAsync(orderCode, OrderStatuses.Expired, ct);
        if (moved && buyerId is { } userId && notifications is not null)
        {
            try
            {
                await notifications.NotifyAsync(
                    userId, NotificationTypes.BillingOrderExpired, "Đơn hàng đã hết hạn",
                    $"Đơn mua gói {planName} đã hết hạn thanh toán.",
                    $"/orders/{orderCode}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Không gửi được thông báo hết hạn cho đơn {OrderCode}", orderCode);
            }
        }
        return moved;
    }

    /// <summary>Đổi đơn đang chờ sang trạng thái mới dưới khóa dòng. False = đơn đã được xử lý (trả, hủy...) trong lúc đó, không đổi gì.</summary>
    private async Task<(bool Moved, Guid? BuyerId, string PlanName)> MovePendingAsync(long orderCode, string newStatus, CancellationToken ct)
    {
        var moved = false;
        Guid? buyerId = null;
        var planName = string.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var order = await orders.LockByCodeAsync(orderCode, txCt);
            if (order is null || order.Status != OrderStatuses.Pending)
                return;
            order.Status = newStatus;
            await orders.SaveAsync(txCt);
            moved = true;
            buyerId = order.UserId;
            planName = order.PlanName;
        }, ct);
        return (moved, buyerId, planName);
    }
}
