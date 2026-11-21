using System.Reflection;
using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Quyền dùng gói: tier theo ngày, thứ tự trừ Personal → Family → Free, chuyển kỳ khi đọc, phần trăm (kế hoạch billing v2).
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
    public async Task F18_MemberWithOwnPersonal_PersonalIsChargedFirst()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Owner, member: Me);
        var personal = repo.AddActive(Me, "personal-1m");

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(personal.Id, charge.CycleId);
    }

    [Fact]
    public async Task F40_MemberWhosePersonalRanOut_FallsBackToTheFamilyPool()
    {
        var repo = new FakeBilling();
        var family = repo.AddFamily(Owner, member: Me);
        var personal = repo.AddActive(Me, "personal-1m");
        repo.Use(Me, personal, 500_000);

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal((family.Id, "family-1m", PlanKinds.Family), (charge.CycleId!.Value, charge.PlanCode, charge.PoolKind));
    }

    [Fact]
    public async Task F19_OwnerWithPersonalAndFamily_PersonalFirst_FamilyKeptForOthers()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Me);
        var personal = repo.AddActive(Me, "personal-1m");

        var service = Service(repo);
        var charge = await service.ResolveChatChargeAsync(Me, UserRoles.Parent, Now);
        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(personal.Id, charge.CycleId);
        Assert.Equal((PlanKinds.Personal, "owner"), (summary.ChargingPool, summary.Family!.Role));
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

    // ---- chuyển kỳ khi đọc

    [Fact]
    public async Task R2_R9_ChatRightAfterExpiry_StartsTheRenewalAtTheOldEndDateWithoutTheJob()
    {
        var repo = new FakeBilling();
        var old = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-30).AddMinutes(-1));
        var renewal = repo.AddQueued(Me, "personal-3m");

        var charge = await Service(repo).ResolveChatChargeAsync(Me, UserRoles.Student, Now);

        Assert.Equal(renewal.Id, charge.CycleId);
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired), (old.Status, old.EndedReason));
        Assert.Equal((old.EndsAt, old.EndsAt!.Value.AddDays(90)), (renewal.StartsAt, renewal.EndsAt));
        Assert.Equal(2, repo.Saves);   // kết thúc kỳ cũ và kích hoạt kỳ mới lưu riêng, theo thứ tự
    }

    [Fact]
    public async Task R3_ExhaustedWithQueued_RollsOverNow()
    {
        var repo = new FakeBilling();
        var old = repo.AddActive(Me, "personal-1m");
        var renewal = repo.AddQueued(Me, "personal-1m");
        repo.Use(Me, old, 500_000);

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal((CycleEndReasons.ExhaustedRollover, (DateTime?)Now, (DateTime?)Now), (old.EndedReason, old.EndedAt, old.EndsAt));   // kỳ cũ hết hạn đúng lúc đổi kỳ (như đường thanh toán)
        Assert.Equal((Now, Now.AddDays(30)), (renewal.StartsAt!.Value, renewal.EndsAt!.Value));
        Assert.Equal((0, false, (QueuedCycleDto?)null), (summary.Personal!.UsedPercent, summary.Personal.Exhausted, summary.Personal.Queued));
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
    public async Task FamilyRenewalQueued_MembersCarryOverToTheNewCycle()
    {
        var repo = new FakeBilling();
        repo.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var renewal = repo.AddQueued(Owner, "family-3m");

        var summary = await Service(repo).GetSummaryAsync(Me, Now);

        Assert.Equal(FamilyGroupStatuses.Active, repo.Groups.Single().Status);
        Assert.Equal(("family-3m", "member", "Chủ gói"), (summary.Family!.PlanCode, summary.Family.Role, summary.Family.OwnerName));
        Assert.Equal(CycleStatuses.Active, renewal.Status);
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
    public async Task Warnings_AreForTheChargingPool_AndExpiringSoonIsQuietWhenRenewed()
    {
        var repo = new FakeBilling();
        var personal = repo.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-25));
        repo.Use(Me, personal, 410_000);

        var service = Service(repo);
        var warned = await service.GetSummaryAsync(Me, Now);
        repo.AddQueued(Me, "personal-1m");
        var renewed = await service.GetSummaryAsync(Me, Now);

        Assert.Equal((true, true), (warned.LowQuota, warned.ExpiringSoon));
        Assert.False(renewed.ExpiringSoon);
        Assert.Equal(("Cá nhân 1 tháng", personal.EndsAt), (renewed.Personal!.Queued!.PlanName, renewed.Personal.Queued.ExpectedStartsAt));
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
        Assert.Equal(new PlanSeatsDto(1, 2), plans.Single(p => p.Code == "family-1m").Seats);
        Assert.Null(plans.Single(p => p.Code == "personal-1m").Seats);
        Assert.Equal(79_000L, plans.Single(p => p.Code == "personal-1m").PriceVnd);
    }

    // ---- T1: không phản hồi khách hàng nào có số token

    [Fact]
    public void T1_CustomerDtosHaveNoTokenField()
    {
        Type[] customerDtos =
        [
            typeof(PlanDto), typeof(PlanSeatsDto), typeof(SubscriptionSummaryDto), typeof(PoolDto),
            typeof(FamilyPoolDto), typeof(FreePoolDto), typeof(QueuedCycleDto),
        ];
        foreach (var type in customerDtos)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("quota", property.Name.Replace("LowQuota", ""), StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(json, "\"planCode\":\"\"").Count);
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
            new() { Code = "personal-1m", Name = "Cá nhân 1 tháng", Kind = PlanKinds.Personal, PriceVnd = 79_000, DurationDays = 30, TokenQuota = 500_000, DisplayOrder = 1 },
            new() { Code = "personal-3m", Name = "Cá nhân 3 tháng", Kind = PlanKinds.Personal, PriceVnd = 199_000, DurationDays = 90, TokenQuota = 1_800_000, DisplayOrder = 2 },
            new() { Code = "family-1m", Name = "Gia đình 1 tháng", Kind = PlanKinds.Family, PriceVnd = 99_000, DurationDays = 30, TokenQuota = 500_000, DisplayOrder = 3 },
            new() { Code = "family-3m", Name = "Gia đình 3 tháng", Kind = PlanKinds.Family, PriceVnd = 249_000, DurationDays = 90, TokenQuota = 1_800_000, DisplayOrder = 4 },
        ];
        public List<UserSubscription> Cycles { get; } = [];
        public List<FamilyGroup> Groups { get; } = [];
        public List<FamilyMember> Members { get; } = [];
        public List<AiCall> Calls { get; } = [];
        public List<PaymentTransaction> Orders { get; } = [];
        public int Reads { get; private set; }
        public int Saves { get; private set; }

        public UserSubscription AddActive(Guid owner, string planCode, DateTime? startsAt = null, long? quota = null)
        {
            var plan = Plans.Single(p => p.Code == planCode);
            var start = startsAt ?? Now.AddDays(-1);
            var cycle = new UserSubscription
            {
                UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = CycleStatuses.Active, StartsAt = start,
                EndsAt = start.AddDays(plan.DurationDays!.Value), DurationDays = plan.DurationDays!.Value, TokenQuota = quota ?? plan.TokenQuota, PlanName = plan.Name,
            };
            Cycles.Add(cycle);
            return cycle;
        }

        public UserSubscription AddQueued(Guid owner, string planCode)
        {
            var plan = Plans.Single(p => p.Code == planCode);
            var cycle = new UserSubscription
            {
                UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = CycleStatuses.Queued,
                DurationDays = plan.DurationDays!.Value, TokenQuota = plan.TokenQuota, PlanName = plan.Name,
                FamilyGroupId = plan.Kind == PlanKinds.Family ? Groups.Single(g => g.OwnerUserId == owner).Id : null,
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

        public void Use(Guid userId, UserSubscription cycle, int tokens) =>
            Calls.Add(Call(userId, tokens, BillingRules.MeteredAiKind, cycle.Id, Plans.Single(p => p.Id == cycle.SubscriptionPlanId).Code));

        public void UseFree(Guid userId, int tokens, DateTime at) =>
            Calls.Add(Call(userId, tokens, BillingRules.MeteredAiKind, null, "free", at));

        public Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default) =>
            Read<IReadOnlyList<SubscriptionPlan>>(Plans.ToList());

        public Task<List<UserSubscription>> GetOpenCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default) =>
            Read(Cycles.Where(c => c.UserId == ownerUserId && c.Kind == kind
                && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued)).ToList());

        public Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default) =>
            Read(Groups.FirstOrDefault(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active));

        public Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default) =>
            Read(Groups.FirstOrDefault(g => g.Status == FamilyGroupStatuses.Active
                && Members.Any(m => m.FamilyGroupId == g.Id && m.UserId == userId && m.Status == FamilyMemberStatuses.Active)));

        public Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default) =>
            Read(Members.Where(m => m.FamilyGroupId == familyGroupId
                && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)).ToList());

        // Bộ lọc thật của repository (MeteringFilters), chạy trên danh sách trong bộ nhớ
        public Task<long> SumCycleTokensAsync(Guid cycleId, CancellationToken ct = default) =>
            Read(Calls.AsQueryable().Where(MeteringFilters.Cycle(cycleId)).Sum(a => (long)(a.TotalTokens ?? 0)));

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
                // Như index duy nhất trong DB: mỗi làn tối đa 1 active và 1 queued sau mỗi lần lưu
                Assert.All(Cycles.GroupBy(c => (c.UserId, c.Kind)), lane =>
                {
                    Assert.True(lane.Count(c => c.Status == CycleStatuses.Active) <= 1, "Hai kỳ active cùng một làn");
                    Assert.True(lane.Count(c => c.Status == CycleStatuses.Queued) <= 1, "Hai kỳ queued cùng một làn");
                });
            }
            return Task.CompletedTask;
        }

        private Task<T> Read<T>(T value)
        {
            Reads++;
            return Task.FromResult(value);
        }
    }
}
