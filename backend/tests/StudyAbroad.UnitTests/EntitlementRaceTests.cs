using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Kết thúc kỳ hết hạn khi nhiều request chạm cùng lúc (M1) và thông báo trong transaction thanh toán (M3).
public class EntitlementRaceTests
{
    private static readonly DateTime Now = OrderHarness.Now;
    private static readonly DateTime PayAt = Now.AddMinutes(1);

    // Người dùng có một kỳ personal 1 tháng đã quá hạn (chưa ai kết thúc)
    private static (OrderHarness H, User User, UserSubscription Due) DueCycle()
    {
        var h = new OrderHarness();
        var user = h.AddUser();
        var due = h.Store.AddActive(user.Id, "personal-1m", startsAt: Now.AddDays(-31));
        return (h, user, due);
    }

    private static int Count(OrderHarness h, string type) => h.Notes.Sent.Count(n => n.Type == type);

    // ---- M1

    [Fact]
    public async Task M1_TwoSequentialReadsOfADueCycle_SecondChangesNothingAndNotifiesNothing()
    {
        var (h, user, due) = DueCycle();

        await h.Entitlements.GetSummaryAsync(user.Id, Now);
        var savesAfterFirst = h.Store.Saves;
        var notesAfterFirst = h.Notes.Sent.Count;
        await h.Entitlements.GetSummaryAsync(user.Id, Now);

        Assert.Equal(CycleStatuses.Ended, due.Status);
        Assert.Equal(1, Count(h, NotificationTypes.BillingExpired));
        Assert.Equal(savesAfterFirst, h.Store.Saves);
        Assert.Equal(notesAfterFirst, h.Notes.Sent.Count);
        Assert.Equal(1, h.Store.CycleLocks);   // lượt đọc thứ hai không còn gì để kết thúc nên không chạm khóa
    }

    [Fact]
    public async Task M1_AnotherRequestAlreadyEndedTheCycle_ThisOneChangesNothingAndSendsNothing()
    {
        var (h, user, due) = DueCycle();
        // Request kia kết thúc xong (và tự báo) ngay sau lượt đọc không khóa của request này, trước khi nó khóa được dòng kỳ
        h.Store.RaceBeforeCycleLock = s =>
        {
            due.Status = CycleStatuses.Ended;
            due.EndedReason = CycleEndReasons.Expired;
        };

        var cycles = await h.Entitlements.EndExpiredCyclesAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Empty(cycles);
        Assert.Equal(CycleStatuses.Ended, due.Status);
        Assert.Empty(h.Notes.Sent);
        Assert.Equal(1, h.Store.CycleLocks);
    }

    [Fact]
    public async Task M1_EndingUnderTheLock_NotifiesOnceForTheCallerThatChangedIt()
    {
        var (h, user, due) = DueCycle();

        await h.Entitlements.EndExpiredCyclesAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(CycleStatuses.Ended, due.Status);
        var note = Assert.Single(h.Notes.Sent);
        Assert.Equal((user.Id, NotificationTypes.BillingExpired), (note.UserId, note.Type));
    }

    [Fact]
    public async Task M1_AnExpiredCycleWithAnotherStillRunning_DoesNotNotifyExpiry()
    {
        var (h, user, due) = DueCycle();
        h.Store.AddActive(user.Id, "personal-3m");

        await h.Entitlements.EndExpiredCyclesAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(CycleStatuses.Ended, due.Status);
        Assert.Empty(h.Notes.Sent);   // vẫn đang Premium bằng kỳ còn lại
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
    public async Task M1_PaymentAddedAFamilyCycleBeforeTheLapseLock_GroupStaysActive()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, 40);
        var cycle = h.Store.AddFamily(owner.Id, startsAt: Now.AddDays(-31));
        cycle.Status = CycleStatuses.Ended;
        var group = h.Store.Groups.Single();
        h.Store.RaceBeforeCycleLock = s => s.AddActive(owner.Id, "family-1m", startsAt: Now).FamilyGroupId = group.Id;

        await h.Entitlements.EndExpiredCyclesAsync(owner.Id, PlanKinds.Family, Now);

        Assert.Equal(FamilyGroupStatuses.Active, group.Status);
        Assert.Empty(h.Notes.Sent);
    }

    // ---- M3

    [Fact]
    public async Task M3_RetriedAttempt_SendsEachTransitionNotificationOnce()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(UserRoles.Parent, 40);
        var order = await h.CreateOrder(buyer, "family-1m");
        h.Store.AddFamily(buyer.Id, startsAt: Now.AddDays(-31));   // kỳ Gia đình cũ quá hạn: thanh toán kết thúc nó và nhóm cũ trong transaction
        h.Store.FailOnCycleInsert = new UniqueViolationException("xung đột");   // lần thử đầu vướng xung đột, lần hai thành công

        await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt);

        Assert.Equal(2, h.Store.Transactions);
        Assert.Equal(1, Count(h, NotificationTypes.BillingExpired));
        Assert.Equal(1, Count(h, NotificationTypes.FamilyEnded));
        Assert.Equal(1, Count(h, NotificationTypes.BillingPaymentPaid));
    }

    [Fact]
    public async Task M3_FailureBeforeCommit_SendsNoTransitionNotification()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(UserRoles.Parent, 40);
        var order = await h.CreateOrder(buyer, "family-1m");
        h.Store.AddFamily(buyer.Id, startsAt: Now.AddDays(-31));
        h.Store.FailOnCycleInsert = new InvalidOperationException("DB sập");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt));

        Assert.Empty(h.Notes.Sent);
    }

    [Fact]
    public async Task M3_ReadPathKeepsSendingExpiryNotificationsImmediately()
    {
        var (h, user, _) = DueCycle();

        await h.Entitlements.EndExpiredCyclesAsync(user.Id, PlanKinds.Personal, Now);

        Assert.Equal(1, Count(h, NotificationTypes.BillingExpired));
    }

    [Fact]
    public async Task M3_DeferredCollector_QueuesInsteadOfSending()
    {
        var (h, user, _) = DueCycle();
        var deferred = new List<Func<CancellationToken, Task>>();

        await h.Entitlements.EndExpiredCyclesAsync(user.Id, PlanKinds.Personal, Now, default, deferred);

        Assert.Empty(h.Notes.Sent);
        await deferred.Single()(CancellationToken.None);
        Assert.Equal(1, Count(h, NotificationTypes.BillingExpired));
    }
}
