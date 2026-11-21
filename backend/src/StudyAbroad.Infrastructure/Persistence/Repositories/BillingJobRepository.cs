using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing.Jobs;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class BillingJobRepository(AppDbContext db) : IBillingJobRepository
{
    public async Task<IReadOnlyList<PendingOrderRef>> ListPendingOrdersAsync(CancellationToken ct = default) =>
        await db.PaymentTransactions.AsNoTracking()
            .Where(o => o.Status == OrderStatuses.Pending)
            .OrderBy(o => o.ExpiresAt)
            .Select(o => new PendingOrderRef(o.OrderCode, o.ExpiresAt))
            .Take(IBillingJobRepository.MaxOrdersPerTick)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PendingOrderRef>> ListRecentlyExpiredOrdersAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        await db.PaymentTransactions.AsNoTracking()
            .Where(o => o.Status == OrderStatuses.Expired && o.ExpiresAt >= sinceUtc)
            .OrderByDescending(o => o.ExpiresAt)
            .Select(o => new PendingOrderRef(o.OrderCode, o.ExpiresAt))
            .Take(IBillingJobRepository.MaxOrdersPerTick)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LaneRef>> FindDueLanesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var dueActive = await db.UserSubscriptions.AsNoTracking()
            .Where(c => c.Status == CycleStatuses.Active && (c.EndsAt == null || c.EndsAt <= nowUtc))
            .Select(c => new { c.UserId, c.Kind })
            .Distinct()
            .ToListAsync(ct);
        // Kỳ queued chỉ làm làn tới hạn khi kỳ active của nó chuyển được ngay: không còn kỳ active, đã tới hạn, hạn mức 0, hoặc token đã dùng ≥ hạn mức
        // (cùng luật với CycleRules.Advance và với bộ lọc MeteringFilters.Cycle). Còn nhiều token thì bỏ qua: không tốn một lượt đọc + cộng token mỗi phút cho mỗi làn đang gia hạn
        var queuedReady = await db.UserSubscriptions.AsNoTracking()
            .Where(q => q.Status == CycleStatuses.Queued
                && (!db.UserSubscriptions.Any(a => a.UserId == q.UserId && a.Kind == q.Kind && a.Status == CycleStatuses.Active)
                    || db.UserSubscriptions.Any(a => a.UserId == q.UserId && a.Kind == q.Kind && a.Status == CycleStatuses.Active
                        && (a.EndsAt == null || a.EndsAt <= nowUtc || a.TokenQuota <= 0
                            || db.AiCalls.Where(t => t.UserSubscriptionId == a.Id && t.Kind == BillingRules.MeteredAiKind).Sum(t => (long?)t.TotalTokens) >= a.TokenQuota))))
            .Select(c => new { c.UserId, c.Kind })
            .Distinct()
            .ToListAsync(ct);
        // Nhóm Gia đình còn active mà làn family không còn kỳ active: AdvanceLaneAsync sẽ kết thúc nhóm
        var lapsedGroups = await db.FamilyGroups.AsNoTracking()
            .Where(g => g.Status == FamilyGroupStatuses.Active
                && !db.UserSubscriptions.Any(c => c.UserId == g.OwnerUserId && c.Kind == PlanKinds.Family && c.Status == CycleStatuses.Active))
            .Select(g => g.OwnerUserId)
            .ToListAsync(ct);
        return dueActive.Concat(queuedReady).Select(l => new LaneRef(l.UserId, l.Kind))
            .Concat(lapsedGroups.Select(owner => new LaneRef(owner, PlanKinds.Family)))
            .Distinct()
            .ToList();
    }

    public Task<List<UserSubscription>> FindCyclesDueForReminderAsync(DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default) =>
        db.UserSubscriptions
            .Where(c => c.Status == CycleStatuses.Active && c.EndsAt > nowUtc && c.EndsAt <= untilUtc && c.ReminderSentAt == null
                && !db.UserSubscriptions.Any(q => q.UserId == c.UserId && q.Kind == c.Kind && q.Status == CycleStatuses.Queued))
            .ToListAsync(ct);

    public Task<List<UserSubscription>> FindActiveCyclesAwaitingUsageNoticeAsync(CancellationToken ct = default) =>
        db.UserSubscriptions.AsNoTracking()
            .Where(c => c.Status == CycleStatuses.Active && c.TokenQuota > 0 && (c.Warned80At == null || c.ExhaustedNotifiedAt == null))
            .ToListAsync(ct);

    public Task<UserSubscription?> FindCycleAsync(Guid cycleId, CancellationToken ct = default) =>
        db.UserSubscriptions.FirstOrDefaultAsync(c => c.Id == cycleId, ct);

    // Cùng luật với MeteringFilters.Cycle (kỳ này, chỉ lần gọi Claude), gom theo kỳ
    public async Task<IReadOnlyDictionary<Guid, long>> SumCycleTokensAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default)
    {
        if (cycleIds.Count == 0)
            return new Dictionary<Guid, long>();
        var ids = cycleIds.ToList();
        var rows = await db.AiCalls
            .Where(a => a.UserSubscriptionId != null && ids.Contains(a.UserSubscriptionId.Value) && a.Kind == BillingRules.MeteredAiKind)
            .GroupBy(a => a.UserSubscriptionId!.Value)
            .Select(g => new { CycleId = g.Key, Tokens = g.Sum(a => (long?)a.TotalTokens) ?? 0 })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.CycleId, r => r.Tokens);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
