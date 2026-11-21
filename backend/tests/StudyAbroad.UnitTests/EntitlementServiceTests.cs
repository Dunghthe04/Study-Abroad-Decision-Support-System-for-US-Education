using System.Reflection;
using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Quyền dùng gói: tier theo ngày, thứ tự trừ theo kỳ hết hạn sớm nhất (chỉ kỳ còn token) rồi Free, kết thúc kỳ hết hạn khi đọc, phần trăm.
public class EntitlementServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    // ---- thứ tự trừ

    [Fact]
    public async Task FreeAccount_ChargesTheFreeAllowance_KnowledgeOnly()
    {
        var repo = new FakeBilling();

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal((false, false, (Guid?)null, "free", PlanKinds.Free), (charge.Ungated, charge.IsPremium, charge.CycleId, charge.PlanCode, charge.PoolKind));
        Assert.Equal(["knowledge", "out_of_scope"], charge.AllowedIntents!);
        Assert.False(charge.FreeExhausted);
        Assert.Equal(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc), charge.FreeResetsAtUtc);
    }

    [Fact]
    public async Task PersonalWithTokens_IsChargedWithEverythingAllowed()
    {
        var repo = new FakeBilling();
        var personal = repo.AddActive(Me, "personal-1m");

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Parent, Now);

        Assert.Equal((true, (Guid?)personal.Id, "personal-1m", PlanKinds.Personal), (charge.IsPremium, charge.CycleId, charge.PlanCode, charge.PoolKind));
        Assert.Null(charge.AllowedIntents);
    }

    [Fact]
    public async Task F18_MemberWithOwnPersonal_TheEarliestEndingCycleIsChargedFirst()
    {
        var repo = new FakeBilling();
        var family = repo.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-20));   // hết hạn sau 10 ngày
        var personal = repo.AddActive(Me, "personal-1m");                              // hết hạn sau 29 ngày

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(family.Id, charge.CycleId);
        Assert.Equal(PlanKinds.Family, charge.PoolKind);

        personal.StartsAt = Now.AddDays(-29);   // personal hết hạn sau 1 ngày: tới lượt personal
        personal.EndsAt = Now.AddDays(1);
        Assert.Equal(personal.Id, (await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now)).CycleId);
    }

    [Fact]
    public async Task B_ChargePicksTheEarliestEndingCycleWithTokens_AndSkipsExhaustedOnes()
    {
        var repo = new FakeBilling();
        var soonest = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-25));    // hết hạn sau 5 ngày
        var middle = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-10));     // sau 20 ngày
        var latest = repo.AddActive(Me, "personal-3m", startsAt: Now.AddDays(-1));      // sau 89 ngày
        var service = Service(repo);

        Assert.Equal(soonest.Id, (await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now)).CycleId);

        repo.Use(Me, soonest, 500_000);   // hết token: bỏ qua nhưng vẫn active
        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now);
        Assert.Equal((middle.Id, PlanKinds.Personal), (charge.CycleId!.Value, charge.PoolKind));

        repo.Use(Me, middle, 500_000);
        Assert.Equal(latest.Id, (await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now)).CycleId);

        repo.Use(Me, latest, 600_000);
        var free = await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now);
        Assert.Equal((true, (Guid?)null, PlanKinds.Free), (free.IsPremium, free.CycleId, free.PoolKind));
        Assert.Equal(CycleStatuses.Active, soonest.Status);
    }

    [Fact]
    public async Task B_EqualEndDates_TieBreakByEarlierStart_ThenId()
    {
        var repo = new FakeBilling();
        var shorterPlanSameEnd = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-10));   // kết thúc Now+20, bắt đầu muộn
        var longerPlanSameEnd = repo.AddActive(Me, "personal-3m", startsAt: Now.AddDays(-70));    // kết thúc Now+20, bắt đầu sớm
        Assert.Equal(shorterPlanSameEnd.EndsAt, longerPlanSameEnd.EndsAt);

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(longerPlanSameEnd.Id, charge.CycleId);
    }

    [Fact]
    public async Task F19_OwnerWithPersonalAndFamily_ChargesTheEarlierEnding_FamilyStillShownAsOwner()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Me, startsAt: Now.AddDays(-5));
        var personal = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-20));

        var service = Service(repo);
        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Parent, Now);
        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(personal.Id, charge.CycleId);
        Assert.Equal((PlanKinds.Personal, "owner"), (summary.ChargingPool, summary.Family!.Role));
        Assert.Equal(["owner", "owner"], summary.Cycles.Select(c => c.Role));
    }

    [Fact]
    public async Task C_MemberIsChargedAgainstBothOfTheOwnersFamilyCycles_AndSummaryListsThemAsMember()
    {
        var repo = new FakeBilling();
        var first = repo.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-20));        // hết hạn sau 10 ngày
        var second = repo.AddFamilyCycle(Owner, "family-3m", startsAt: Now.AddDays(-1));   // hết hạn sau 89 ngày
        var service = Service(repo);

        Assert.Equal(first.Id, (await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now)).CycleId);
        repo.Use(Me, first, 500_000);   // cạn kỳ đầu: chuyển sang kỳ thứ hai của chủ gói
        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now);
        Assert.Equal((second.Id, "family-3m", PlanKinds.Family), (charge.CycleId!.Value, charge.PlanCode, charge.PoolKind));

        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(AccountTiers.Premium, summary.Tier);
        Assert.Equal([first.Id, second.Id], summary.Cycles.Select(c => c.Id));
        Assert.All(summary.Cycles, c => Assert.Equal((PlanKinds.Family, "member", "Chủ gói"), (c.Kind, c.Role, c.OwnerName)));
        Assert.Equal((100, true), (summary.Cycles[0].UsedPercent, summary.Cycles[0].Exhausted));
        Assert.Equal((0, false), (summary.Cycles[1].UsedPercent, summary.Cycles[1].Exhausted));
        Assert.Equal(("member", "Chủ gói"), (summary.Family!.Role, summary.Family.OwnerName));
        Assert.Equal(PlanKinds.Family, summary.ChargingPool);
        Assert.Null(summary.Personal);
    }

    [Fact]
    public async Task D_OwnersPersonalCycleIsNeitherVisibleNorUsableByMembers()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Owner, member: Me);
        var ownersPersonal = repo.AddActive(Owner, "personal-3m", startsAt: Now.AddDays(-80));   // hết hạn sớm hơn kỳ Gia đình: nếu chia sẻ thì sẽ bị chọn
        var service = Service(repo);

        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now);
        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.NotEqual(ownersPersonal.Id, charge.CycleId);
        Assert.Equal(PlanKinds.Family, charge.PoolKind);
        Assert.DoesNotContain(summary.Cycles, c => c.Id == ownersPersonal.Id);
        Assert.Null(summary.Personal);
        Assert.Single(summary.Cycles);

        var ownerSummary = await service.GetSummaryAsync(Owner, Now);   // chủ gói thấy cả hai kỳ của mình
        Assert.Equal(2, ownerSummary.Cycles.Count);
        Assert.Equal(ownersPersonal.Id, ownerSummary.Cycles[0].Id);
    }

    [Fact]
    public async Task A_SummaryCycles_AreSortedByEndDate_WithPurchaseTimeAndExpiringSoon()
    {
        var repo = new FakeBilling();
        var later = repo.AddActive(Me, "personal-3m", startsAt: Now.AddDays(-1));
        var sooner = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-25));
        sooner.CreatedAt = Now.AddDays(-25);
        repo.Use(Me, sooner, 410_000);

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal([sooner.Id, later.Id], summary.Cycles.Select(c => c.Id));
        var dto = summary.Cycles[0];
        Assert.Equal((PlanKinds.Personal, "personal-1m", "Cá nhân 1 tháng", "owner", (string?)null), (dto.Kind, dto.PlanCode, dto.PlanName, dto.Role, dto.OwnerName));
        Assert.Equal((CycleRules.ToVietnamTime(sooner.CreatedAt), CycleRules.ToVietnamTime(sooner.StartsAt!.Value), CycleRules.ToVietnamTime(sooner.EndsAt!.Value)),
            (dto.PurchasedAt, dto.StartsAt, dto.EndsAt));
        Assert.Equal((82, false, true, true, true), (dto.UsedPercent, dto.Exhausted, dto.LowQuota, dto.ExpiringSoon, dto.QuotaResetsAt is null));
        Assert.False(summary.Cycles[1].ExpiringSoon);
        Assert.Equal(sooner.PlanName, summary.Personal!.PlanName);   // Personal = kỳ hết hạn sớm nhất
        Assert.Equal((PlanKinds.Personal, true, true), (summary.ChargingPool, summary.LowQuota, summary.ExpiringSoon));
    }

    [Fact]
    public async Task F20_FamilyPoolExhausted_MemberFallsToFreeKnowledgeButStaysPremium()
    {
        var repo = new FakeBilling();
        var family = repo.AddFamily(Owner, member: Me);
        repo.Use(Owner, family, 300_000);
        repo.Use(Me, family, 200_000);

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal((true, (Guid?)null, "free", PlanKinds.Free), (charge.IsPremium, charge.CycleId, charge.PlanCode, charge.PoolKind));
        Assert.Equal(PremiumIntents.FreeAllowed, charge.AllowedIntents);
        Assert.False(charge.FreeExhausted);
    }

    [Fact]
    public async Task FreeAllowanceUsedUp_IsReportedAsFreeExhausted()
    {
        var repo = new FakeBilling();
        repo.UseFree(Me, 120_000, Now.AddDays(-1));

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.True(charge.FreeExhausted);
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Center)]
    public async Task Staff_AreUngatedAndNeverCharged(string role)
    {
        var repo = new FakeBilling();
        var service = Service(repo);

        var charge = await service.ResolveChatChargeAsync(Me, role, Now);

        Assert.True(charge.Ungated);
        Assert.Equal(((Guid?)null, (string?)null, (IReadOnlyList<string>?)null), (charge.CycleId, charge.PlanCode, charge.AllowedIntents));
        Assert.True(await service.IsPremiumAsync(Me, role, Now));
        Assert.Equal(0, repo.Reads);   // không chạm DB
    }

    // ---- kết thúc kỳ khi đọc

    [Fact]
    public async Task R2_ChatRightAfterExpiry_EndsTheOldCycleAtItsEndDateWithoutTheJob_AndChargesTheOtherOne()
    {
        var repo = new FakeBilling();
        var old = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-30).AddMinutes(-1));
        var running = repo.AddActive(Me, "personal-3m");

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(running.Id, charge.CycleId);
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired, old.EndsAt), (old.Status, old.EndedReason, old.EndedAt));
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task R3_ExhaustedCycle_StaysActiveAndPremiumUntilItsEndDate_ThenFree()
    {
        var repo = new FakeBilling();
        var cycle = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, cycle, 500_000);
        var service = Service(repo);

        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal((CycleStatuses.Active, AccountTiers.Premium, PlanKinds.Free), (cycle.Status, summary.Tier, summary.ChargingPool));
        Assert.Equal((100, true), (summary.Personal!.UsedPercent, summary.Personal.Exhausted));
        Assert.Equal(0, repo.Saves);

        Assert.Equal(AccountTiers.Free, (await service.GetSummaryAsync(Me, cycle.EndsAt!.Value)).Tier);
        Assert.Equal(CycleStatuses.Ended, cycle.Status);
    }

    [Fact]
    public async Task R7_ExpiredWithoutRenewal_BackToFree()
    {
        var repo = new FakeBilling();
        var old = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-31));

        var service = Service(repo);
        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(CycleStatuses.Ended, old.Status);
        Assert.Equal((AccountTiers.Free, PlanKinds.Free), (summary.Tier, summary.ChargingPool));
        Assert.Null(summary.Personal);
        Assert.False(await service.IsPremiumAsync(Me, UserRoles.Student, Now));
    }

    [Fact]
    public async Task FamilyLapse_EndsTheGroupItsMembersAndPendingInvites()
    {
        var repo = new FakeBilling();
        var cycle = repo.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var group = repo.Groups.Single();
        var invite = new FamilyMember
        {
            FamilyGroupId = group.Id, InvitedEmail = "x@y.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "abc",
        };
        repo.Members.Add(invite);

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(CycleStatuses.Ended, cycle.Status);
        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.FamilyLapsed), (group.Status, group.EndedReason));
        Assert.All(repo.Members, m => Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.FamilyLapsed), (m.Status, m.EndedReason)));
        Assert.Null(invite.TokenHash);
        Assert.False(charge.IsPremium);
    }

    [Fact]
    public async Task FamilyCycleExpired_WithAnotherStillRunning_MembersKeepTheGroupAndTheRemainingCycle()
    {
        var repo = new FakeBilling();
        var old = repo.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var running = repo.AddFamilyCycle(Owner, "family-3m", startsAt: Now.AddDays(-2));

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal(FamilyGroupStatuses.Active, repo.Groups.Single().Status);
        Assert.Equal(("family-3m", "member", "Chủ gói"), (summary.Family!.PlanCode, summary.Family.Role, summary.Family.OwnerName));
        Assert.Equal((CycleStatuses.Ended, CycleStatuses.Active), (old.Status, running.Status));
        Assert.Equal([running.Id], summary.Cycles.Select(c => c.Id));
    }

    // ---- tóm tắt và phần trăm

    [Fact]
    public async Task T2_FreeUsageBeforeVietnamMidnightIsLastMonth()
    {
        var repo = new FakeBilling();
        repo.UseFree(Me, 60_000, new DateTime(2026, 11, 30, 16, 59, 0, DateTimeKind.Utc));
        var service = Service(repo);

        var before = await service.GetSummaryAsync(Me, new DateTime(2026, 11, 30, 16, 59, 30, DateTimeKind.Utc));
        var after = await service.GetSummaryAsync(Me, new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc));

        Assert.Equal(50, before.Free.UsedPercent);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(7)), before.Free.ResetsAt);
        Assert.Equal(0, after.Free.UsedPercent);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(7)), after.Free.ResetsAt);
    }

    [Fact]
    public async Task T3_HeavyPaidUse_DoesNotTouchTheFreeAllowance()
    {
        var repo = new FakeBilling();
        var personal = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, personal, 400_000);
        repo.UseFree(Me, 12_000, Now.AddHours(-1));

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal((80, 10), (summary.Personal!.UsedPercent, summary.Free.UsedPercent));
    }

    [Fact]
    public async Task OnlyClaudeCallsOfChatTurnsAreCounted()
    {
        var repo = new FakeBilling();
        var personal = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, personal, 100_000);
        repo.Calls.Add(Call(Me, 300_000, "ollama", personal.Id, "personal-1m"));        // SLM tự host: miễn phí
        repo.Calls.Add(Call(Me, 300_000, "embedding", null, "free"));                     // embedding: miễn phí
        repo.Calls.Add(Call(Me, 300_000, "claude", null, null));                          // OCR, ngoại khóa: không đóng dấu gói
        repo.Calls.Add(Call(Me, 300_000, "claude", null, "family-1m"));                   // kỳ đã bị xóa (FK set null): không sang Free

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal((20, 0), (summary.Personal!.UsedPercent, summary.Free.UsedPercent));
    }

    [Fact]
    public async Task T10_QuotaZero_PremiumByDateShows100AndChatFallsToFree()
    {
        var repo = new FakeBilling();
        repo.AddActive(Me, "personal-1m", quota: 0);

        var service = Service(repo);
        var summary = await service.GetSummaryAsync(Me, Now);
        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal((AccountTiers.Premium, 100, true), (summary.Tier, summary.Personal!.UsedPercent, summary.Personal.Exhausted));
        Assert.Equal((PlanKinds.Free, true), (charge.PoolKind, charge.IsPremium));
    }

    [Fact]
    public async Task T11_Overshoot_Shows100()
    {
        var repo = new FakeBilling();
        var personal = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, personal, 650_000);

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal(100, summary.Personal!.UsedPercent);
    }

    [Fact]
    public async Task Warnings_AreForTheChargedCycle()
    {
        var repo = new FakeBilling();
        var soon = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-25));
        var far = repo.AddActive(Me, "personal-3m", startsAt: Now.AddDays(-1));
        repo.Use(Me, soon, 410_000);
        var service = Service(repo);

        var warned = await service.GetSummaryAsync(Me, Now);
        repo.Use(Me, soon, 90_000);   // cạn kỳ sắp hết: cảnh báo chuyển sang kỳ dài còn rất nhiều token
        var next = await service.GetSummaryAsync(Me, Now);

        Assert.Equal((true, true), (warned.LowQuota, warned.ExpiringSoon));
        Assert.Equal((false, false, PlanKinds.Personal), (next.LowQuota, next.ExpiringSoon, next.ChargingPool));
        Assert.Null(next.Personal!.Queued);
        Assert.Equal(soon.PlanName, next.Personal.PlanName);   // Personal vẫn là kỳ hết hạn sớm nhất
        Assert.Equal(far.PlanName, next.Cycles[1].PlanName);
    }

    [Fact]
    public async Task PendingOrderCode_IsReturned()
    {
        var repo = new FakeBilling();
        repo.Orders.Add(new PaymentTransaction { UserId = Me, OrderCode = 1795000000000, Status = OrderStatuses.Pending });
        repo.Orders.Add(new PaymentTransaction { UserId = Me, OrderCode = 1794000000000, Status = OrderStatuses.Paid });

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal(1795000000000, summary.PendingOrderCode);
    }

    [Fact]
    public async Task PlansOnSale_OnlyActiveInDisplayOrder_FamilyHasSeats()
    {
        var repo = new FakeBilling();
        repo.Plans.Single(p => p.Code == "personal-3m").IsActive = false;

        var plans = await Service(repo).GetPlansOnSaleAsync();

        Assert.Equal(["free", "personal-1m", "family-1m", "family-3m"], plans.Select(p => p.Code));
        Assert.Equal(new PlanSeatsDto(2, 2), plans.Single(p => p.Code == "family-1m").Seats);
        Assert.Null(plans.Single(p => p.Code == "personal-1m").Seats);
        Assert.Equal(79_000L, plans.Single(p => p.Code == "personal-1m").PriceVnd);
        Assert.Null(plans.Single(p => p.Code == "free").QuotaMultiplier);
        Assert.Equal(4.2m, plans.Single(p => p.Code == "personal-1m").QuotaMultiplier);
        Assert.Equal((30, 5.0m), (plans.Single(p => p.Code == "family-3m").QuotaPeriodDays, plans.Single(p => p.Code == "family-3m").QuotaMultiplier));
    }

    [Fact]
    public async Task RecurringCycle_WindowResetsFresh_AndIsNotEndedEarly()
    {
        var repo = new FakeBilling();
        var start = Now.AddDays(-35);   // đang ở cửa sổ 1 (ngày 30..60) của gói 90 ngày / 30 ngày
        var cycle = repo.AddActive(Me, "personal-3m", startsAt: start);
        repo.Use(Me, cycle, 600_000, start.AddDays(5));   // dùng hết cửa sổ 0

        var service = Service(repo);
        var summary = await service.GetSummaryAsync(Me, Now);
        var cycles = await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal(CycleStatuses.Active, cycle.Status);
        Assert.False(Assert.Single(cycles).Exhausted);
        Assert.Equal((0, false, (int?)30), (summary.Personal!.UsedPercent, summary.Personal.Exhausted, summary.Personal.QuotaPeriodDays));
        Assert.Equal(CycleRules.ToVietnamTime(start.AddDays(60)), summary.Personal.QuotaResetsAt);
    }

    [Fact]
    public async Task FreeCustomQuotaPeriod_ResetsOnCustomCadence()
    {
        var repo = new FakeBilling();
        repo.Plans.Single(p => p.Kind == PlanKinds.Free).QuotaPeriodDays = 7;
        var now = new DateTime(2026, 1, 10, 3, 0, 0, DateTimeKind.Utc);   // cửa sổ [2026-01-07 17:00, 2026-01-14 17:00)
        repo.UseFree(Me, 120_000, new DateTime(2026, 1, 5, 3, 0, 0, DateTimeKind.Utc));   // cửa sổ trước

        var summary = await Service(repo).GetSummaryAsync(Me, now);

        Assert.Equal((0, (int?)7), (summary.Free.UsedPercent, summary.Free.QuotaPeriodDays));
        Assert.Equal(CycleRules.ToVietnamTime(new DateTime(2026, 1, 14, 17, 0, 0, DateTimeKind.Utc)), summary.Free.ResetsAt);
    }

    // ---- T1: không phản hồi khách hàng nào có số token

    [Fact]
    public void T1_CustomerDtosHaveNoTokenField()
    {
        Type[] customerDtos =
        [
            typeof(PlanDto), typeof(PlanSeatsDto), typeof(SubscriptionSummaryDto), typeof(PoolDto),
            typeof(FamilyPoolDto), typeof(FreePoolDto), typeof(QueuedCycleDto), typeof(SubscriptionCycleDto),
        ];
        foreach (var type in customerDtos)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
                var stripped = property.Name
                    .Replace("LowQuota", "")
                    .Replace("QuotaPeriodDays", "")
                    .Replace("QuotaMultiplier", "")
                    .Replace("QuotaResetsAt", "");
                Assert.DoesNotContain("quota", stripped, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task T1_SummaryJsonHasNoTokenNumbers()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Owner, member: Me);
        var personal = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, personal, 123_456);

        var json = JsonSerializer.Serialize(await Service(repo).GetSummaryAsync(Me, Now), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("500000", json);
        Assert.DoesNotContain("123456", json);
        Assert.Contains("\"role\":\"member\"", json);   // khai báo FamilyPoolDto nên vai trò và tên chủ gói không bị mất
    }

    [Fact]
    public async Task F5_PlanCodeIsAlwaysAString_EvenWhenThePlanRowOfTheCycleIsGone()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Owner, member: Me);
        repo.AddActive(Me, "personal-1m");
        repo.Plans.RemoveAll(p => p.Code is "personal-1m" or "family-1m");   // gói đã bị xóa khỏi bảng gói, kỳ đã mua vẫn còn

        var summary = await Service(repo).GetSummaryAsync(Me, Now);
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(("", ""), (summary.Personal!.PlanCode, summary.Family!.PlanCode));
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(json, "\"planCode\":\"\"").Count);   // personal, family và hai mục trong cycles
        Assert.DoesNotContain("\"planCode\":null", json);
    }

    // ---- fake

    private static EntitlementService Service(FakeBilling repo) => new(repo);

    private static AiCall Call(Guid userId, int tokens, string kind, Guid? cycleId, string? planCode, DateTime? at = null) => new()
    {
        UserId = userId, Kind = kind, TotalTokens = tokens, UserSubscriptionId = cycleId, PlanCode = planCode, CreatedAt = at ?? Now.AddHours(-1),
    };

    private sealed class FakeBilling : IBillingRepository
    {
        public List<SubscriptionPlan> Plans { get; } =
        [
            new() { Code = "free", Name = "Miễn phí", Kind = PlanKinds.Free, TokenQuota = 120_000, DisplayOrder = 0 },
            new() { Code = "personal-1m", Name = "Cá nhân 1 tháng", Kind = PlanKinds.Personal, PriceVnd = 79_000, DurationDays = 30, QuotaPeriodDays = 30, TokenQuota = 500_000, DisplayOrder = 1 },
            new() { Code = "personal-3m", Name = "Cá nhân 3 tháng", Kind = PlanKinds.Personal, PriceVnd = 199_000, DurationDays = 90, QuotaPeriodDays = 30, TokenQuota = 600_000, DisplayOrder = 2 },
            new() { Code = "family-1m", Name = "Gia đình 1 tháng", Kind = PlanKinds.Family, PriceVnd = 99_000, DurationDays = 30, QuotaPeriodDays = 30, TokenQuota = 500_000, DisplayOrder = 3 },
            new() { Code = "family-3m", Name = "Gia đình 3 tháng", Kind = PlanKinds.Family, PriceVnd = 249_000, DurationDays = 90, QuotaPeriodDays = 30, TokenQuota = 600_000, DisplayOrder = 4 },
        ];
        public List<UserSubscription> Cycles { get; } = [];
        public List<FamilyGroup> Groups { get; } = [];
        public List<FamilyMember> Members { get; } = [];
        public List<AiCall> Calls { get; } = [];
        public List<PaymentTransaction> Orders { get; } = [];
        public int Reads { get; private set; }
        public int Saves { get; private set; }

        public UserSubscription AddActive(Guid owner, string planCode, DateTime? startsAt = null, long? quota = null, int? quotaPeriodDays = null)
        {
            var plan = Plans.Single(p => p.Code == planCode);
            var start = startsAt ?? Now.AddDays(-1);
            var cycle = new UserSubscription
            {
                UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = CycleStatuses.Active, StartsAt = start,
                EndsAt = start.AddDays(plan.DurationDays!.Value), DurationDays = plan.DurationDays!.Value,
                QuotaPeriodDays = quotaPeriodDays ?? plan.QuotaPeriodDays,
                TokenQuota = quota ?? plan.TokenQuota, PlanName = plan.Name,
            };
            Cycles.Add(cycle);
            return cycle;
        }

        /// <summary>Nhóm gia đình của owner với một kỳ family-1m active; member (nếu có) là thành viên active.</summary>
        public UserSubscription AddFamily(Guid owner, Guid? member = null, DateTime? startsAt = null)
        {
            var group = new FamilyGroup { OwnerUserId = owner, Status = FamilyGroupStatuses.Active };
            Groups.Add(group);
            if (member is { } m)
                Members.Add(new FamilyMember
                {
                    FamilyGroupId = group.Id, UserId = m, InvitedEmail = "m@x.vn", SeatRole = FamilySeats.Student, Status = FamilyMemberStatuses.Active,
                });
            var cycle = AddActive(owner, "family-1m", startsAt);
            cycle.FamilyGroupId = group.Id;
            return cycle;
        }

        public UserSubscription AddFamilyCycle(Guid owner, string planCode = "family-1m", DateTime? startsAt = null)
        {
            var cycle = AddActive(owner, planCode, startsAt);
            cycle.FamilyGroupId = Groups.First(g => g.OwnerUserId == owner && g.Status == FamilyGroupStatuses.Active).Id;
            return cycle;
        }

        public void Use(Guid userId, UserSubscription cycle, int tokens, DateTime? at = null) =>
            Calls.Add(Call(userId, tokens, BillingRules.MeteredAiKind, cycle.Id, Plans.Single(p => p.Id == cycle.SubscriptionPlanId).Code, at));

        public void UseFree(Guid userId, int tokens, DateTime at) =>
            Calls.Add(Call(userId, tokens, BillingRules.MeteredAiKind, null, "free", at));

        public Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default) =>
            Read<IReadOnlyList<SubscriptionPlan>>(Plans.ToList());

        public Task<List<UserSubscription>> GetActiveCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default) =>
            Read(Cycles.Where(c => c.UserId == ownerUserId && c.Kind == kind && c.Status == CycleStatuses.Active).ToList());

        public Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default) =>
            Read(Groups.FirstOrDefault(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active));

        public Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default) =>
            Read(Groups.FirstOrDefault(g => g.Status == FamilyGroupStatuses.Active
                && Members.Any(m => m.FamilyGroupId == g.Id && m.UserId == userId && m.Status == FamilyMemberStatuses.Active)));

        public Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default) =>
            Read(Members.Where(m => m.FamilyGroupId == familyGroupId
                && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)).ToList());

        // Bộ lọc thật của repository (MeteringFilters), chạy trên danh sách trong bộ nhớ
        public Task<long> SumCycleTokensAsync(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default) =>
            Read(Calls.AsQueryable().Where(MeteringFilters.Cycle(cycleId, fromUtc, toUtc)).Sum(a => (long)(a.TotalTokens ?? 0)));

        public Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default) =>
            Read(Calls.AsQueryable().Where(MeteringFilters.Free(userId, fromUtc)).Sum(a => (long)(a.TotalTokens ?? 0)));

        public Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default) =>
            Read<string?>(userId == Owner ? "Chủ gói" : "Tôi");

        public Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default) =>
            Read(Orders.Where(o => o.UserId == userId && o.Status == OrderStatuses.Pending).Select(o => (long?)o.OrderCode).FirstOrDefault());

        public Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default)
        {
            foreach (var step in steps)
            {
                step();
                Saves++;
            }
            return Task.CompletedTask;
        }

        public async Task<bool> SaveLockedStepsAsync(Guid ownerUserId, string kind, bool lockGroup, Func<Task<IReadOnlyList<Action>>> plan, CancellationToken ct = default)
        {
            var steps = await plan();
            await SaveStepsAsync(steps, ct);
            return steps.Count > 0;
        }

        private Task<T> Read<T>(T value)
        {
            Reads++;
            return Task.FromResult(value);
        }
    }
}
