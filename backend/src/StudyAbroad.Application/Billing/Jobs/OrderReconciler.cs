using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
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
    ILogger<OrderReconciler>? logger = null)
{
    /// <summary>Hỏi payOS rồi áp dụng. Lỗi cổng được ghi log và coi như không biết trạng thái (đơn quá hạn vẫn hết hạn).</summary>
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
                    // Đi qua ApplyPaymentAsync để có khóa dòng, trạng thái mismatch và thông báo cho người mua; số tiền luôn thấp hơn đơn để chắc chắn lệch
                    await orderService.ApplyPaymentAsync(orderCode, Math.Min(status.AmountPaid, (long)order.AmountVnd - 1), null, nowUtc, ct,
                        new PaymentApplyOptions(AdminUserId: adminUserId));
                    return;
                case GatewayStatuses.Cancelled when order.Status == OrderStatuses.Pending:
                    await MovePendingAsync(orderCode, OrderStatuses.Cancelled, ct);
                    return;
                case GatewayStatuses.Expired or GatewayStatuses.Failed when order.Status == OrderStatuses.Pending:
                    await MovePendingAsync(orderCode, OrderStatuses.Expired, ct);
                    return;
            }
        }

        // PENDING / PROCESSING / không biết: chờ thêm ân hạn cho webhook tới muộn, rồi hết hạn và đóng link
        if (order.Status == OrderStatuses.Pending && nowUtc > order.ExpiresAt.AddMinutes(options.PendingGraceMinutes)
            && await MovePendingAsync(orderCode, OrderStatuses.Expired, ct))
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

    /// <summary>Đổi đơn đang chờ sang trạng thái mới dưới khóa dòng. False = đơn đã được xử lý (trả, hủy...) trong lúc đó, không đổi gì.</summary>
    private async Task<bool> MovePendingAsync(long orderCode, string newStatus, CancellationToken ct)
    {
        var moved = false;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var order = await orders.LockByCodeAsync(orderCode, txCt);
            if (order is null || order.Status != OrderStatuses.Pending)
                return;
            order.Status = newStatus;
            await orders.SaveAsync(txCt);
            moved = true;
        }, ct);
        return moved;
    }
}
