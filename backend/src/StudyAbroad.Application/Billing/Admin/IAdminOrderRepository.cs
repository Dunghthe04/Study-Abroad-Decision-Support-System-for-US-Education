namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Đọc đơn cho màn quản trị (UC-132). Mọi ghi đi qua IOrderService và IOrderRepository.</summary>
public interface IAdminOrderRepository
{
    /// <summary>
    /// Mới nhất trước. status: đúng trạng thái (chữ thường), null = tất cả.
    /// q: toàn chữ số thì là mã đơn chính xác, còn lại là chuỗi con của email người mua (không phân biệt hoa thường).
    /// </summary>
    Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> ListAsync(string? status, string? q, int page, int pageSize, CancellationToken ct = default);

    Task<AdminOrderRow?> FindAsync(long orderCode, CancellationToken ct = default);

    /// <summary>[UC-75 lớp 2] Họ tên, email người mua đã giữ lại theo mã người mua; null khi đã hết hạn và bị xóa.</summary>
    Task<StudyAbroad.Domain.Entities.RetainedBuyer?> FindRetainedBuyerAsync(Guid buyerCode, CancellationToken ct = default);
}
