using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// IEntitlementService.AdvanceLaneAsync: làn sau khi chuyển kỳ, dùng cho đơn hàng (tạo đơn, áp dụng thanh toán)
public class EntitlementLaneTests
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
    public async Task EmptyLane_HasNothing()
    {
        var (_, service) = Setup();

        var lane = await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal(new LaneState(null, null, 0, false), lane);
    }

    [Fact]
    public async Task ActiveWithTokensLeft_ReportsUsage_NotExhausted()
    {
        var (store, service) = Setup();
        var active = store.AddActive(Me, "personal-1m");
        store.Use(Me, active, 100_000);

        var lane = await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal((active, (UserSubscription?)null, 100_000L, false), (lane.Active, lane.Queued, lane.ActiveUsed, lane.ActiveExhausted));
    }

    [Theory]
    [InlineData(500_000, true)]
    [InlineData(499_999, false)]
    [InlineData(900_000, true)]
    public async Task Exhausted_WhenUsedReachesTheQuota(int used, bool exhausted)
    {
        var (store, service) = Setup();
        store.Use(Me, store.AddActive(Me, "personal-1m"), used);

        Assert.Equal(exhausted, (await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now)).ActiveExhausted);
    }

    [Fact]
    public async Task QuotaZero_IsAlwaysExhausted()
    {
        var (store, service) = Setup();
        store.AddActive(Me, "personal-1m", quota: 0);

        Assert.True((await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now)).ActiveExhausted);
    }

    [Fact]
    public async Task ExpiredActiveWithQueued_ReturnsTheStartedQueuedCycle_AlreadySaved()
    {
        var (store, service) = Setup();
        var old = store.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-31));
        var renewal = store.AddQueued(Me, "personal-3m");

        var lane = await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal((renewal, (UserSubscription?)null, 0L), (lane.Active, lane.Queued, lane.ActiveUsed));
        Assert.Equal((CycleStatuses.Ended, CycleStatuses.Active, old.EndsAt), (old.Status, renewal.Status, renewal.StartsAt));
        Assert.Equal(2, store.Saves);
    }

    [Fact]
    public async Task ExhaustedActiveWithQueued_RollsOver_NewCycleIsFreshAndNotExhausted()
    {
        var (store, service) = Setup();
        var old = store.AddActive(Me, "personal-1m");
        store.Use(Me, old, 500_000);
        var renewal = store.AddQueued(Me, "personal-1m");

        var lane = await service.AdvanceLaneAsync(Me, PlanKinds.Personal, Now);

        Assert.Equal((renewal, 0L, false), (lane.Active, lane.ActiveUsed, lane.ActiveExhausted));
        Assert.Equal(CycleEndReasons.ExhaustedRollover, old.EndedReason);
    }

    [Fact]
    public async Task OnlyTheAskedLaneIsAdvanced()
    {
        var (store, service) = Setup();
        var personal = store.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-31));
        store.AddFamily(Owner);

        await service.AdvanceLaneAsync(Owner, PlanKinds.Family, Now);

        Assert.Equal(CycleStatuses.Active, personal.Status);   // làn Personal của người khác chưa được chạm
    }

    // ---- nhóm gia đình hết hiệu lực khi làn family trống (C8, SW9)

    [Fact]
    public async Task FamilyLaneEmpty_EndsTheOwnersGroupMembersAndInvites()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var group = store.Groups.Single();
        store.Members.Add(new FamilyMember { FamilyGroupId = group.Id, InvitedEmail = "x@y.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "t" });

        var lane = await service.AdvanceLaneAsync(Owner, PlanKinds.Family, Now);

        Assert.Equal(new LaneState(null, null, 0, false), lane);
        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.FamilyLapsed, (DateTime?)Now), (group.Status, group.EndedReason, group.EndedAt));
        Assert.All(store.Members, m => Assert.Equal((FamilyMemberStatuses.Ended, (string?)null), (m.Status, m.TokenHash)));
    }

    [Fact]
    public async Task FamilyLaneWithACycle_LeavesTheGroupAlone()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me);

        var lane = await service.AdvanceLaneAsync(Owner, PlanKinds.Family, Now);

        Assert.NotNull(lane.Active);
        Assert.Equal(FamilyGroupStatuses.Active, store.Groups.Single().Status);
        Assert.Equal(FamilyMemberStatuses.Active, store.Members.Single().Status);
    }

    [Fact]
    public async Task FamilyRenewalQueued_KeepsTheGroup_AndStartsTheRenewal()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));
        var renewal = store.AddQueued(Owner, "family-3m");

        var lane = await service.AdvanceLaneAsync(Owner, PlanKinds.Family, Now);

        Assert.Equal(renewal, lane.Active);
        Assert.Equal(FamilyGroupStatuses.Active, store.Groups.Single().Status);
    }

    [Fact]
    public async Task PersonalLane_NeverEndsAFamilyGroup()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, startsAt: Now.AddDays(-31));   // kỳ gia đình đã hết hạn nhưng chưa ai đọc làn family

        await service.AdvanceLaneAsync(Owner, PlanKinds.Personal, Now);

        Assert.Equal(FamilyGroupStatuses.Active, store.Groups.Single().Status);
    }

    [Fact]
    public async Task FamilyLaneOfSomeoneWithNoGroup_IsJustEmpty()
    {
        var (_, service) = Setup();

        Assert.Equal(new LaneState(null, null, 0, false), await service.AdvanceLaneAsync(Me, PlanKinds.Family, Now));
    }

    [Fact]
    public async Task SummaryAndChargeStillWorkThroughTheSharedLaneLogic()
    {
        var (store, service) = Setup();
        store.AddFamily(Owner, member: Me, startsAt: Now.AddDays(-31));

        var summary = await service.GetSummaryAsync(Me, Now);

        Assert.Equal(AccountTiers.Free, summary.Tier);
        Assert.Equal(FamilyGroupStatuses.Ended, store.Groups.Single().Status);
    }
}
