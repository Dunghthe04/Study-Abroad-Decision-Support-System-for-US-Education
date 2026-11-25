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

    public async Task<IReadOnlyList<CycleOwnerRef>> FindDueCycleOwnersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var dueActive = await db.UserSubscriptions.AsNoTracking()
            .Where(c => c.Status == CycleStatuses.Active && (c.EndsAt == null || c.EndsAt <= nowUtc))
            .Select(c => new { c.UserId, c.Kind })
            .Distinct()
            .ToListAsync(ct);
        // Nhóm Gia đình còn active mà chủ không còn kỳ Gia đình active nào: EndExpiredCyclesAsync sẽ kết thúc nhóm
        var lapsedGroups = await db.FamilyGroups.AsNoTracking()
            .Where(g => g.Status == FamilyGroupStatuses.Active
                && !db.UserSubscriptions.Any(c => c.UserId == g.OwnerUserId && c.Kind == PlanKinds.Family && c.Status == CycleStatuses.Active))
            .Select(g => g.OwnerUserId)
            .ToListAsync(ct);
        return dueActive.Select(l => new CycleOwnerRef(l.UserId, l.Kind))
            .Concat(lapsedGroups.Select(owner => new CycleOwnerRef(owner, PlanKinds.Family)))
            .Distinct()
            .ToList();
    }

    public Task<List<UserSubscription>> FindCyclesDueForReminderAsync(DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default) =>
        db.UserSubscriptions
            .Where(c => c.Status == CycleStatuses.Active && c.EndsAt > nowUtc && c.EndsAt <= untilUtc && c.ReminderSentAt == null)
            .ToListAsync(ct);

    public Task<List<UserSubscription>> FindActiveCyclesAwaitingUsageNoticeAsync(CancellationToken ct = default) =>
        db.UserSubscriptions.AsNoTracking()
            .Where(c => c.Status == CycleStatuses.Active && c.TokenQuota > 0
                && (c.Warned80At == null || c.ExhaustedNotifiedAt == null || (c.QuotaPeriodDays != null && c.QuotaPeriodDays < c.DurationDays)))
            .ToListAsync(ct);

    public Task<UserSubscription?> FindCycleAsync(Guid cycleId, CancellationToken ct = default) =>
        db.UserSubscriptions.FirstOrDefaultAsync(c => c.Id == cycleId, ct);

    // Cùng luật với MeteringFilters.Cycle (kỳ này, lần gọi LLM claude + SLM ollama), gom theo kỳ
    public async Task<IReadOnlyDictionary<Guid, long>> SumCycleTokensAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default)
    {
        if (cycleIds.Count == 0)
            return new Dictionary<Guid, long>();
        var ids = cycleIds.ToList();
        var rows = await db.AiCalls
            .Where(a => a.UserSubscriptionId != null && ids.Contains(a.UserSubscriptionId.Value)
                && (a.Kind == BillingRules.ClaudeAiKind || a.Kind == BillingRules.OllamaAiKind))
            .GroupBy(a => a.UserSubscriptionId!.Value)
            .Select(g => new { CycleId = g.Key, Tokens = g.Sum(a => (long?)a.TotalTokens) ?? 0 })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.CycleId, r => r.Tokens);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
