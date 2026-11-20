using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Pool và tier của một tài khoản (kế hoạch billing v2, mục "Pools and tiers"). Thứ tự trừ: Personal của mình → Gia đình → hạn mức Free.
/// Premium tính theo ngày (kỳ còn hạn, kể cả đã dùng hết token). Đọc là áp dụng chuyển kỳ đến hạn, job chạy trễ vẫn đúng.
/// </summary>
public class EntitlementService(IBillingRepository repository) : IEntitlementService
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
        return new ChatCharge(false, e.IsPremium, null, freePlan?.Code ?? PlanKinds.Free, PlanKinds.Free, PremiumIntents.FreeAllowed,
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
        return new PoolDto(e.CodeOf(cycle), cycle.PlanName, cycle.StartsAt, cycle.EndsAt, percent,
            CycleRules.IsExhausted(lane.Used, cycle.TokenQuota), CycleRules.IsLowQuota(percent), expiringSoon,
            lane.Queued is null ? null : new QueuedCycleDto(lane.Queued.PlanName, cycle.EndsAt));
    }

    private async Task<Entitlement> LoadAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var plans = await repository.GetPlansAsync(ct);
        var personal = await AdvanceLaneAsync(userId, PlanKinds.Personal, nowUtc, ct);

        // Gói Gia đình: nhóm mình làm chủ, nếu không thì nhóm mình là thành viên active (C9: tối đa một)
        var group = await repository.FindActiveOwnedGroupAsync(userId, ct);
        var isOwner = group is not null;
        group ??= await repository.FindActiveMemberGroupAsync(userId, ct);
        var family = new Lane(null, null, 0);
        if (group is not null)
        {
            family = await AdvanceLaneAsync(group.OwnerUserId, PlanKinds.Family, nowUtc, ct);
            if (family.Active is null && family.Queued is null)
            {
                await LapseAsync(group, nowUtc, ct);
                group = null;
                isOwner = false;
            }
        }
        return new Entitlement(personal, family, group, isOwner, plans);
    }

    private async Task<Lane> AdvanceLaneAsync(Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct)
    {
        var cycles = await repository.GetOpenCyclesAsync(ownerUserId, kind, ct);
        var active = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Active);
        var queued = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Queued);
        var used = active is null ? 0 : await repository.SumCycleTokensAsync(active.Id, ct);

        var changes = CycleRules.Advance(active, queued, used, nowUtc);
        if (changes.Count == 0)
            return new Lane(active, queued, used);

        await repository.SaveStepsAsync(changes.Select(c => (Action)c.Apply).ToList(), ct);
        active = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Active);
        queued = cycles.FirstOrDefault(c => c.Status == CycleStatuses.Queued);
        return new Lane(active, queued, active is null ? 0 : await repository.SumCycleTokensAsync(active.Id, ct));
    }

    // Gói Gia đình hết hiệu lực (kỳ cuối hết hạn, không có kỳ gia hạn): nhóm kết thúc, lời mời và thành viên kết thúc (C8)
    private async Task LapseAsync(FamilyGroup group, DateTime nowUtc, CancellationToken ct)
    {
        var members = await repository.GetOpenMembersAsync(group.Id, ct);
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
    }
}
