using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>
/// Admin xử lý đơn hàng (UC-132..134). Lỗi quy tắc là <see cref="BillingRuleException"/>:
/// note_required 400, order_not_found 404, already_paid / not_mismatch / buyer_deleted 409, gateway_unavailable 502.
/// </summary>
public interface IAdminOrderService
{
    /// <summary>UC-132. pageSize kẹp 1..50; status chữ thường (không biết thì ra danh sách rỗng).</summary>
    Task<PagedResult<AdminOrderDto>> ListAsync(string? status, string? q, int page, int pageSize, CancellationToken ct = default);

    Task<AdminOrderDetailDto> GetAsync(long orderCode, CancellationToken ct = default);

    /// <summary>
    /// [UC-75 lớp 2] Họ tên, email người mua của đơn thuộc tài khoản đã xóa hẳn. Mỗi lần xem ghi nhật ký truy cập (UC-91, UC-124).
    /// Đơn không có mã người mua hoặc dòng người mua đã hết hạn: retained_buyer_not_found (404).
    /// </summary>
    Task<RetainedBuyerDto> GetRetainedBuyerAsync(Guid adminUserId, long orderCode, CancellationToken ct = default);

    /// <summary>UC-133 / UC-134: kích hoạt tay một đơn chưa trả (bỏ qua so khớp số tiền), kèm ghi chú bắt buộc.</summary>
    Task<AdminOrderDto> ActivateAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-133: từ chối đơn lệch tiền (mismatch), đơn thành cancelled, báo người mua.</summary>
    Task<AdminOrderDto> RejectAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-134: hỏi lại payOS và áp dụng trạng thái như job đối soát.</summary>
    Task<AdminOrderDto> RecheckAsync(Guid adminUserId, long orderCode, DateTime nowUtc, CancellationToken ct = default);
}
