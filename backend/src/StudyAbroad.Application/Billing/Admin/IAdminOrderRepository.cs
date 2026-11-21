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
}
