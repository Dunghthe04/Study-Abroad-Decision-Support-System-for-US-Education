using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// IEntitlementService.EndExpiredCyclesAsync: kết thúc kỳ hết hạn, trả các kỳ còn chạy kèm token đã dùng; nhóm Gia đình chỉ kết thúc khi hết kỳ Gia đình cuối cùng
public class EntitlementCycleTests
{
    private static readonly DateTime Now = BillingStore.Now;
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static (BillingStore Store, EntitlementService Service) Setup()
    {
        var store = new BillingStore();
        return (store, new EntitlementService(store));
    }

    [Fact]
    public async Task NoCycles_ReturnsEmpty()
    {
        var (_, service) = Setup();

        Assert.Empty(await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now));
    }

    [Fact]
    public async Task ActiveWithTokensLeft_ReportsUsage_NotExhausted()
    {
        var (store, service) = Setup();
        var active = store.AddActive(Me, "personal-1m");
        store.Use(Me, active, 100_000);

        var cycle = Assert.Single(await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now));

        Assert.Equal((active, 100_000L, false), (cycle.Cycle, cycle.Used, cycle.Exhausted));
    }

    [Theory]
    [InlineData(500_000, true)]
    [InlineData(499_999, false)]
    [InlineData(900_000, true)]
    public async Task Exhausted_WhenUsedReachesTheQuota(int used, bool exhausted)
    {
        var (store, service) = Setup();
        store.Use(Me, store.AddActive(Me, "personal-1m"), used);

        Assert.Equal(exhausted, Assert.Single(await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now)).Exhausted);
    }

    [Fact]
    public async Task QuotaZero_IsAlwaysExhausted()
    {
        var (store, service) = Setup();
        store.AddActive(Me, "personal-1m", quota: 0);

        Assert.True(Assert.Single(await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now)).Exhausted);
    }

    [Fact]
    public async Task ExpiredCycle_IsEnded_AtItsEndDate_WhileTheOtherStaysActive()
    {
        var (store, service) = Setup();
        var old = store.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-31));
        var running = store.AddActive(Me, "personal-3m");

        var cycles = await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal([running], cycles.Select(c => c.Cycle));
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired, old.EndsAt), (old.Status, old.EndedReason, old.EndedAt));
        Assert.Equal(CycleStatuses.Active, running.Status);
    }

    [Fact]
    public async Task ExhaustedCycle_StaysActiveUntilItsEndDate_NoRollover()
    {
        var (store, service) = Setup();
        var exhausted = store.AddActive(Me, "personal-1m");
        store.Use(Me, exhausted, 500_000);
        var second = store.AddActive(Me, "personal-1m");

        var cycles = await service.EndExpiredCyclesAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal(2, cycles.Count);
        Assert.All(new[] { exhausted, second }, c => Assert.Equal(CycleStatuses.Active, c.Status));
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task OnlyTheAskedKindIsTouched()
    {
        var (store, service) = Setup();
        var personal = store.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-31));
        store.AddFamily(Owner);

        await service.EndExpiredCyclesAsync(Owner, PlanKinds.Family, Now);

        Assert.Equal(CycleStatuses.Active, personal.Status);   // kỳ Personal của người khác chưa được chạm
    }

    // ---- nhóm gia đình chỉ hết hiệu lực khi chủ gói không còn kỳ Gia đình nào (C8, SW9)

    [Fact]
    public async Task FamilyLastCycleExpired_EndsTheOwnersGroupMembersAndInvites()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var group = store.Groups.Single();
        store.Members.Add(new FamilyMember { FamilyGroupId = group.Id, InvitedEmail = "x@y.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "t" });

        var cycles = await service.EndExpiredCyclesAsync(Owner, PlanKinds.Family, Now);

        Assert.Empty(cycles);
        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.FamilyLapsed, (DateTime?)Now), (group.Status, group.EndedReason, group.EndedAt));
        Assert.All(store.Members, m => Assert.Equal((FamilyMemberStatuses.Ended, (string?)null), (m.Status, m.TokenHash)));
    }

    [Fact]
    public async Task E_GroupLapsesOnlyAfterTheLastFamilyCycleExpires()
    {
        var (store, service) = Setup();
        var first = store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));   // đã hết hạn
        var second = store.AddFamilyCycle(Owner, "family-3m", startsAt: Now.AddDays(-10));   // còn 80 ngày
        var group = store.Groups.Single();

        var cycles = await service.EndExpiredCyclesAsync(Owner, PlanKinds.Family, Now);

        Assert.Equal([second], cycles.Select(c => c.Cycle));
        Assert.Equal(CycleStatuses.Ended, first.Status);
        Assert.Equal((FamilyGroupStatuses.Active, FamilyMemberStatuses.Active), (group.Status, store.Members.Single().Status));

        // kỳ cuối cùng cũng hết hạn: nhóm kết thúc
        var later = Now.AddDays(81);
        Assert.Empty(await service.EndExpiredCyclesAsync(Owner, PlanKinds.Family, later));
        Assert.Equal(CycleStatuses.Ended, second.Status);
        Assert.Equal((FamilyGroupStatuses.Ended, FamilyMemberStatuses.Ended), (group.Status, store.Members.Single().Status));
    }

    [Fact]
    public async Task FamilyWithACycle_LeavesTheGroupAlone()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me);

        var cycles = await service.EndExpiredCyclesAsync(Owner, PlanKinds.Family, Now);

        Assert.Single(cycles);
        Assert.Equal(FamilyGroupStatuses.Active, store.Groups.Single().Status);
        Assert.Equal(FamilyMemberStatuses.Active, store.Members.Single().Status);
    }

    [Fact]
    public async Task PersonalKind_NeverEndsAFamilyGroup()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, startsAt: Now.AddDays(-31));   // kỳ gia đình đã hết hạn nhưng chưa ai đọc loại family

        await service.EndExpiredCyclesAsync(Owner, PlanKinds.Personal, Now);

        Assert.Equal(FamilyGroupStatuses.Active, store.Groups.Single().Status);
    }

    [Fact]
    public async Task FamilyOfSomeoneWithNoGroup_IsJustEmpty()
    {
        var (_, service) = Setup();

        Assert.Empty(await service.EndExpiredCyclesAsync(Me, PlanKinds.Family, Now));
    }

    [Fact]
    public async Task Summary_OfAMemberWhoseGroupExpired_IsFree_AndTheGroupEnds()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));

        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(AccountTiers.Free, summary.Tier);
        Assert.Empty(summary.Cycles);
        Assert.Equal(FamilyGroupStatuses.Ended, store.Groups.Single().Status);
    }
}
