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
}
