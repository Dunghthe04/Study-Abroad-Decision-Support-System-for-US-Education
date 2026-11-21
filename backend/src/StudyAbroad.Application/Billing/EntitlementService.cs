using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Pool và tier của một tài khoản (kế hoạch billing v2, mục "Pools and tiers"). Thứ tự trừ: Personal của mình → Gia đình → hạn mức Free.
/// Premium tính theo ngày (kỳ còn hạn, kể cả đã dùng hết token). Đọc là áp dụng chuyển kỳ đến hạn, job chạy trễ vẫn đúng.
/// Chuyển kỳ nào xảy ra (job hay đọc) thì thông báo phát ngay sau khi lưu: người đọc đầu tiên thấy chuyển kỳ là người phát, các lần sau không còn gì để chuyển.
/// Chuyển kỳ chạy dưới khóa dòng làn (<see cref="IBillingRepository.SaveLockedStepsAsync"/>) và tính lại trên dòng đã khóa, nên hai request cùng đọc một làn đến hạn
/// không cùng chuyển, không ném lỗi xung đột và không phát thông báo hai lần.
/// </summary>
public class EntitlementService(IBillingRepository repository, INotificationService? notifications = null) : IEntitlementService
{
    private static readonly IReadOnlySet<string> StaffRoles = new HashSet<string> { UserRoles.Admin, UserRoles.Moderator, UserRoles.Center };

    /// <summary>Làn sau khi chuyển kỳ: kỳ active (còn hạn), kỳ queued, token đã dùng của kỳ active.</summary>
    private sealed record Lane(UserSubscription? Active, UserSubscription? Queued, long Used)
    {
        public bool HasTokens => Active is not null && !CycleRules.IsExhausted(Used, Active.TokenQuota);
    }

    private sealed record Entitlement(
        Lane Personal, Lane Family, FamilyGroup? Group, bool IsFamilyOwner, IReadOnlyList<SubscriptionPlan> Plans)
    {
        public bool IsPremium => Personal.Active is not null || Family.Active is not null;
        public SubscriptionPlan? FreePlan => Plans.FirstOrDefault(p => p.Kind == PlanKinds.Free);
        public string? CodeOf(UserSubscription cycle) => Plans.FirstOrDefault(p => p.Id == cycle.SubscriptionPlanId)?.Code;
    }

    public async Task<SubscriptionSummaryDto> GetSummaryAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var e = await LoadAsync(userId, nowUtc, ct);
        var freePlan = e.FreePlan;
        var (freeStart, freeResetsAt) = CycleRules.FreeWindowUtc(nowUtc, freePlan?.QuotaPeriodDays);
        var freePercent = CycleRules.UsedPercent(
            await repository.SumFreeTokensAsync(userId, freeStart, ct), freePlan?.TokenQuota ?? 0);
        var free = new FreePoolDto(freePercent, CycleRules.ToVietnamTime(freeResetsAt), freePlan?.QuotaPeriodDays);

        var personal = e.Personal.Active is null ? null : Pool(e.Personal, e, nowUtc);
        FamilyPoolDto? family = null;
        if (e.Family.Active is not null && e.Group is not null)
        {
            var p = Pool(e.Family, e, nowUtc);
            var ownerName = await repository.GetUserNameAsync(e.Group.OwnerUserId, ct) ?? string.Empty;
            family = new FamilyPoolDto(p.PlanCode, p.PlanName, p.StartsAt, p.EndsAt, p.UsedPercent, p.Exhausted, p.LowQuota, p.ExpiringSoon, p.Queued,
                e.IsFamilyOwner ? FamilySeats.Owner : "member", ownerName, p.QuotaPeriodDays, p.QuotaResetsAt);
        }

        var (pool, lowQuota, expiringSoon) =
            e.Personal.HasTokens ? (PlanKinds.Personal, personal!.LowQuota, personal.ExpiringSoon)
            : e.Family.HasTokens ? (PlanKinds.Family, family?.LowQuota ?? false, family?.ExpiringSoon ?? false)
            : (PlanKinds.Free, CycleRules.IsLowQuota(freePercent), false);

        return new SubscriptionSummaryDto(
            e.IsPremium ? AccountTiers.Premium : AccountTiers.Free, pool, personal, family, free,
            await repository.FindPendingOrderCodeAsync(userId, ct), lowQuota, expiringSoon);
    }

    public async Task<ChatCharge> ResolveChatChargeAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default)
    {
        if (StaffRoles.Contains(role))
            return new ChatCharge(true, true, null, null, PlanKinds.Free, null, false, CycleRules.FreeResetsAtUtc(nowUtc));

        var e = await LoadAsync(userId, nowUtc, ct);
        var freePlan = e.FreePlan;
        var (freeStart, resetsAt) = CycleRules.FreeWindowUtc(nowUtc, freePlan?.QuotaPeriodDays);
        foreach (var (lane, kind) in new[] { (e.Personal, PlanKinds.Personal), (e.Family, PlanKinds.Family) })
        {
            if (lane.HasTokens)
                return new ChatCharge(false, true, lane.Active!.Id, e.CodeOf(lane.Active), kind, null, false, resetsAt);
        }

        // Không còn pool trả phí có token: hạn mức Free chu kỳ này, chỉ hỏi kiến thức
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

    private static PoolDto Pool(Lane lane, Entitlement e, DateTime nowUtc)
    {
        var cycle = lane.Active!;
        var percent = CycleRules.UsedPercent(lane.Used, cycle.TokenQuota);
        // Đã có kỳ gia hạn xếp hàng thì không cảnh báo sắp hết hạn
        var expiringSoon = lane.Queued is null && CycleRules.IsExpiringSoon(cycle.EndsAt, nowUtc);
        var (_, windowEnd, isFinal) = CycleRules.CycleWindowUtc(cycle, nowUtc);
        DateTimeOffset? quotaResetsAt = !isFinal ? CycleRules.ToVietnamTime(windowEnd) : null;
        return new PoolDto(e.CodeOf(cycle) ?? string.Empty, cycle.PlanName, cycle.StartsAt, cycle.EndsAt, percent,
            CycleRules.IsExhausted(lane.Used, cycle.TokenQuota), CycleRules.IsLowQuota(percent), expiringSoon,
            lane.Queued is null ? null : new QueuedCycleDto(lane.Queued.PlanName, cycle.EndsAt),
            cycle.QuotaPeriodDays, quotaResetsAt);
    }

    public async Task<LaneState> AdvanceLaneAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default, ICollection<Func<CancellationToken, Task>>? deferredNotifications = null)
    {
        var lane = await AdvanceCyclesAsync(ownerUserId, kind, nowUtc, deferredNotifications, ct);
        // Làn family hết kỳ: nhóm của chủ phải kết thúc trước khi ai đó (kể cả chính chủ vừa mua lại) dùng nó (C8, SW9)
        if (kind == PlanKinds.Family && lane.Active is null && lane.Queued is null
            && await repository.FindActiveOwnedGroupAsync(ownerUserId, ct) is not null)
            await LapseAsync(ownerUserId, nowUtc, deferredNotifications, ct);
        return new LaneState(lane.Active, lane.Queued, lane.Used,
            lane.Active is not null && CycleRules.CycleWindowUtc(lane.Active, nowUtc).IsFinalWindow && CycleRules.IsExhausted(lane.Used, lane.Active.TokenQuota));
    }

    private async Task<Entitlement> LoadAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var plans = await repository.GetPlansAsync(ct);
        var personal = await AdvanceCyclesAsync(userId, PlanKinds.Personal, nowUtc, null, ct);

        // Gói Gia đình: nhóm mình làm chủ, nếu không thì nhóm mình là thành viên active (C9: tối đa một)
        var group = await repository.FindActiveOwnedGroupAsync(userId, ct);
        var isOwner = group is not null;
        group ??= await repository.FindActiveMemberGroupAsync(userId, ct);
        var family = new Lane(null, null, 0);
        if (group is not null)
        {
            var state = await AdvanceLaneAsync(group.OwnerUserId, PlanKinds.Family, nowUtc, ct);   // hết kỳ thì nhóm đã bị kết thúc ở đây
            family = new Lane(state.Active, state.Queued, state.ActiveUsed);
            if (family.Active is null && family.Queued is null)
            {
                group = null;
                isOwner = false;
            }
        }
        return new Entitlement(personal, family, group, isOwner, plans);
    }

    private async Task<Lane> ReadLaneAsync(Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct)
    {
        var cycles = await repository.GetOpenCyclesAsync(ownerUserId, kind, ct);
        var active = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Active);
        var queued = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Queued);
        long used = 0;
        if (active is not null)
        {
            if (active.QuotaPeriodDays is > 0 && active.QuotaPeriodDays.Value < active.DurationDays)
            {
                var (windowStart, windowEnd, _) = CycleRules.CycleWindowUtc(active, nowUtc);
                used = await repository.SumCycleTokensAsync(active.Id, windowStart, windowEnd, ct);
            }
            else
            {
                used = await repository.SumCycleTokensAsync(active.Id, null, null, ct);
            }
        }
        return new Lane(active, queued, used);
    }

    // Đọc không khóa trước: không có gì phải chuyển (đa số lượt đọc) thì không mở transaction. Có thì chuyển dưới khóa dòng làn, tính lại trên dòng đã khóa
    // và chỉ báo khi CHÍNH lần này đổi dữ liệu: hai request chạm cùng một làn đến hạn thì một bên chuyển và báo, bên kia thấy không còn gì để chuyển
    private async Task<Lane> AdvanceCyclesAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, ICollection<Func<CancellationToken, Task>>? deferred, CancellationToken ct)
    {
        var lane = await ReadLaneAsync(ownerUserId, kind, nowUtc, ct);
        if (CycleRules.Advance(lane.Active, lane.Queued, lane.Used, nowUtc).Count == 0)
            return lane;

        IReadOnlyList<CycleChange> applied = [];
        var saved = await repository.SaveLockedStepsAsync(ownerUserId, kind, lockGroup: false, async () =>
        {
            var fresh = await ReadLaneAsync(ownerUserId, kind, nowUtc, ct);
            applied = CycleRules.Advance(fresh.Active, fresh.Queued, fresh.Used, nowUtc);
            return applied.Select(c => (Action)c.Apply).ToList();
        }, ct);
        if (saved)
            await NotifyTransitionsAsync(applied, deferred, ct);
        return await ReadLaneAsync(ownerUserId, kind, nowUtc, ct);   // thực thể mới nạp (đã theo dõi), người gọi sửa tiếp được
    }

    // Kỳ mới bắt đầu → báo chủ kỳ; kỳ cuối của làn hết hạn mà không còn kỳ nào chạy → báo chủ kỳ. Chỉ tính trạng thái cuối của mỗi kỳ trong lượt này
    // (kỳ xếp hàng bắt đầu rồi cũng hết hạn trong cùng lượt, vì job nghỉ rất lâu, thì chỉ báo hết hạn)
    private async Task NotifyTransitionsAsync(IReadOnlyList<CycleChange> changes, ICollection<Func<CancellationToken, Task>>? deferred, CancellationToken ct)
    {
        var final = changes.GroupBy(c => c.Cycle.Id).Select(g => g.Last()).ToList();
        foreach (var started in final.Where(c => c.Status == CycleStatuses.Active))
            await NotifyAsync(deferred, started.Cycle.UserId, NotificationTypes.BillingCycleStarted, "Kỳ gói mới đã bắt đầu",
                $"Gói {started.Cycle.PlanName} đã bắt đầu.", "/account", ct);
        if (final.All(c => c.Status != CycleStatuses.Active)
            && final.LastOrDefault(c => c.Status == CycleStatuses.Ended && c.EndedReason == CycleEndReasons.Expired) is { } expired)
            await NotifyAsync(deferred, expired.Cycle.UserId, NotificationTypes.BillingExpired, $"Gói {expired.Cycle.PlanName} đã hết hạn",
                "Gia hạn để tiếp tục dùng các tính năng Premium.", "/plans", ct);
    }

    // Gói Gia đình hết hiệu lực (kỳ cuối hết hạn, không có kỳ gia hạn): nhóm kết thúc, lời mời và thành viên kết thúc (C8).
    // Khóa làn và nhóm rồi kiểm lại: nhóm vẫn active và làn vẫn không có kỳ nào (thanh toán chen vào thì không kết thúc nữa)
    private async Task LapseAsync(Guid ownerUserId, DateTime nowUtc, ICollection<Func<CancellationToken, Task>>? deferred, CancellationToken ct)
    {
        var activeMembers = new List<Guid>();
        var saved = await repository.SaveLockedStepsAsync(ownerUserId, PlanKinds.Family, lockGroup: true, async () =>
        {
            if ((await repository.GetOpenCyclesAsync(ownerUserId, PlanKinds.Family, ct)).Count > 0
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
