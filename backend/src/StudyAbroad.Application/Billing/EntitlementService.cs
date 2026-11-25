using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Kỳ và tier của một tài khoản. Mọi đơn đã trả tạo một kỳ active chạy ngay, nhiều kỳ cùng lúc. Kỳ dùng được = kỳ Personal của mình + kỳ Gia đình của mình
/// (chủ gói) + kỳ Gia đình của chủ nhóm mình là thành viên (kỳ Personal không bao giờ chia sẻ). Thứ tự trừ: trong các kỳ còn token ở cửa sổ hiện tại,
/// kỳ hết hạn sớm nhất (hòa: bắt đầu sớm hơn, rồi Id); không còn thì hạn mức Free. Premium tính theo ngày (có kỳ còn hạn, kể cả đã dùng hết token).
/// Đọc là kết thúc kỳ đến hạn nên job chạy trễ vẫn đúng. Thông báo hết hạn phát ngay sau khi lưu: người đọc đầu tiên thấy kỳ hết hạn là người phát.
/// Kết thúc kỳ chạy dưới khóa dòng (<see cref="IBillingRepository.SaveLockedStepsAsync"/>) và tính lại trên dòng đã khóa, nên hai request cùng đọc một kỳ đến hạn
/// không cùng kết thúc, không ném lỗi xung đột và không phát thông báo hai lần.
/// </summary>
public class EntitlementService(IBillingRepository repository, INotificationService? notifications = null) : IEntitlementService
{
    private static readonly IReadOnlySet<string> StaffRoles = new HashSet<string> { UserRoles.Admin, UserRoles.Moderator, UserRoles.Center };

    private sealed record Entitlement(
        IReadOnlyList<ActiveCycle> Personal, IReadOnlyList<ActiveCycle> Family, FamilyGroup? Group, bool IsFamilyOwner, IReadOnlyList<SubscriptionPlan> Plans)
    {
        public IReadOnlyList<ActiveCycle> Usable { get; } = Order(Personal.Concat(Family));
        public bool IsPremium => Usable.Count > 0;
        /// <summary>Kỳ bị trừ cho lượt chat kế tiếp; null = hạn mức Free.</summary>
        public ActiveCycle? Charged => Usable.FirstOrDefault(c => c.HasTokens);
        public SubscriptionPlan? FreePlan => Plans.FirstOrDefault(p => p.Kind == PlanKinds.Free);
        public string? CodeOf(UserSubscription cycle) => Plans.FirstOrDefault(p => p.Id == cycle.SubscriptionPlanId)?.Code;
    }

    /// <summary>Hết hạn sớm nhất trước; hòa thì bắt đầu sớm hơn, rồi Id.</summary>
    private static List<ActiveCycle> Order(IEnumerable<ActiveCycle> cycles) =>
        cycles.OrderBy(c => c.Cycle.EndsAt).ThenBy(c => c.Cycle.StartsAt).ThenBy(c => c.Cycle.Id).ToList();

    public async Task<SubscriptionSummaryDto> GetSummaryAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var e = await LoadAsync(userId, nowUtc, ct);
        var freePlan = e.FreePlan;
        var (freeStart, freeResetsAt) = CycleRules.FreeWindowUtc(nowUtc, freePlan?.QuotaPeriodDays);
        var freePercent = CycleRules.UsedPercent(
            await repository.SumFreeTokensAsync(userId, freeStart, ct), freePlan?.TokenQuota ?? 0);
        var free = new FreePoolDto(freePercent, CycleRules.ToVietnamTime(freeResetsAt), freePlan?.QuotaPeriodDays);

        var ownerName = e.Group is null || e.Family.Count == 0 ? string.Empty : await repository.GetUserNameAsync(e.Group.OwnerUserId, ct) ?? string.Empty;
        var cycles = e.Usable.Select(c => CycleDto(c, e, ownerName, nowUtc)).ToList();

        var personal = e.Personal.Count == 0 ? null : Pool(Order(e.Personal)[0], e, nowUtc);
        FamilyPoolDto? family = null;
        if (e.Family.Count > 0)
        {
            var p = Pool(Order(e.Family)[0], e, nowUtc);
            family = new FamilyPoolDto(p.PlanCode, p.PlanName, p.StartsAt, p.EndsAt, p.UsedPercent, p.Exhausted, p.LowQuota, p.ExpiringSoon, null,
                e.IsFamilyOwner ? FamilySeats.Owner : "member", ownerName, p.QuotaPeriodDays, p.QuotaResetsAt);
        }

        var (pool, lowQuota, expiringSoon) = e.Charged is { } charged
            ? (charged.Cycle.Kind, Pool(charged, e, nowUtc).LowQuota, CycleRules.IsExpiringSoon(charged.Cycle.EndsAt, nowUtc, charged.Cycle.DurationDays))
            : (PlanKinds.Free, CycleRules.IsLowQuota(freePercent), false);

        return new SubscriptionSummaryDto(
            e.IsPremium ? AccountTiers.Premium : AccountTiers.Free, pool, personal, family, free,
            await repository.FindPendingOrderCodeAsync(userId, ct), lowQuota, expiringSoon, cycles);
    }

    public async Task<QuotaOverviewDto> GetQuotaOverviewAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var (e, freeUsed, freeQuota) = await LoadQuotaNumbersAsync(userId, nowUtc, ct);
        // Mỗi nguồn góp tối đa bằng hạn mức của nó (dùng lố không làm âm phần còn lại của nguồn khác)
        var quota = e.Usable.Sum(c => (long)c.Cycle.TokenQuota) + freeQuota;
        var used = e.Usable.Sum(c => Math.Min(c.Used, c.Cycle.TokenQuota)) + Math.Min(freeUsed, freeQuota);
        var usedPercent = CycleRules.UsedPercent(used, quota);
        return new QuotaOverviewDto(100 - usedPercent, usedPercent, CycleRules.IsLowQuota(usedPercent));
    }

    public async Task<UserQuotaUsageDto> GetCurrentQuotaUsageAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var (e, freeUsed, freeQuota) = await LoadQuotaNumbersAsync(userId, nowUtc, ct);
        var quota = e.Usable.Sum(c => (long)c.Cycle.TokenQuota) + freeQuota;
        var used = e.Usable.Sum(c => c.Used) + freeUsed;
        return new UserQuotaUsageDto(used, quota, e.IsPremium ? AccountTiers.Premium : AccountTiers.Free);
    }

    private async Task<(Entitlement Ent, long FreeUsed, long FreeQuota)> LoadQuotaNumbersAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var e = await LoadAsync(userId, nowUtc, ct);
        var freePlan = e.FreePlan;
        var (freeStart, _) = CycleRules.FreeWindowUtc(nowUtc, freePlan?.QuotaPeriodDays);
        var freeQuota = freePlan?.TokenQuota ?? 0;
        var freeUsed = await repository.SumFreeTokensAsync(userId, freeStart, ct);
        return (e, freeUsed, freeQuota);
    }

    public async Task<IReadOnlyList<ExpiredCycleDto>> GetExpiredCyclesAsync(Guid userId, CancellationToken ct = default)
    {
        var plans = await repository.GetPlansAsync(ct);
        return (await repository.ListEndedCyclesAsync(userId, 100, ct))
            .Select(c => new ExpiredCycleDto(
                c.Id, c.Kind, plans.FirstOrDefault(p => p.Id == c.SubscriptionPlanId)?.Code ?? string.Empty, c.PlanName,
                CycleRules.ToVietnamTime(c.CreatedAt), CycleRules.ToVietnamTime(c.StartsAt!.Value), CycleRules.ToVietnamTime(c.EndsAt ?? c.EndedAt ?? c.StartsAt.Value)))
            .ToList();
    }

    public async Task<ChatCharge> ResolveChatChargeAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default)
    {
        if (StaffRoles.Contains(role))
            return new ChatCharge(true, true, null, null, PlanKinds.Free, null, false, CycleRules.FreeResetsAtUtc(nowUtc));

        var e = await LoadAsync(userId, nowUtc, ct);
        var freePlan = e.FreePlan;
        var (freeStart, resetsAt) = CycleRules.FreeWindowUtc(nowUtc, freePlan?.QuotaPeriodDays);
        if (e.Charged is { } charged)
            return new ChatCharge(false, true, charged.Cycle.Id, e.CodeOf(charged.Cycle), charged.Cycle.Kind, null, false, resetsAt);

        // Không còn kỳ trả phí có token: hạn mức Free chu kỳ này, chỉ hỏi kiến thức
        var freeUsed = await repository.SumFreeTokensAsync(userId, freeStart, ct);
        return new ChatCharge(false, e.IsPremium, null, PlanKinds.Free, PlanKinds.Free, PremiumIntents.FreeAllowed,
            CycleRules.IsExhausted(freeUsed, freePlan?.TokenQuota ?? 0), resetsAt);
    }

    public async Task<bool> IsPremiumAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default) =>
        StaffRoles.Contains(role) || (await LoadAsync(userId, nowUtc, ct)).IsPremium;

    public async Task<IReadOnlyList<PlanDto>> GetPlansOnSaleAsync(CancellationToken ct = default)
    {
        var allPlans = await repository.GetPlansAsync(ct);
        var freePlan = allPlans.FirstOrDefault(p => p.Kind == PlanKinds.Free);
        return allPlans.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).Select(p => PlanDto.From(p, freePlan)).ToList();
    }

    private static PoolDto Pool(ActiveCycle active, Entitlement e, DateTime nowUtc)
    {
        var cycle = active.Cycle;
        var percent = CycleRules.UsedPercent(active.Used, cycle.TokenQuota);
        var (_, windowEnd, isFinal) = CycleRules.CycleWindowUtc(cycle, nowUtc);
        DateTimeOffset? quotaResetsAt = !isFinal ? CycleRules.ToVietnamTime(windowEnd) : null;
        return new PoolDto(e.CodeOf(cycle) ?? string.Empty, cycle.PlanName, cycle.StartsAt, cycle.EndsAt, percent,
            active.Exhausted, CycleRules.IsLowQuota(percent), CycleRules.IsExpiringSoon(cycle.EndsAt, nowUtc, cycle.DurationDays), null,
            cycle.QuotaPeriodDays, quotaResetsAt);
    }

    private static SubscriptionCycleDto CycleDto(ActiveCycle active, Entitlement e, string ownerName, DateTime nowUtc)
    {
        var c = active.Cycle;
        var pool = Pool(active, e, nowUtc);
        var member = c.Kind == PlanKinds.Family && !e.IsFamilyOwner;
        return new SubscriptionCycleDto(
            c.Id, c.Kind, pool.PlanCode, c.PlanName,
            CycleRules.ToVietnamTime(c.CreatedAt), CycleRules.ToVietnamTime(c.StartsAt ?? c.CreatedAt), CycleRules.ToVietnamTime(c.EndsAt ?? c.CreatedAt),
            pool.UsedPercent, pool.Exhausted, pool.LowQuota, pool.ExpiringSoon, pool.QuotaPeriodDays, pool.QuotaResetsAt,
            member ? "member" : FamilySeats.Owner, member ? ownerName : null);
    }

    public async Task<IReadOnlyList<ActiveCycle>> EndExpiredCyclesAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default, ICollection<Func<CancellationToken, Task>>? deferredNotifications = null)
    {
        var cycles = await EndDueAsync(ownerUserId, kind, nowUtc, deferredNotifications, ct);
        // Gói Gia đình hết kỳ: nhóm của chủ phải kết thúc trước khi ai đó (kể cả chính chủ vừa mua lại) dùng nó (C8, SW9). Còn một kỳ active là nhóm còn sống
        if (kind == PlanKinds.Family && cycles.Count == 0 && await repository.FindActiveOwnedGroupAsync(ownerUserId, ct) is not null)
            await LapseAsync(ownerUserId, nowUtc, deferredNotifications, ct);

        var withUsage = new List<ActiveCycle>(cycles.Count);
        foreach (var cycle in cycles)
            withUsage.Add(new ActiveCycle(cycle, await repository.SumCurrentWindowTokensAsync(cycle, nowUtc, ct)));
        return withUsage;
    }

    private async Task<Entitlement> LoadAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var plans = await repository.GetPlansAsync(ct);
        var personal = await EndExpiredCyclesAsync(userId, PlanKinds.Personal, nowUtc, ct);

        // Gói Gia đình: nhóm mình làm chủ, nếu không thì nhóm mình là thành viên active (C9: tối đa một)
        var group = await repository.FindActiveOwnedGroupAsync(userId, ct);
        var isOwner = group is not null;
        group ??= await repository.FindActiveMemberGroupAsync(userId, ct);
        IReadOnlyList<ActiveCycle> family = [];
        if (group is not null)
        {
            family = await EndExpiredCyclesAsync(group.OwnerUserId, PlanKinds.Family, nowUtc, ct);   // hết kỳ thì nhóm đã bị kết thúc ở đây
            if (family.Count == 0)
            {
                group = null;
                isOwner = false;
            }
        }
        return new Entitlement(personal, family, group, isOwner, plans);
    }

    private static bool IsDue(UserSubscription cycle, DateTime nowUtc) => cycle.EndsAt is not { } end || nowUtc >= end;

    // Đọc không khóa trước: không có kỳ nào tới hạn (đa số lượt đọc) thì không mở transaction. Có thì kết thúc dưới khóa dòng, tính lại trên dòng đã khóa
    // và chỉ báo khi CHÍNH lần này đổi dữ liệu: hai request chạm cùng một kỳ đến hạn thì một bên kết thúc và báo, bên kia thấy không còn gì để kết thúc
    private async Task<List<UserSubscription>> EndDueAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, ICollection<Func<CancellationToken, Task>>? deferred, CancellationToken ct)
    {
        var cycles = await repository.GetActiveCyclesAsync(ownerUserId, kind, ct);
        if (!cycles.Any(c => IsDue(c, nowUtc)))
            return cycles;

        var ended = new List<UserSubscription>();
        var remaining = 0;
        var saved = await repository.SaveLockedStepsAsync(ownerUserId, kind, lockGroup: false, async () =>
        {
            var fresh = await repository.GetActiveCyclesAsync(ownerUserId, kind, ct);
            var due = fresh.Where(c => IsDue(c, nowUtc)).ToList();
            ended.Clear();
            ended.AddRange(due);
            remaining = fresh.Count - due.Count;
            return due.Count == 0 ? [] : [() =>
            {
                foreach (var c in due)
                {
                    c.Status = CycleStatuses.Ended;
                    c.EndedReason = CycleEndReasons.Expired;
                    c.EndedAt = c.EndsAt ?? nowUtc;
                }
            }];
        }, ct);
        // Báo hết hạn khi kỳ cuối của chủ ở loại này vừa hết (còn kỳ khác thì người dùng vẫn đang Premium, không cần báo)
        if (saved && remaining == 0 && ended.OrderBy(c => c.EndsAt).Last() is { } last)
        {
            await NotifyAsync(deferred, last.UserId, NotificationTypes.BillingExpired, $"Gói {last.PlanName} đã hết hạn",
                "Gia hạn để tiếp tục dùng các tính năng Premium.", "/plans", ct);
            // [UC-128] Gói Gia đình hết hạn: thông báo cho cả chủ gói và các thành viên đang hoạt động
            if (last.Kind == PlanKinds.Family && last.FamilyGroupId is { } groupId)
            {
                var activeMemberIds = (await repository.GetOpenMembersAsync(groupId, ct))
                    .Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId is { } uid && uid != last.UserId)
                    .Select(m => m.UserId!.Value)
                    .Distinct()
                    .ToList();
                if (activeMemberIds.Count > 0)
                {
                    var ownerName = await repository.GetUserNameAsync(last.UserId, ct) ?? string.Empty;
                    foreach (var memberId in activeMemberIds)
                        await NotifyAsync(deferred, memberId, NotificationTypes.BillingExpired, "Gói Gia đình đã hết hạn",
                            $"Gói {last.PlanName} của {ownerName} đã hết hạn.", "/plans", ct);
                }
            }
        }
        return (await repository.GetActiveCyclesAsync(ownerUserId, kind, ct)).Where(c => !IsDue(c, nowUtc)).ToList();   // thực thể mới nạp (đã theo dõi), người gọi sửa tiếp được
    }

    // Gói Gia đình hết hiệu lực (kỳ Gia đình cuối cùng của chủ đã hết hạn): nhóm kết thúc, lời mời và thành viên kết thúc (C8).
    // Khóa các kỳ và nhóm rồi kiểm lại: nhóm vẫn active và chủ vẫn không có kỳ Gia đình nào (thanh toán chen vào thì không kết thúc nữa)
    private async Task LapseAsync(Guid ownerUserId, DateTime nowUtc, ICollection<Func<CancellationToken, Task>>? deferred, CancellationToken ct)
    {
        var activeMembers = new List<Guid>();
        var saved = await repository.SaveLockedStepsAsync(ownerUserId, PlanKinds.Family, lockGroup: true, async () =>
        {
            if ((await repository.GetActiveCyclesAsync(ownerUserId, PlanKinds.Family, ct)).Count > 0
                || await repository.FindActiveOwnedGroupAsync(ownerUserId, ct) is not { } group)
                return [];
            var members = await repository.GetOpenMembersAsync(group.Id, ct);
            activeMembers.AddRange(members.Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId is not null).Select(m => m.UserId!.Value));
            return [() =>
            {
                group.Status = FamilyGroupStatuses.Ended;
                group.EndedAt = nowUtc;
                group.EndedReason = CycleEndReasons.FamilyLapsed;
                foreach (var m in members)
                {
                    m.Status = FamilyMemberStatuses.Ended;
                    m.EndedAt = nowUtc;
                    m.EndedReason = FamilyMemberEndReasons.FamilyLapsed;
                    m.TokenHash = null;   // link mời không dùng được nữa
                }
            }];
        }, ct);
        if (!saved)
            return;

        // Báo chủ gói và mọi thành viên đang active (lời mời chưa nhận thì không có ai để báo)
        await NotifyAsync(deferred, ownerUserId, NotificationTypes.FamilyEnded, "Gói Gia đình đã kết thúc",
            "Gói Gia đình của bạn đã kết thúc, các thành viên không còn dùng chung được nữa.", "/plans", ct);
        if (activeMembers.Count == 0)
            return;
        var ownerName = await repository.GetUserNameAsync(ownerUserId, ct) ?? string.Empty;
        foreach (var memberId in activeMembers)
            await NotifyAsync(deferred, memberId, NotificationTypes.FamilyEnded, "Gói Gia đình đã kết thúc",
                $"Gói Gia đình của {ownerName} đã kết thúc.", "/plans", ct);
    }

    // deferred != null (đang trong transaction thanh toán): xếp hàng để người gọi gửi SAU commit, chỉ cho lần thử đã commit; không thì gửi ngay (đường đọc, đã lưu xong).
    // Hàng đợi chỉ giữ giá trị thường (id, chữ), không giữ thực thể: theo dõi bị xóa khi thử lại
    private Task NotifyAsync(ICollection<Func<CancellationToken, Task>>? deferred, Guid userId, string type, string title, string body, string link, CancellationToken ct)
    {
        if (notifications is not { } service)
            return Task.CompletedTask;
        if (deferred is null)
            return service.NotifyAsync(userId, type, title, body, link, ct);
        deferred.Add(c => service.NotifyAsync(userId, type, title, body, link, c));
        return Task.CompletedTask;
    }
}
