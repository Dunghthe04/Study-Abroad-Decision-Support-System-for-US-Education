using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Pool và tier của một tài khoản (kế hoạch billing v2, mục "Pools and tiers"). Thứ tự trừ: Personal của mình → Gia đình → hạn mức Free.
/// Premium tính theo ngày (kỳ còn hạn, kể cả đã dùng hết token). Đọc là áp dụng chuyển kỳ đến hạn, job chạy trễ vẫn đúng.
/// Chuyển kỳ nào xảy ra (job hay đọc) thì thông báo phát ngay sau khi lưu: người đọc đầu tiên thấy chuyển kỳ là người phát, các lần sau không còn gì để chuyển.
/// ponytail: kỳ không có xmin nên hai tiến trình chuyển cùng một kỳ đúng lúc có thể phát thông báo hai lần; khóa dòng làn nếu cần chặt hơn.
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
        var freePercent = CycleRules.UsedPercent(
            await repository.SumFreeTokensAsync(userId, CycleRules.FreeMonthStartUtc(nowUtc), ct), freePlan?.TokenQuota ?? 0);
        var free = new FreePoolDto(freePercent, CycleRules.ToVietnamTime(CycleRules.FreeResetsAtUtc(nowUtc)));

        var personal = e.Personal.Active is null ? null : Pool(e.Personal, e, nowUtc);
        FamilyPoolDto? family = null;
        if (e.Family.Active is not null && e.Group is not null)
        {
            var p = Pool(e.Family, e, nowUtc);
            var ownerName = await repository.GetUserNameAsync(e.Group.OwnerUserId, ct) ?? string.Empty;
            family = new FamilyPoolDto(p.PlanCode, p.PlanName, p.StartsAt, p.EndsAt, p.UsedPercent, p.Exhausted, p.LowQuota, p.ExpiringSoon, p.Queued,
                e.IsFamilyOwner ? FamilySeats.Owner : "member", ownerName);
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
        var resetsAt = CycleRules.FreeResetsAtUtc(nowUtc);
        if (StaffRoles.Contains(role))
            return new ChatCharge(true, true, null, null, PlanKinds.Free, null, false, resetsAt);

        var e = await LoadAsync(userId, nowUtc, ct);
        foreach (var (lane, kind) in new[] { (e.Personal, PlanKinds.Personal), (e.Family, PlanKinds.Family) })
        {
            if (lane.HasTokens)
                return new ChatCharge(false, true, lane.Active!.Id, e.CodeOf(lane.Active), kind, null, false, resetsAt);
        }

        // Không còn pool trả phí có token: hạn mức Free tháng này, chỉ hỏi kiến thức
        var freePlan = e.FreePlan;
        var freeUsed = await repository.SumFreeTokensAsync(userId, CycleRules.FreeMonthStartUtc(nowUtc), ct);
        return new ChatCharge(false, e.IsPremium, null, PlanKinds.Free, PlanKinds.Free, PremiumIntents.FreeAllowed,
            CycleRules.IsExhausted(freeUsed, freePlan?.TokenQuota ?? 0), resetsAt);
    }

    public async Task<bool> IsPremiumAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default) =>
        StaffRoles.Contains(role) || (await LoadAsync(userId, nowUtc, ct)).IsPremium;

    public async Task<IReadOnlyList<PlanDto>> GetPlansOnSaleAsync(CancellationToken ct = default) =>
        (await repository.GetPlansAsync(ct)).Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).Select(PlanDto.From).ToList();

    private static PoolDto Pool(Lane lane, Entitlement e, DateTime nowUtc)
    {
        var cycle = lane.Active!;
        var percent = CycleRules.UsedPercent(lane.Used, cycle.TokenQuota);
        // Đã có kỳ gia hạn xếp hàng thì không cảnh báo sắp hết hạn
        var expiringSoon = lane.Queued is null && CycleRules.IsExpiringSoon(cycle.EndsAt, nowUtc);
        return new PoolDto(e.CodeOf(cycle) ?? string.Empty, cycle.PlanName, cycle.StartsAt, cycle.EndsAt, percent,
            CycleRules.IsExhausted(lane.Used, cycle.TokenQuota), CycleRules.IsLowQuota(percent), expiringSoon,
            lane.Queued is null ? null : new QueuedCycleDto(lane.Queued.PlanName, cycle.EndsAt));
    }

    public async Task<LaneState> AdvanceLaneAsync(Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default)
    {
        var lane = await AdvanceCyclesAsync(ownerUserId, kind, nowUtc, ct);
        // Làn family hết kỳ: nhóm của chủ phải kết thúc trước khi ai đó (kể cả chính chủ vừa mua lại) dùng nó (C8, SW9)
        if (kind == PlanKinds.Family && lane.Active is null && lane.Queued is null
            && await repository.FindActiveOwnedGroupAsync(ownerUserId, ct) is { } dead)
            await LapseAsync(dead, nowUtc, ct);
        return new LaneState(lane.Active, lane.Queued, lane.Used, lane.Active is not null && CycleRules.IsExhausted(lane.Used, lane.Active.TokenQuota));
    }

    private async Task<Entitlement> LoadAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var plans = await repository.GetPlansAsync(ct);
        var personal = await AdvanceCyclesAsync(userId, PlanKinds.Personal, nowUtc, ct);

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

    private async Task<Lane> AdvanceCyclesAsync(Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct)
    {
        var cycles = await repository.GetOpenCyclesAsync(ownerUserId, kind, ct);
        var active = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Active);
        var queued = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Queued);
        var used = active is null ? 0 : await repository.SumCycleTokensAsync(active.Id, ct);

        var changes = CycleRules.Advance(active, queued, used, nowUtc);
        if (changes.Count == 0)
            return new Lane(active, queued, used);

        await repository.SaveStepsAsync(changes.Select(c => (Action)c.Apply).ToList(), ct);
        await NotifyTransitionsAsync(changes, ct);
        active = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Active);
        queued = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Queued);
        return new Lane(active, queued, active is null ? 0 : await repository.SumCycleTokensAsync(active.Id, ct));
    }

    // Kỳ mới bắt đầu → báo chủ kỳ; kỳ cuối của làn hết hạn mà không còn kỳ nào chạy → báo chủ kỳ. Chỉ tính trạng thái cuối của mỗi kỳ trong lượt này
    // (kỳ xếp hàng bắt đầu rồi cũng hết hạn trong cùng lượt, vì job nghỉ rất lâu, thì chỉ báo hết hạn)
    private async Task NotifyTransitionsAsync(IReadOnlyList<CycleChange> changes, CancellationToken ct)
    {
        if (notifications is null)
            return;
        var final = changes.GroupBy(c => c.Cycle.Id).Select(g => g.Last()).ToList();
        foreach (var started in final.Where(c => c.Status == CycleStatuses.Active))
            await notifications.NotifyAsync(started.Cycle.UserId, NotificationTypes.BillingCycleStarted, "Kỳ gói mới đã bắt đầu",
                $"Gói {started.Cycle.PlanName} đã bắt đầu.", "/account", ct);
        if (final.All(c => c.Status != CycleStatuses.Active)
            && final.LastOrDefault(c => c.Status == CycleStatuses.Ended && c.EndedReason == CycleEndReasons.Expired) is { } expired)
            await notifications.NotifyAsync(expired.Cycle.UserId, NotificationTypes.BillingExpired, $"Gói {expired.Cycle.PlanName} đã hết hạn",
                "Gia hạn để tiếp tục dùng các tính năng Premium.", "/plans", ct);
    }

    // Gói Gia đình hết hiệu lực (kỳ cuối hết hạn, không có kỳ gia hạn): nhóm kết thúc, lời mời và thành viên kết thúc (C8)
    private async Task LapseAsync(FamilyGroup group, DateTime nowUtc, CancellationToken ct)
    {
        var members = await repository.GetOpenMembersAsync(group.Id, ct);
        var activeMembers = members.Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId is not null).Select(m => m.UserId!.Value).ToList();
        await repository.SaveStepsAsync([() =>
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
        }], ct);

        // Báo chủ gói và mọi thành viên đang active (lời mời chưa nhận thì không có ai để báo)
        if (notifications is null)
            return;
        await notifications.NotifyAsync(group.OwnerUserId, NotificationTypes.FamilyEnded, "Gói Gia đình đã kết thúc",
            "Gói Gia đình của bạn đã kết thúc, các thành viên không còn dùng chung được nữa.", "/plans", ct);
        if (activeMembers.Count == 0)
            return;
        var ownerName = await repository.GetUserNameAsync(group.OwnerUserId, ct) ?? string.Empty;
        foreach (var memberId in activeMembers)
            await notifications.NotifyAsync(memberId, NotificationTypes.FamilyEnded, "Gói Gia đình đã kết thúc",
                $"Gói Gia đình của {ownerName} đã kết thúc.", "/plans", ct);
    }
}
