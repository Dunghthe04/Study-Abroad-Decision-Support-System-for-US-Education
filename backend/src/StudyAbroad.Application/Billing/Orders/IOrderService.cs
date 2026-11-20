using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Billing.Orders;

public interface IOrderService
{
    /// <summary>Tạo đơn chờ thanh toán và link payOS (UC-78). Lỗi quy tắc: BillingRuleException (role_not_allowed, age_restricted, plan_not_for_sale, pending_order_exists, already_renewed, already_in_family, gateway_unavailable).</summary>
    Task<OrderDto> CreateAsync(Guid userId, string role, string planCode, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Đơn của chính người dùng; đơn người khác hoặc không có: 404 order_not_found.</summary>
    Task<OrderDto> GetMineAsync(Guid userId, long orderCode, CancellationToken ct = default);

    /// <summary>Lịch sử đơn mới nhất trước; pageSize kẹp 1..50.</summary>
    Task<PagedResult<OrderDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Hủy đơn đang chờ (409 not_pending nếu không còn chờ); hủy link payOS làm hết sức.</summary>
    Task<OrderDto> CancelAsync(Guid userId, long orderCode, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Áp dụng một khoản thanh toán cho đơn (webhook, job đối soát, admin kích hoạt): một transaction, khóa dòng đơn,
    /// thử lại một lần khi vướng index duy nhất. Gọi lại với đơn đã trả thì trả AlreadyPaid, không đổi gì.
    /// LƯU Ý cho người gọi (job đối soát, admin): hàm xóa theo dõi của DbContext trong scope (đọc lại dữ liệu mới và không ghi lại phần dang dở),
    /// nên mọi thực thể đã nạp trước đó bị tách ra. Cần sửa tiếp đơn hay kỳ nào thì nạp lại sau khi gọi, nếu không SaveChanges không ghi gì và không báo lỗi.
    /// </summary>
    Task<PaymentApplyResult> ApplyPaymentAsync(long orderCode, long amountPaid, string? payloadJson, DateTime nowUtc, CancellationToken ct = default, PaymentApplyOptions? options = null);

    /// <summary>Webhook payOS. False = chữ ký sai hoặc body hỏng (controller trả 400); true cho mọi trường hợp còn lại, kể cả đơn không biết.</summary>
    Task<bool> HandleWebhookAsync(string rawBody, DateTime nowUtc, CancellationToken ct = default);
}

/// <param name="SkipAmountCheck">Admin kích hoạt tay một đơn lệch tiền: bỏ qua so khớp số tiền.</param>
public sealed record PaymentApplyOptions(bool SkipAmountCheck = false, Guid? AdminUserId = null, string? AdminNote = null);

public enum PaymentApplyStatus
{
    /// <summary>Kỳ mới chạy ngay.</summary>
    Activated,
    /// <summary>Kỳ mới xếp hàng sau kỳ đang chạy.</summary>
    Queued,
    /// <summary>Đơn đã trả từ trước, không đổi gì.</summary>
    AlreadyPaid,
    /// <summary>Số tiền không khớp (hoặc người mua không còn): đơn chuyển mismatch chờ admin.</summary>
    Mismatch,
    /// <summary>Tiền đã nhận nhưng làn đã có kỳ đang chạy và kỳ chờ (P22): đơn mismatch chờ admin.</summary>
    LaneFull,
    UnknownOrder,
}

/// <param name="CycleId">Kỳ được tạo (Activated, Queued) hoặc đã tạo từ trước (AlreadyPaid).</param>
public sealed record PaymentApplyResult(PaymentApplyStatus Status, Guid? CycleId = null);
