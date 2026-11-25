using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Đọc đơn cho màn quản trị (UC-124). Mọi ghi đi qua IOrderService và IOrderRepository.</summary>
public interface IAdminOrderRepository
{
    /// <summary>
    /// Mới nhất trước. status: đúng trạng thái (chữ thường), null = tất cả.
    /// q: mã đơn chính xác, mã giao dịch payOS, tên/email người mua hoặc mã người mua đã xóa (không phân biệt hoa thường).
    /// fromUtc / toUtcExclusive: khoảng thời gian tạo đơn [fromUtc, toUtcExclusive).
    /// </summary>
    Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> ListAsync(
        string? status, string? q, int page, int pageSize,
        DateTime? fromUtc = null, DateTime? toUtcExclusive = null, CancellationToken ct = default);

    Task<AdminOrderRow?> FindAsync(long orderCode, CancellationToken ct = default);

    /// <summary>[UC-124 2a] Lấy bản ghi lưu thông tin người mua (bảng riêng) của một đơn thuộc tài khoản đã xóa; null nếu không có.</summary>
    Task<DeletedBuyerRetention?> FindDeletedBuyerRetentionAsync(long orderCode, CancellationToken ct = default) =>
        Task.FromResult<DeletedBuyerRetention?>(null);
}
