using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Billing.Jobs;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Phần truy vấn của job nền và màn admin đơn hàng trên cùng cơ sở dữ liệu giả (cùng luật với BillingJobRepository / AdminOrderRepository)
internal sealed partial class BillingStore : IBillingJobRepository, IAdminOrderRepository
{
    /// <summary>Tài khoản để nối tên và email người mua / admin (AdminOrderRepository nối bảng users).</summary>
    public Dictionary<Guid, User> Accounts { get; } = [];

    public Task<IReadOnlyList<PendingOrderRef>> ListPendingOrdersAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PendingOrderRef>>(Orders.Where(o => o.Status == OrderStatuses.Pending).OrderBy(o => o.ExpiresAt)
            .Select(o => new PendingOrderRef(o.OrderCode, o.ExpiresAt)).Take(IBillingJobRepository.MaxOrdersPerTick).ToList());

    public Task<IReadOnlyList<PendingOrderRef>> ListRecentlyExpiredOrdersAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PendingOrderRef>>(Orders.Where(o => o.Status == OrderStatuses.Expired && o.ExpiresAt >= sinceUtc).OrderByDescending(o => o.ExpiresAt)
            .Select(o => new PendingOrderRef(o.OrderCode, o.ExpiresAt)).Take(IBillingJobRepository.MaxOrdersPerTick).ToList());

    public Task<IReadOnlyList<LaneRef>> FindDueLanesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var dueActive = Cycles.Where(c => c.Status == CycleStatuses.Active && (c.EndsAt == null || c.EndsAt <= nowUtc))
            .Select(c => new LaneRef(c.UserId, c.Kind));
        // Kỳ queued chỉ làm làn tới hạn khi kỳ active của nó chuyển được ngay (cùng luật với BillingJobRepository)
        var queuedReady = Cycles.Where(q => q.Status == CycleStatuses.Queued && Cycles.Where(a => a.UserId == q.UserId && a.Kind == q.Kind && a.Status == CycleStatuses.Active)
                .DefaultIfEmpty().Any(a => a is null || a.EndsAt == null || a.EndsAt <= nowUtc || a.TokenQuota <= 0
                    || (a.QuotaPeriodDays != null && a.QuotaPeriodDays < a.DurationDays)
                    || Calls.Where(t => t.UserSubscriptionId == a.Id && t.Kind == BillingRules.MeteredAiKind).Sum(t => (long)(t.TotalTokens ?? 0)) >= a.TokenQuota))
            .Select(c => new LaneRef(c.UserId, c.Kind));
        var lapsed = Groups.Where(g => g.Status == FamilyGroupStatuses.Active
                && !Cycles.Any(c => c.UserId == g.OwnerUserId && c.Kind == PlanKinds.Family && c.Status == CycleStatuses.Active))
            .Select(g => new LaneRef(g.OwnerUserId, PlanKinds.Family));
        return Task.FromResult<IReadOnlyList<LaneRef>>(dueActive.Concat(queuedReady).Concat(lapsed).Distinct().ToList());
    }

    public Task<List<UserSubscription>> FindCyclesDueForReminderAsync(DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default) =>
        Task.FromResult(Cycles.Where(c => c.Status == CycleStatuses.Active && c.EndsAt > nowUtc && c.EndsAt <= untilUtc && c.ReminderSentAt == null
            && !Cycles.Any(q => q.UserId == c.UserId && q.Kind == c.Kind && q.Status == CycleStatuses.Queued)).ToList());

    public Task<List<UserSubscription>> FindActiveCyclesAwaitingUsageNoticeAsync(CancellationToken ct = default) =>
        Task.FromResult(Cycles.Where(c => c.Status == CycleStatuses.Active && c.TokenQuota > 0
            && (c.Warned80At == null || c.ExhaustedNotifiedAt == null || (c.QuotaPeriodDays != null && c.QuotaPeriodDays < c.DurationDays))).ToList());

    public Task<UserSubscription?> FindCycleAsync(Guid cycleId, CancellationToken ct = default) =>
        Task.FromResult(Cycles.FirstOrDefault(c => c.Id == cycleId));

    public Task<IReadOnlyDictionary<Guid, long>> SumCycleTokensAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, long>>(Calls.Where(a => a.UserSubscriptionId is { } id && cycleIds.Contains(id) && a.Kind == BillingRules.MeteredAiKind)
            .GroupBy(a => a.UserSubscriptionId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => (long)(a.TotalTokens ?? 0))));

    private AdminOrderRow Row(PaymentTransaction o)
    {
        var buyer = o.UserId is { } b && Accounts.TryGetValue(b, out var u) ? u : null;
        var admin = o.HandledByUserId is { } a && Accounts.TryGetValue(a, out var h) ? h : null;
        return new AdminOrderRow(o, Plans.FirstOrDefault(p => p.Id == o.SubscriptionPlanId)?.Code,
            o.UserSubscriptionId is { } c ? Cycles.FirstOrDefault(x => x.Id == c) : null, buyer?.FullName, buyer?.Email, admin?.FullName);
    }

    public Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> ListAsync(string? status, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var rows = Orders.Select(Row).AsEnumerable();
        if (!string.IsNullOrEmpty(status))
            rows = rows.Where(r => r.Order.Status == status);
        if (!string.IsNullOrEmpty(q))
            rows = long.TryParse(q, out var code)
                ? rows.Where(r => r.Order.OrderCode == code)
                : rows.Where(r => r.BuyerEmail != null && r.BuyerEmail.Contains(q, StringComparison.OrdinalIgnoreCase));
        var all = rows.OrderByDescending(r => r.Order.CreatedAt).ThenByDescending(r => r.Order.OrderCode).ToList();
        return Task.FromResult<(IReadOnlyList<AdminOrderRow>, int)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<AdminOrderRow?> FindAsync(long orderCode, CancellationToken ct = default) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.OrderCode == orderCode) is { } order ? Row(order) : null);
}
