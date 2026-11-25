using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AdminOrderRepository(AppDbContext db) : IAdminOrderRepository
{
    // Nối trái gói, kỳ, người mua, admin xử lý và mã lưu trữ tài khoản đã xóa (chỉ lấy BuyerCode, không lộ họ tên/email cho tới khi gọi reveal)
    private IQueryable<Joined> Joins() =>
        from o in db.PaymentTransactions.AsNoTracking()
        join p in db.SubscriptionPlans.AsNoTracking() on o.SubscriptionPlanId equals p.Id into plans
        from p in plans.DefaultIfEmpty()
        join c in db.UserSubscriptions.AsNoTracking() on o.UserSubscriptionId equals c.Id into cycles
        from c in cycles.DefaultIfEmpty()
        join u in db.Users.AsNoTracking() on o.UserId equals u.Id into buyers
        from u in buyers.DefaultIfEmpty()
        join a in db.Users.AsNoTracking() on o.HandledByUserId equals a.Id into admins
        from a in admins.DefaultIfEmpty()
        join r in db.DeletedBuyerRetentions.AsNoTracking() on o.DeletedBuyerRetentionId equals r.Id into retentions
        from r in retentions.DefaultIfEmpty()
        select new Joined { Order = o, Plan = p, Cycle = c, Buyer = u, Admin = a, Retention = r };

    private sealed class Joined
    {
        public required PaymentTransaction Order { get; init; }
        public SubscriptionPlan? Plan { get; init; }
        public UserSubscription? Cycle { get; init; }
        public User? Buyer { get; init; }
        public User? Admin { get; init; }
        public DeletedBuyerRetention? Retention { get; init; }
    }

    private static IQueryable<AdminOrderRow> Project(IQueryable<Joined> query) =>
        query.Select(x => new AdminOrderRow(
            x.Order, x.Plan!.Code, x.Cycle, x.Buyer!.FullName, x.Buyer.Email, x.Admin!.FullName,
            x.Retention != null ? x.Retention.BuyerCode : null));

    public async Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> ListAsync(
        string? status, string? q, int page, int pageSize,
        DateTime? fromUtc = null, DateTime? toUtcExclusive = null, CancellationToken ct = default)
    {
        var query = Joins();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Order.Status == status);
        if (fromUtc.HasValue)
            query = query.Where(x => x.Order.CreatedAt >= fromUtc.Value);
        if (toUtcExclusive.HasValue)
            query = query.Where(x => x.Order.CreatedAt < toUtcExclusive.Value);
        if (!string.IsNullOrEmpty(q))
        {
            var lower = q.ToLower();
            if (long.TryParse(q, out var code))
            {
                // Số: khớp mã đơn chính xác hoặc mã giao dịch payOS chính xác (kể cả đơn của tài khoản đã xóa)
                query = query.Where(x =>
                    x.Order.OrderCode == code
                    || (x.Order.GatewayTransactionId != null && x.Order.GatewayTransactionId.ToLower() == lower));
            }
            else
            {
                // Chữ: khớp tên/email người mua, mã giao dịch payOS hoặc mã người mua đã xóa (kể cả tài khoản đã xóa)
                query = query.Where(x =>
                    (x.Buyer != null && (x.Buyer.Email.ToLower().Contains(lower) || x.Buyer.FullName.ToLower().Contains(lower)))
                    || (x.Order.GatewayTransactionId != null && x.Order.GatewayTransactionId.ToLower().Contains(lower))
                    || (x.Retention != null && x.Retention.BuyerCode.ToLower().Contains(lower)));
            }
        }
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(x => x.Order.CreatedAt).ThenByDescending(x => x.Order.OrderCode)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return (items, total);
    }

    public Task<AdminOrderRow?> FindAsync(long orderCode, CancellationToken ct = default) =>
        Project(Joins().Where(x => x.Order.OrderCode == orderCode)).FirstOrDefaultAsync(ct);

    public Task<DeletedBuyerRetention?> FindDeletedBuyerRetentionAsync(long orderCode, CancellationToken ct = default) =>
        (from o in db.PaymentTransactions.AsNoTracking()
         join r in db.DeletedBuyerRetentions.AsNoTracking() on o.DeletedBuyerRetentionId equals r.Id
         where o.OrderCode == orderCode
         select r).FirstOrDefaultAsync(ct);
}
