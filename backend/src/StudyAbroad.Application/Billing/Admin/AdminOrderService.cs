using System.Text.Encodings.Web;
using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Billing.Jobs;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>
/// Admin xử lý đơn (UC-132..134). Kích hoạt và kiểm lại đi qua <see cref="IOrderService.ApplyPaymentAsync"/> (một transaction riêng, xóa theo dõi DbContext),
/// nên dòng nhật ký ghi SAU khi hàm trả về, ở lần lưu riêng; không bọc trong transaction ngoài (transaction không lồng nhau).
/// Hai admin cùng thao tác trên một đơn: khóa dòng đơn quyết định ai thắng, bên thua nhận already_paid / not_mismatch.
/// </summary>
public sealed class AdminOrderService(
    IAdminOrderRepository adminOrders,
    IOrderRepository orders,
    IOrderService orderService,
    OrderReconciler reconciler,
    IPaymentGateway gateway,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    INotificationService notifications) : IAdminOrderService
{
    public const int MaxPageSize = 50;
    private const int MaxNoteLength = 1000;
    private const string AuditEntityType = "payment_transaction";
    // Ghi chú tiếng Việt giữ nguyên chữ trong nhật ký (không bị đổi thành mã thoát unicode); chuỗi chỉ lưu và hiện dạng văn bản, không nhúng vào HTML
    private static readonly JsonSerializerOptions AuditJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<PagedResult<AdminOrderDto>> ListAsync(string? status, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await adminOrders.ListAsync(
            string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(q) ? null : q.Trim(), page, pageSize, ct);
        return new PagedResult<AdminOrderDto>(items.Select(AdminOrderDto.From).ToList(), page, pageSize, total);
    }

    public async Task<AdminOrderDetailDto> GetAsync(long orderCode, CancellationToken ct = default)
    {
        var row = await adminOrders.FindAsync(orderCode, ct) ?? throw NotFound();
        return new AdminOrderDetailDto(AdminOrderDto.From(row), row.Order.PayloadJson);
    }

    public async Task<AdminOrderDto> ActivateAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default)
    {
        note = RequireNote(note);
        var row = await adminOrders.FindAsync(orderCode, ct) ?? throw NotFound();
        if (row.Order.Status == OrderStatuses.Paid)
            throw AlreadyPaid();

        // Đơn còn pending nghĩa là link payOS vẫn mở: sau khi nhận tiền tay thì đóng link để khách không trả lần nữa
        var wasPending = row.Order.Status == OrderStatuses.Pending;

        // Số tiền lấy từ chính đơn: admin đã xác nhận tiền về, không so khớp với số payOS báo
        var result = await orderService.ApplyPaymentAsync(
            orderCode, (long)row.Order.AmountVnd, null, nowUtc, ct, new PaymentApplyOptions(SkipAmountCheck: true, adminUserId, note));
        if (result.Status is PaymentApplyStatus.AlreadyPaid)
            throw AlreadyPaid();
        if (result.Status is PaymentApplyStatus.UnknownOrder)
            throw NotFound();

        // Tiền đã được xác nhận, kể cả khi không kích hoạt được (người mua đã xóa): đóng link payOS và để lại dấu vết kiểm toán trước khi báo lỗi
        // (đã commit, nên không theo token của request: admin đóng trang không làm mất nó)
        if (wasPending)
            await CloseGatewayLinkAsync(orderCode);
        var outcome = result.Status switch
        {
            PaymentApplyStatus.Mismatch => "buyer_deleted",
            _ => (string?)null,
        };
        await AuditAsync(adminUserId, "order.activate", row.Order,
            new { orderCode, note, result = result.Status.ToString(), outcome, cycleId = result.CycleId }, CancellationToken.None);
        switch (result.Status)
        {
            case PaymentApplyStatus.Mismatch:
                throw Rule("buyer_deleted", 409, "Tài khoản người mua đã bị xóa nên không có ai để nhận gói.");
        }
        return await LoadDtoAsync(orderCode, ct);
    }

    public async Task<AdminOrderDto> RejectAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default)
    {
        note = RequireNote(note);
        Guid? buyerId = null;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            // Khóa dòng đơn rồi mới kiểm trạng thái: kích hoạt hoặc từ chối của admin khác cùng lúc chỉ một bên đi tiếp
            var order = await orders.LockByCodeAsync(orderCode, txCt) ?? throw NotFound();
            if (order.Status != OrderStatuses.Mismatch)
                throw Rule("not_mismatch", 409, "Chỉ từ chối được đơn sai số tiền.");
            order.Status = OrderStatuses.Cancelled;
            order.AdminNote = note;
            order.HandledByUserId = adminUserId;
            await orders.SaveAsync(txCt);
            await AuditAsync(adminUserId, "order.reject", order, new { orderCode, note }, txCt);
            buyerId = order.UserId;
        }, ct);

        if (buyerId is { } buyer)
            await notifications.NotifyAsync(buyer, NotificationTypes.BillingPaymentMismatch, "Đơn thanh toán bị từ chối",
                "Quản trị viên đã hủy đơn này sau khi kiểm tra khoản chuyển. Nếu bạn đã bị trừ tiền, hãy liên hệ hỗ trợ.", $"/orders/{orderCode}", CancellationToken.None);   // đơn đã hủy (commit): báo người mua dù request đã bị hủy
        return await LoadDtoAsync(orderCode, ct);
    }

    public async Task<AdminOrderDto> RecheckAsync(Guid adminUserId, long orderCode, DateTime nowUtc, CancellationToken ct = default)
    {
        var row = await adminOrders.FindAsync(orderCode, ct) ?? throw NotFound();
        if (row.Order.Status == OrderStatuses.Paid)
            throw AlreadyPaid();

        GatewayStatus status;
        try
        {
            status = await gateway.GetStatusAsync(orderCode, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Rule("gateway_unavailable", 502, "Không kết nối được payOS, vui lòng thử lại sau.");
        }

        await reconciler.ApplyStatusAsync(orderCode, status, nowUtc, adminUserId, ct);
        // Trạng thái payOS đã được áp dụng (đã commit): audit không theo token của request
        await AuditAsync(adminUserId, "order.recheck", row.Order,
            new { orderCode, before = row.Order.Status, gatewayStatus = status.Status, amountPaid = status.AmountPaid }, CancellationToken.None);
        return await LoadDtoAsync(orderCode, ct);
    }

    // ---- nội bộ

    /// <summary>Hủy link thanh toán ở payOS sau khi đã nhận tiền tay. Lỗi cổng không làm hỏng kết quả (đơn đã chốt), link tự hết hạn.</summary>
    private async Task CloseGatewayLinkAsync(long orderCode)
    {
        try
        {
            await gateway.CancelAsync(orderCode, "Quản trị viên đã xác nhận thanh toán", CancellationToken.None);
        }
        catch (Exception)
        {
            // best-effort
        }
    }

    /// <summary>Nạp lại sau khi ghi: ApplyPaymentAsync đã xóa theo dõi nên bản trước đó không còn đúng.</summary>
    private async Task<AdminOrderDto> LoadDtoAsync(long orderCode, CancellationToken ct) =>
        AdminOrderDto.From(await adminOrders.FindAsync(orderCode, ct) ?? throw NotFound());

    private Task AuditAsync(Guid adminUserId, string action, PaymentTransaction order, object detail, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = action,
            EntityType = AuditEntityType,
            EntityId = order.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(detail, AuditJson),
        }, ct);

    private static string RequireNote(string? note)
    {
        note = note?.Trim();
        if (string.IsNullOrEmpty(note))
            throw Rule("note_required", 400, "Cần nhập ghi chú.");
        if (note.Length > MaxNoteLength)
            throw Rule("note_too_long", 400, "Ghi chú tối đa 1000 ký tự.");
        return note;
    }

    private static BillingRuleException Rule(string code, int status, string message) => new(code, status, message);
    private static BillingRuleException NotFound() => Rule("order_not_found", 404, "Không tìm thấy đơn hàng.");
    private static BillingRuleException AlreadyPaid() => Rule("already_paid", 409, "Đơn này đã được thanh toán.");
}
