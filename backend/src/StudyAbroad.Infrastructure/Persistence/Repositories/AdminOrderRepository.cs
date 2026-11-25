using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing.Admin;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AdminOrderRepository(AppDbContext db) : IAdminOrderRepository
{
    // Nối trái gói, kỳ, người mua và admin xử lý (người mua bị xóa hẳn thì UserId null, nên không có tên và email)
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
        select new Joined { Order = o, Plan = p, Cycle = c, Buyer = u, Admin = a };

    private sealed class Joined
    {
        public required StudyAbroad.Domain.Entities.PaymentTransaction Order { get; init; }
        public StudyAbroad.Domain.Entities.SubscriptionPlan? Plan { get; init; }
        public StudyAbroad.Domain.Entities.UserSubscription? Cycle { get; init; }
        public StudyAbroad.Domain.Entities.User? Buyer { get; init; }
        public StudyAbroad.Domain.Entities.User? Admin { get; init; }
    }

    private static IQueryable<AdminOrderRow> Project(IQueryable<Joined> query) =>
        query.Select(x => new AdminOrderRow(x.Order, x.Plan!.Code, x.Cycle, x.Buyer!.FullName, x.Buyer.Email, x.Admin!.FullName));

    public async Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> ListAsync(string? status, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Joins();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Order.Status == status);
        if (!string.IsNullOrEmpty(q))
        {
            // Số thì là mã đơn chính xác; còn lại tìm trong email người mua, không phân biệt hoa thường
            if (long.TryParse(q, out var code))
                query = query.Where(x => x.Order.OrderCode == code);
            else
            {
                var lower = q.ToLower();
                query = query.Where(x => x.Buyer != null && x.Buyer.Email.ToLower().Contains(lower));
            }
        }
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(x => x.Order.CreatedAt).ThenByDescending(x => x.Order.OrderCode)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return (items, total);
    }

    public Task<AdminOrderRow?> FindAsync(long orderCode, CancellationToken ct = default) =>
        Project(Joins().Where(x => x.Order.OrderCode == orderCode)).FirstOrDefaultAsync(ct);

    public Task<StudyAbroad.Domain.Entities.RetainedBuyer?> FindRetainedBuyerAsync(Guid buyerCode, CancellationToken ct = default) =>
        db.RetainedBuyers.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buyerCode, ct);
}
