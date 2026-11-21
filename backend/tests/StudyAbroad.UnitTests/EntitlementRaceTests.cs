using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Chuyển kỳ khi nhiều request chạm cùng một làn (M1) và thông báo chuyển kỳ trong transaction thanh toán (M3).
public class EntitlementRaceTests
{
    private static readonly DateTime Now = OrderHarness.Now;
    private static readonly DateTime PayAt = Now.AddMinutes(1);

    // Làn personal có kỳ 1 tháng đã quá hạn và một kỳ gia hạn đang chờ
    private static (OrderHarness H, User User, UserSubscription Due, UserSubscription Next) DueLane()
    {
        var h = new OrderHarness();
        var user = h.AddUser();
        var due = h.Store.AddActive(user.Id, "personal-1m", startsAt: Now.AddDays(-31));
        var next = h.Store.AddQueued(user.Id, "personal-3m");
        return (h, user, due, next);
    }

    private static int Count(OrderHarness h, string type) => h.Notes.Sent.Count(n => n.Type == type);

    // ---- M1

    [Fact]
    public async Task M1_TwoSequentialReadsOfADueLane_SecondChangesNothingAndNotifiesNothing()
    {
        var (h, user, due, next) = DueLane();

        await h.Entitlements.GetSummaryAsync(user.Id, Now);
        var savesAfterFirst = h.Store.Saves;
        var notesAfterFirst = h.Notes.Sent.Count;
        await h.Entitlements.GetSummaryAsync(user.Id, Now);

        Assert.Equal(CycleStatuses.Ended, due.Status);
        Assert.Equal(CycleStatuses.Active, next.Status);
        Assert.Equal(1, Count(h, NotificationTypes.BillingCycleStarted));
        Assert.Equal(savesAfterFirst, h.Store.Saves);
        Assert.Equal(notesAfterFirst, h.Notes.Sent.Count);
        Assert.Equal(1, h.Store.LaneLocks);   // lượt đọc thứ hai không còn gì để chuyển nên không chạm khóa
    }

    [Fact]
    public async Task M1_AnotherRequestAlreadyTransitionedTheLane_ThisOneChangesNothingAndSendsNothing()
    {
        var (h, user, due, next) = DueLane();
        // Request kia chuyển xong (và tự báo) ngay sau lượt đọc không khóa của request này, trước khi nó khóa được dòng làn
        h.Store.RaceBeforeLaneLock = s =>
        {
            foreach (var change in CycleRules.Advance(due, next, 0, Now))
                change.Apply();
        };

        var lane = await h.Entitlements.AdvanceLaneAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(next.Id, lane.Active?.Id);
        Assert.Equal(CycleStatuses.Ended, due.Status);
        Assert.Empty(h.Notes.Sent);
        Assert.Equal(1, h.Store.LaneLocks);
    }

    [Fact]
    public async Task M1_TransitionUnderTheLock_StillNotifiesOnceForTheCallerThatChangedIt()
    {
        var (h, user, _, next) = DueLane();

        await h.Entitlements.AdvanceLaneAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(CycleStatuses.Active, next.Status);
        var note = Assert.Single(h.Notes.Sent);
        Assert.Equal((user.Id, NotificationTypes.BillingCycleStarted), (note.UserId, note.Type));
    }

    [Fact]
    public async Task M1_LapseConflict_NextReadFinishesTheGroupAndNotifiesOnce()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, 40);
        var member = h.AddUser();
        h.Store.AddFamily(owner.Id, member.Id, startsAt: Now.AddDays(-31));
        var cycle = h.Store.Cycles.Single();
        cycle.Status = CycleStatuses.Ended;   // kỳ đã được request khác kết thúc (hết hạn): chỉ còn nhóm active không kỳ
        cycle.EndedReason = CycleEndReasons.Expired;
        h.Store.ConcurrencyConflictOnLockedSave = true;   // lần lưu kết thúc nhóm vướng xmin của dòng thành viên (request khác vừa đổi dòng đó)

        await h.Entitlements.GetSummaryAsync(member.Id, Now);   // xung đột không ném ra ngoài

        Assert.Equal(FamilyGroupStatuses.Active, h.Store.Groups.Single().Status);   // transaction quay lui: nhóm chưa kết thúc (đọc lại, thực thể cũ đã thành bản cũ)
        Assert.Equal(0, Count(h, NotificationTypes.FamilyEnded));

        await h.Entitlements.GetSummaryAsync(member.Id, Now);   // lượt đọc kế tiếp kết thúc nhóm

        Assert.Equal(FamilyGroupStatuses.Ended, h.Store.Groups.Single().Status);
        Assert.Equal(2, Count(h, NotificationTypes.FamilyEnded));   // chủ gói và một thành viên, mỗi người một lần
    }

    [Fact]
    public async Task M1_PaymentRenewedTheFamilyLaneBeforeTheLapseLock_GroupStaysActive()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, 40);
        var cycle = h.Store.AddFamily(owner.Id, startsAt: Now.AddDays(-31));
        cycle.Status = CycleStatuses.Ended;
        var group = h.Store.Groups.Single();
        h.Store.RaceBeforeLaneLock = s => s.AddActive(owner.Id, "family-1m", startsAt: Now).FamilyGroupId = group.Id;

        await h.Entitlements.AdvanceLaneAsync(owner.Id, PlanKinds.Family, Now);

        Assert.Equal(FamilyGroupStatuses.Active, group.Status);
        Assert.Empty(h.Notes.Sent);
    }

    // ---- M3

    [Fact]
    public async Task M3_RetriedAttempt_SendsEachTransitionNotificationOnce()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");
        h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-31));   // kỳ cũ quá hạn: thanh toán sẽ chuyển kỳ (hết hạn) trong transaction
        h.Store.FailOnCycleInsert = new UniqueViolationException("ux_user_subscriptions_one_active_per_lane");   // lần thử đầu vướng index, lần hai thành công

        await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt);

        Assert.Equal(2, h.Store.Transactions);
        Assert.Equal(1, Count(h, NotificationTypes.BillingExpired));
        Assert.Equal(1, Count(h, NotificationTypes.BillingPaymentPaid));
    }

    [Fact]
    public async Task M3_FailureBeforeCommit_SendsNoTransitionNotification()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");
        h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-31));
        h.Store.FailOnCycleInsert = new InvalidOperationException("DB sập");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt));

        Assert.Empty(h.Notes.Sent);
    }

    [Fact]
    public async Task M3_ReadPathKeepsSendingTransitionNotificationsImmediately()
    {
        var (h, user, _, _) = DueLane();

        await h.Entitlements.AdvanceLaneAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(1, Count(h, NotificationTypes.BillingCycleStarted));
    }

    [Fact]
    public async Task M3_DeferredCollector_QueuesInsteadOfSending()
    {
        var (h, user, _, _) = DueLane();
        var deferred = new List<Func<CancellationToken, Task>>();

        await h.Entitlements.AdvanceLaneAsync(user.Id, PlanKinds.Personal, Now, default, deferred);

        Assert.Empty(h.Notes.Sent);
        await deferred.Single()(CancellationToken.None);
        Assert.Equal(1, Count(h, NotificationTypes.BillingCycleStarted));
    }
}
