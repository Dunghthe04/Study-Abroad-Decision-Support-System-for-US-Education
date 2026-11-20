using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class PlanAdminRepository(AppDbContext db) : IPlanAdminRepository
{
    private sealed record Row(SubscriptionPlan Plan, int ActiveCycles, int Cycles, int Orders)
    {
        public PlanWithUsage ToResult() => new(Plan, new PlanUsage(ActiveCycles, Cycles, Orders));
    }

    private IQueryable<Row> Rows(IQueryable<SubscriptionPlan> plans) => plans.Select(p => new Row(
        p,
        db.UserSubscriptions.Count(c => c.SubscriptionPlanId == p.Id && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued)),
        db.UserSubscriptions.Count(c => c.SubscriptionPlanId == p.Id),
        db.PaymentTransactions.Count(o => o.SubscriptionPlanId == p.Id)));

    public async Task<IReadOnlyList<PlanWithUsage>> ListAsync(CancellationToken ct = default) =>
        (await Rows(db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)).ToListAsync(ct))
            .Select(r => r.ToResult()).ToList();

    public async Task<PlanWithUsage?> FindAsync(Guid id, CancellationToken ct = default) =>
        (await Rows(db.SubscriptionPlans.Where(p => p.Id == id)).FirstOrDefaultAsync(ct))?.ToResult();

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        db.SubscriptionPlans.AnyAsync(p => p.Code == code, ct);

    public Task<bool> KindExistsAsync(string kind, CancellationToken ct = default) =>
        db.SubscriptionPlans.AnyAsync(p => p.Kind == kind, ct);

    public async Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }
}
