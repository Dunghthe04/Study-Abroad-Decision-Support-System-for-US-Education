using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>
/// Admin xử lý đơn hàng (UC-124..126). Lỗi quy tắc là <see cref="BillingRuleException"/>:
/// note_required 400, order_not_found / buyer_info_not_found 404, already_paid / not_mismatch / gateway_not_paid / buyer_deleted 409, gateway_unavailable 502.
/// </summary>
public interface IAdminOrderService
{
    /// <summary>UC-124. pageSize kẹp 1..50; status chữ thường; from/to theo ngày Việt Nam (tính cả hai đầu).</summary>
    Task<PagedResult<AdminOrderDto>> ListAsync(
        string? status, string? q, int page, int pageSize,
        DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);

    Task<AdminOrderDetailDto> GetAsync(long orderCode, CancellationToken ct = default);

    /// <summary>UC-125 (đơn MISMATCH) / UC-126 (đơn thường: bắt buộc payOS trả PAID): kích hoạt gói thủ công kèm lý do bắt buộc.</summary>
    Task<AdminOrderDto> ActivateAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-125 3a: hủy đơn lệch số tiền (mismatch) kèm ghi chú hoàn tiền ngoài hệ thống, đơn thành cancelled, báo người mua.</summary>
    Task<AdminOrderDto> RejectAsync(Guid adminUserId, long orderCode, string? note, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-126: hỏi trạng thái giao dịch từ payOS và trả về kết quả (KHÔNG tự kích hoạt).</summary>
    Task<AdminOrderRecheckDto> RecheckAsync(Guid adminUserId, long orderCode, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-124 2a / UC-126 E3: xem họ tên + email đã lưu 10 năm của tài khoản đã xóa và ghi nhật ký truy cập UC-91.</summary>
    Task<AdminOrderBuyerInfoDto> RevealBuyerAsync(
        Guid adminUserId, long orderCode, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
}
