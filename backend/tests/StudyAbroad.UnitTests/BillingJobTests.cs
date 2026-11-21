using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Billing.Jobs;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.BackgroundJobs;

namespace StudyAbroad.UnitTests;

/// <summary>OrderHarness cộng job nền, bộ đối soát và dịch vụ admin đơn hàng (cùng cơ sở dữ liệu giả, cùng cổng giả).</summary>
internal sealed class JobHarness
{
    public OrderHarness H { get; } = new();
    public StubFamily Family { get; } = new();
    public FakeAuditLogs Audit { get; } = new();
    public OrderReconciler Reconciler { get; }
    public BillingJobService Jobs { get; }
    public AdminOrderService Admin { get; }
    public BillingStore Store => H.Store;
    public RecordingNotifications Notes => H.Notes;

    public JobHarness()
    {
        Reconciler = new OrderReconciler(H.Store, H.Orders, H.Gateway, H.Store, H.Billing);
        Jobs = JobsWith(H.Entitlements);
        Admin = new AdminOrderService(H.Store, H.Store, H.Orders, Reconciler, H.Gateway, Audit, H.Store, H.Notes);
    }

    /// <summary>Job nền trên cùng dữ liệu với một bản IEntitlementService khác (ví dụ bản hỏng cho một chủ gói).</summary>
    public BillingJobService JobsWith(IEntitlementService entitlements) =>
        new(Store, Store, Store, Reconciler, entitlements, Family, H.Users, H.Email, H.Notes, H.Billing);

    public User AddUser(string role = UserRoles.Student, int? age = 17, string? email = null, string? name = null)
    {
        var user = H.AddUser(role, age, email, name);
        Store.Accounts[user.Id] = user;
        return user;
    }

    public User AddAdmin(string name = "Admin Một") => AddUser(UserRoles.Admin, null, name: name);

    public IEnumerable<(Guid UserId, string Type, string Title, string? Body, string? Link)> Sent(string type) => Notes.Sent.Where(n => n.Type == type);

    /// <summary>IFamilyService chỉ có ExpireInvitesAsync (bước 3 của job); phần còn lại có test riêng ở FamilyServiceTests.</summary>
    internal sealed class StubFamily : IFamilyService
    {
        public List<DateTime> ExpireCalls { get; } = [];
        public int ExpiredCount { get; set; }

        public Task<int> ExpireInvitesAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            ExpireCalls.Add(nowUtc);
            return Task.FromResult(ExpiredCount);
        }

        public Task<FamilyDto> GetAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task InviteAsync(Guid ownerId, string email, string seatRole, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ResendInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CancelInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FamilyInviteLookupDto> LookupAsync(string token, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FamilyDto> AcceptAsync(Guid userId, string role, string token, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeclineAsync(Guid userId, string token, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveMemberAsync(Guid ownerId, Guid memberId, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LeaveAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

// Job nền thanh toán (kế hoạch billing v2, tác vụ 11): P11, R2, R8, F13, F27, T7, T8 và bảng đối soát payOS.
public class BillingJobTests
{
    private static readonly DateTime Now = BillingStore.Now;

    private static async Task<(JobHarness J, User Buyer, OrderDto Order)> Pending(string plan = "personal-1m")
    {
        var j = new JobHarness();
        var buyer = j.AddUser();
        return (j, buyer, await j.H.CreateOrder(buyer, plan));
    }

    private static DateTime PastGrace(OrderDto order, BillingOptions o) => order.ExpiresAt.AddMinutes(o.PendingGraceMinutes).AddSeconds(1);

    // ---- P11: đơn không trả thì hết hạn

    [Fact]
    public async Task P11_PendingOrder_ExpiresOnlyAfterExpiryPlusGrace_AndTheLinkIsCancelled()
    {
        var (j, _, order) = await Pending();

        await j.Jobs.ReconcilePendingOrdersAsync(order.ExpiresAt.AddMinutes(2));   // đúng hạn cộng ân hạn: chưa quá
        Assert.Equal(OrderStatuses.Pending, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.H.Gateway.Cancelled);

        var handled = await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing));

        Assert.Equal(1, handled);
        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Equal(order.OrderCode, Assert.Single(j.H.Gateway.Cancelled).Code);
        Assert.Empty(j.Store.Cycles);
    }

    [Fact]
    public async Task P11_ExpiredByTheJob_SecondTickChangesNothing_AndDoesNotCancelTheLinkAgain()
    {
        var (j, _, order) = await Pending();
        var later = PastGrace(order, j.H.Billing);
        await j.Jobs.ReconcilePendingOrdersAsync(later);
        j.H.Gateway.StatusRequests.Clear();

        Assert.Equal(0, await j.Jobs.ReconcilePendingOrdersAsync(later.AddMinutes(1)));   // không còn đơn chờ

        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Single(j.H.Gateway.Cancelled);
    }

    // ---- M2: payOS không hỏi được thì chưa coi là hết hạn; đơn hết hạn vẫn được hỏi lại trong cửa sổ trả muộn

    [Fact]
    public async Task M2_GatewayUnreachable_AnOverdueOrderStaysPending_UntilTheHardLimit()
    {
        var (j, _, order) = await Pending();
        j.H.Gateway.FailStatusFor.Add(order.OrderCode);

        await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing));
        await j.Jobs.ReconcilePendingOrdersAsync(order.ExpiresAt.AddHours(j.H.Billing.UnknownStatusExpiryHours));   // đúng hạn cứng: chưa quá
        Assert.Equal(OrderStatuses.Pending, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.H.Gateway.Cancelled);

        await j.Jobs.ReconcilePendingOrdersAsync(order.ExpiresAt.AddHours(j.H.Billing.UnknownStatusExpiryHours).AddSeconds(1));

        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Equal(order.OrderCode, Assert.Single(j.H.Gateway.Cancelled).Code);
    }

    [Fact]
    public async Task M2_GatewayUnreachableThenPaid_TheOrderIsStillActivatedNotExpired()
    {
        var (j, buyer, order) = await Pending();
        j.H.Gateway.FailStatusFor.Add(order.OrderCode);
        await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing));   // payOS sập: giữ nguyên
        j.H.Gateway.FailStatusFor.Clear();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("PAID", order.AmountVnd);

        await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing).AddMinutes(1));

        Assert.Equal(OrderStatuses.Paid, j.H.Order(order.OrderCode).Status);
        Assert.Equal(buyer.Id, Assert.Single(j.Store.Cycles).UserId);
    }

    [Fact]
    public async Task M2_AnExpiredOrderThatPayOsNowSaysIsPaid_IsActivated_C6()
    {
        var (j, buyer, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("EXPIRED", 0);
        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));
        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("PAID", order.AmountVnd);   // khách quét mã trễ

        var handled = await j.Jobs.ReconcilePendingOrdersAsync(Now.AddHours(3));

        Assert.Equal(0, handled);   // đơn hết hạn hỏi lại không tính vào số đơn chờ đã xử lý
        Assert.Equal(OrderStatuses.Paid, j.H.Order(order.OrderCode).Status);
        Assert.Equal(buyer.Id, Assert.Single(j.Store.Cycles).UserId);
        Assert.Single(j.Sent(NotificationTypes.BillingPaymentPaid));
    }

    [Theory]
    [InlineData("EXPIRED")]
    [InlineData("CANCELLED")]
    [InlineData("PENDING")]
    public async Task M2_AnExpiredOrderPayOsDoesNotSayIsPaid_IsLeftAlone(string gatewayStatus)
    {
        var (j, _, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("EXPIRED", 0);
        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(gatewayStatus, 0);
        j.H.Gateway.StatusRequests.Clear();

        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddHours(3));

        Assert.Equal(order.OrderCode, Assert.Single(j.H.Gateway.StatusRequests));   // đã hỏi lại
        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.Store.Cycles);
    }

    [Fact]
    public async Task M2_AnExpiredOrderPayOsCannotBeReached_IsLeftAlone_AndAnOldOneIsNotAskedAnymore()
    {
        var (j, _, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("EXPIRED", 0);
        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));
        j.H.Gateway.FailStatusFor.Add(order.OrderCode);
        j.H.Gateway.StatusRequests.Clear();

        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddHours(3));
        Assert.Single(j.H.Gateway.StatusRequests);
        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);

        j.H.Gateway.StatusRequests.Clear();
        await j.Jobs.ReconcilePendingOrdersAsync(order.ExpiresAt.AddHours(j.H.Billing.UnknownStatusExpiryHours).AddMinutes(1));   // ra ngoài cửa sổ trả muộn

        Assert.Empty(j.H.Gateway.StatusRequests);
    }

    // ---- Bảng ánh xạ trạng thái payOS

    [Theory]
    [InlineData("PAID", null, OrderStatuses.Paid)]
    [InlineData("PAID", 5_000L, OrderStatuses.Mismatch)]
    [InlineData("UNDERPAID", 5_000L, OrderStatuses.Mismatch)]
    [InlineData("UNDERPAID", 79_000L, OrderStatuses.Mismatch)]   // payOS báo thiếu mà số tiền bằng đơn: vẫn không kích hoạt
    [InlineData("CANCELLED", 0L, OrderStatuses.Cancelled)]
    [InlineData("EXPIRED", 0L, OrderStatuses.Expired)]
    [InlineData("FAILED", 0L, OrderStatuses.Expired)]
    [InlineData("PENDING", 0L, OrderStatuses.Pending)]
    [InlineData("PROCESSING", 0L, OrderStatuses.Pending)]
    [InlineData("SOMETHING_NEW", 0L, OrderStatuses.Pending)]
    public async Task Reconcile_MapsEveryGatewayStatus_WithinTheExpiryWindow(string gatewayStatus, long? amountPaid, string expected)
    {
        var (j, _, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(gatewayStatus, amountPaid ?? order.AmountVnd);

        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));

        Assert.Equal(expected, j.H.Order(order.OrderCode).Status);
        Assert.Equal(expected == OrderStatuses.Paid ? 1 : 0, j.Store.Cycles.Count);
        Assert.Empty(j.H.Gateway.Cancelled);   // trong hạn: không đóng link
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PROCESSING")]
    public async Task Reconcile_StillWaitingAtTheGateway_ExpiresOncePastGrace(string gatewayStatus)
    {
        var (j, _, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(gatewayStatus, 0);

        await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing));

        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Single(j.H.Gateway.Cancelled);
    }

    [Fact]
    public async Task Reconcile_Underpaid_NotifiesTheBuyerOnce()
    {
        var (j, buyer, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("UNDERPAID", 5_000);

        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));
        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(2));

        var note = Assert.Single(j.Sent(NotificationTypes.BillingPaymentMismatch));
        Assert.Equal((buyer.Id, $"/orders/{order.OrderCode}"), (note.UserId, note.Link));
    }

    [Fact]
    public async Task Reconcile_PaidAtTheGatewayWithoutAWebhook_ActivatesOnce_ReceiptOnce_P14()
    {
        var (j, buyer, order) = await Pending();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("PAID", order.AmountVnd);

        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));
        await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(2));   // đơn đã trả không còn trong danh sách chờ

        var cycle = Assert.Single(j.Store.Cycles);
        Assert.Equal((buyer.Id, CycleStatuses.Active), (cycle.UserId, cycle.Status));
        Assert.Single(j.H.Email.SentEmails, e => e.Purpose == "payment_receipt");
        Assert.Single(j.Sent(NotificationTypes.BillingPaymentPaid));
    }

    [Fact]
    public async Task Reconcile_AGatewayErrorOnOneOrder_DoesNotStopTheOthers_AndAnOverdueOneIsNotExpiredBlind()
    {
        var j = new JobHarness();
        var (u1, u2, u3, u4) = (j.AddUser(), j.AddUser(), j.AddUser(), j.AddUser());
        var failing = await j.H.CreateOrder(u1, "personal-1m");
        var paid = await j.H.CreateOrder(u2, "personal-1m");
        var overdue = await j.H.CreateOrder(u3, "personal-1m", Now.AddHours(-2));   // đã quá hạn cộng ân hạn từ lâu
        var waiting = await j.H.CreateOrder(u4, "family-1m");
        j.H.Gateway.FailStatusFor.Add(failing.OrderCode);
        j.H.Gateway.FailStatusFor.Add(overdue.OrderCode);
        j.H.Gateway.Statuses[paid.OrderCode] = new GatewayStatus("PAID", paid.AmountVnd);

        var handled = await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));

        Assert.Equal(4, handled);   // lỗi cổng chỉ ghi log, đơn vẫn được xét xong
        Assert.Equal(OrderStatuses.Pending, j.H.Order(failing.OrderCode).Status);   // chưa quá hạn: giữ nguyên
        Assert.Equal(OrderStatuses.Paid, j.H.Order(paid.OrderCode).Status);
        Assert.Equal(OrderStatuses.Pending, j.H.Order(overdue.OrderCode).Status);   // quá hạn nhưng payOS lỗi: chưa biết đã trả chưa nên chờ tới hạn cứng (M2)
        Assert.Equal(OrderStatuses.Pending, j.H.Order(waiting.OrderCode).Status);
    }

    [Fact]
    public async Task Reconcile_AnUnexpectedErrorOnOneOrder_IsLogged_TrackingDropped_AndTheLoopContinues()
    {
        var j = new JobHarness();
        var (u1, u2) = (j.AddUser(), j.AddUser());
        var first = await j.H.CreateOrder(u1, "personal-1m", Now.AddMinutes(-1));
        var second = await j.H.CreateOrder(u2, "personal-1m");
        j.H.Gateway.Statuses[first.OrderCode] = new GatewayStatus("PAID", first.AmountVnd);
        j.H.Gateway.Statuses[second.OrderCode] = new GatewayStatus("PAID", second.AmountVnd);
        j.Store.FailOnCycleInsert = new InvalidOperationException("lưu lỗi");   // lần lưu kỳ đầu tiên hỏng (không phải xung đột nên không thử lại)
        var cleared = j.Store.TrackingCleared;

        var handled = await j.Jobs.ReconcilePendingOrdersAsync(Now.AddMinutes(1));

        Assert.Equal(1, handled);
        Assert.Equal(OrderStatuses.Pending, j.H.Order(first.OrderCode).Status);   // giao dịch quay lui, lần sau thử lại
        Assert.Equal(OrderStatuses.Paid, j.H.Order(second.OrderCode).Status);
        Assert.True(j.Store.TrackingCleared > cleared + 1);
    }

    [Fact]
    public async Task Reconcile_LeavesMismatchOrdersToTheAdmin()
    {
        var (j, _, order) = await Pending();
        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, 1_000, null, Now.AddMinutes(1));
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("CANCELLED", 0);
        j.H.Gateway.StatusRequests.Clear();

        await j.Jobs.ReconcilePendingOrdersAsync(PastGrace(order, j.H.Billing));

        Assert.Equal(OrderStatuses.Mismatch, j.H.Order(order.OrderCode).Status);   // có tiền về rồi: không tự hủy hay hết hạn
        Assert.Empty(j.H.Gateway.StatusRequests);
    }

    [Fact]
    public async Task Reconcile_OrderPaidByAWebhookInTheMeantime_IsNotOverwrittenByAStaleExpiredStatus()
    {
        var (j, _, order) = await Pending();
        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, Now.AddMinutes(1));

        await j.Reconciler.ApplyStatusAsync(order.OrderCode, new GatewayStatus("EXPIRED", 0), Now.AddMinutes(2), null);
        await j.Reconciler.ApplyStatusAsync(order.OrderCode, new GatewayStatus("CANCELLED", 0), Now.AddMinutes(2), null);

        Assert.Equal(OrderStatuses.Paid, j.H.Order(order.OrderCode).Status);
        Assert.Single(j.Store.Cycles);
    }

    // ---- R2: kết thúc kỳ hết hạn, thông báo hết hạn; job và đọc đều chỉ báo một lần

    [Fact]
    public async Task R2_ActiveReachesItsEnd_JobEndsItAtTheEndDate_AndNotifiesOnce()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var active = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-30));   // hết hạn đúng Now

        Assert.Equal(1, await j.Jobs.EndExpiredCyclesAsync(Now.AddMinutes(10)));
        await j.Jobs.EndExpiredCyclesAsync(Now.AddMinutes(11));

        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired, active.EndsAt), (active.Status, active.EndedReason, active.EndedAt));
        var note = Assert.Single(j.Notes.Sent);
        Assert.Equal((owner.Id, NotificationTypes.BillingExpired, "Gói Cá nhân 1 tháng đã hết hạn"), (note.UserId, note.Type, note.Title));
    }

    [Fact]
    public async Task Transition_OneOfTwoCyclesExpires_EndsOnlyThatOne_AndDoesNotNotify()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var old = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-31));
        var running = j.Store.AddActive(owner.Id, "personal-3m", Now.AddDays(-1));

        await j.Jobs.EndExpiredCyclesAsync(Now);

        Assert.Equal((CycleStatuses.Ended, CycleStatuses.Active), (old.Status, running.Status));
        Assert.Empty(j.Notes.Sent);
    }

    [Fact]
    public async Task Transition_ExhaustedCycle_IsNotEndedByTheJob_UntilItsEndDate()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var active = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        j.Store.AddActive(owner.Id, "personal-1m");
        j.Store.Use(owner.Id, active, 1_000);

        Assert.Equal(0, await j.Jobs.EndExpiredCyclesAsync(Now));

        Assert.Equal(CycleStatuses.Active, active.Status);
        Assert.Empty(j.Notes.Sent);
    }

    [Fact]
    public async Task Transition_ExpiryTriggeredByARead_NotifiesOnce()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-31));

        Assert.Equal(AccountTiers.Free, (await j.H.Entitlements.GetSummaryAsync(owner.Id, Now)).Tier);
        await j.H.Entitlements.IsPremiumAsync(owner.Id, UserRoles.Student, Now.AddMinutes(1));
        await j.Jobs.EndExpiredCyclesAsync(Now.AddMinutes(2));

        Assert.Equal(NotificationTypes.BillingExpired, Assert.Single(j.Notes.Sent).Type);
    }

    [Fact]
    public async Task EndExpiredCycles_HealthyCycle_NoChangeNoNotification()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-5));

        Assert.Equal(0, await j.Jobs.EndExpiredCyclesAsync(Now));

        Assert.Equal(CycleStatuses.Active, cycle.Status);
        Assert.Empty(j.Notes.Sent);
    }

    [Fact]
    public async Task EndExpiredCycles_AnOwnerThatFails_DoesNotStopTheOthers()
    {
        var j = new JobHarness();
        var (bad, good) = (j.AddUser(), j.AddUser());
        j.Store.AddActive(bad.Id, "personal-1m", Now.AddDays(-31));
        var goodCycle = j.Store.AddActive(good.Id, "personal-1m", Now.AddDays(-31));
        var cleared = j.Store.TrackingCleared;

        var handled = await j.JobsWith(new ThrowingEntitlements(j.H.Entitlements, bad.Id)).EndExpiredCyclesAsync(Now);

        Assert.Equal(1, handled);
        Assert.Equal(CycleStatuses.Ended, goodCycle.Status);
        Assert.True(j.Store.TrackingCleared > cleared);
    }

    private sealed class ThrowingEntitlements(IEntitlementService inner, Guid failFor) : IEntitlementService
    {
        public Task<IReadOnlyList<ActiveCycle>> EndExpiredCyclesAsync(
            Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default, ICollection<Func<CancellationToken, Task>>? deferredNotifications = null) =>
            ownerUserId == failFor ? throw new InvalidOperationException("lưu lỗi") : inner.EndExpiredCyclesAsync(ownerUserId, kind, nowUtc, ct, deferredNotifications);

        public Task<SubscriptionSummaryDto> GetSummaryAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) => inner.GetSummaryAsync(userId, nowUtc, ct);
        public Task<ChatCharge> ResolveChatChargeAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default) => inner.ResolveChatChargeAsync(userId, role, nowUtc, ct);
        public Task<bool> IsPremiumAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default) => inner.IsPremiumAsync(userId, role, nowUtc, ct);
        public Task<IReadOnlyList<PlanDto>> GetPlansOnSaleAsync(CancellationToken ct = default) => inner.GetPlansOnSaleAsync(ct);
        public Task<IReadOnlyList<ExpiredCycleDto>> GetExpiredCyclesAsync(Guid userId, CancellationToken ct = default) => inner.GetExpiredCyclesAsync(userId, ct);
        public Task<QuotaOverviewDto> GetQuotaOverviewAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) => inner.GetQuotaOverviewAsync(userId, nowUtc, ct);
    }

    // ---- F27: Gia đình hết hạn (không còn kỳ Gia đình nào): thành viên được báo

    [Fact]
    public async Task F27_FamilyEndsWithNoCycleLeft_OwnerAndActiveMembersNotified_PendingInviteEnds_Once()
    {
        var j = new JobHarness();
        var (owner, member) = (j.AddUser(UserRoles.Parent, 40, name: "Chủ Gói"), j.AddUser());
        var cycle = j.Store.AddFamily(owner.Id, member.Id, Now.AddDays(-31));
        var group = j.Store.Groups.Single();
        var invite = new FamilyMember { FamilyGroupId = group.Id, InvitedEmail = "p@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "t" };
        j.Store.Members.Add(invite);

        await j.Jobs.EndExpiredCyclesAsync(Now);
        await j.Jobs.EndExpiredCyclesAsync(Now.AddMinutes(1));

        Assert.Equal(CycleStatuses.Ended, cycle.Status);
        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.FamilyLapsed), (group.Status, group.EndedReason));
        Assert.All(j.Store.Members, m => Assert.Equal(FamilyMemberStatuses.Ended, m.Status));
        Assert.Null(invite.TokenHash);
        var ended = j.Sent(NotificationTypes.FamilyEnded).ToList();
        Assert.Equal(2, ended.Count);   // chủ gói và thành viên đang active; lời mời chưa nhận không có ai để báo
        Assert.Contains(ended, n => n.UserId == owner.Id);
        var memberNote = Assert.Single(ended, n => n.UserId == member.Id);
        Assert.Contains("Chủ Gói", memberNote.Body);
        Assert.Single(j.Sent(NotificationTypes.BillingExpired), n => n.UserId == owner.Id);
    }

    [Fact]
    public async Task F27_FamilyWithAnotherCycleStillRunning_KeepsTheGroup_AndSaysNothing()
    {
        var j = new JobHarness();
        var (owner, member) = (j.AddUser(UserRoles.Parent, 40), j.AddUser());
        var old = j.Store.AddFamily(owner.Id, member.Id, Now.AddDays(-30));
        var running = j.Store.AddFamilyCycle(owner.Id, "family-3m", Now.AddDays(-5));

        await j.Jobs.EndExpiredCyclesAsync(Now.AddMinutes(1));

        Assert.Equal((CycleStatuses.Ended, CycleStatuses.Active), (old.Status, running.Status));
        Assert.Equal(FamilyGroupStatuses.Active, j.Store.Groups.Single().Status);
        Assert.Empty(j.Sent(NotificationTypes.FamilyEnded));
        Assert.Empty(j.Notes.Sent);
    }

    [Fact]
    public async Task F27_ActiveGroupWithoutAnyActiveCycle_IsEndedByTheJob()
    {
        var j = new JobHarness();
        var owner = j.AddUser(UserRoles.Parent, 40);
        j.Store.Groups.Add(new FamilyGroup { OwnerUserId = owner.Id, Status = FamilyGroupStatuses.Active });

        Assert.Equal(1, await j.Jobs.EndExpiredCyclesAsync(Now));

        Assert.Equal(FamilyGroupStatuses.Ended, j.Store.Groups.Single().Status);
        Assert.Equal(owner.Id, Assert.Single(j.Sent(NotificationTypes.FamilyEnded)).UserId);
    }

    // ---- F13: bước hết hạn lời mời

    [Fact]
    public async Task F13_ExpireFamilyInvites_DelegatesToTheFamilyService_AndReturnsItsCount()
    {
        var j = new JobHarness();
        j.Family.ExpiredCount = 3;

        Assert.Equal(3, await j.Jobs.ExpireFamilyInvitesAsync(Now));

        Assert.Equal(Now, Assert.Single(j.Family.ExpireCalls));
    }

    // ---- R8: nhắc gia hạn một lần

    [Fact]
    public async Task R8_SevenDaysBeforeTheEnd_OneEmailAndOneNotification_NotRepeatedOnLaterTicks()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-25));   // còn 5 ngày

        Assert.Equal(1, await j.Jobs.SendRenewalRemindersAsync(Now));
        Assert.Equal(0, await j.Jobs.SendRenewalRemindersAsync(Now.AddMinutes(1)));
        Assert.Equal(0, await j.Jobs.SendRenewalRemindersAsync(Now.AddDays(1)));

        Assert.Equal((owner.Email, "subscription_expiring", "Cá nhân 1 tháng"), Assert.Single(j.H.Email.SentEmails));
        var note = Assert.Single(j.Notes.Sent);
        Assert.Equal((owner.Id, NotificationTypes.BillingExpiring), (note.UserId, note.Type));
        Assert.Equal(Now, cycle.ReminderSentAt);
    }

    [Theory]
    [InlineData(-22, false)]   // còn 8 ngày: chưa tới
    [InlineData(-23, true)]    // còn đúng 7 ngày
    [InlineData(-30, false)]   // vừa hết hạn đúng lúc này: để bước chuyển kỳ lo
    public async Task R8_ReminderWindow_IsAfterNowUpToReminderDays(int startsDaysAgo, bool reminded)
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(startsDaysAgo));

        Assert.Equal(reminded ? 1 : 0, await j.Jobs.SendRenewalRemindersAsync(Now));
    }

    [Fact]
    public async Task R8_EachExpiringCycleIsRemindedOnce_FamilyRemindersGoToTheOwnerOnly()
    {
        var j = new JobHarness();
        var (buyer, owner, member) = (j.AddUser(), j.AddUser(UserRoles.Parent, 40), j.AddUser());
        j.Store.AddActive(buyer.Id, "personal-1m", Now.AddDays(-25));
        j.Store.AddActive(buyer.Id, "personal-1m", Now.AddDays(-26));   // hai kỳ cùng sắp hết: mỗi kỳ một lần nhắc
        j.Store.AddFamily(owner.Id, member.Id, Now.AddDays(-25));

        Assert.Equal(3, await j.Jobs.SendRenewalRemindersAsync(Now));
        Assert.Equal(0, await j.Jobs.SendRenewalRemindersAsync(Now.AddMinutes(1)));

        Assert.Equal(2, j.Notes.Sent.Count(n => n.UserId == buyer.Id));
        Assert.Single(j.Notes.Sent, n => n.UserId == owner.Id);
        Assert.DoesNotContain(j.Notes.Sent, n => n.UserId == member.Id);
        Assert.Single(j.H.Email.SentEmails, e => e.To == owner.Email);
    }

    [Fact]
    public async Task R8_OwnerPendingDeletion_GetsNoReminderButTheCycleIsStillMarked()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        owner.DeletedAt = Now.AddDays(-1);
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-25));

        await j.Jobs.SendRenewalRemindersAsync(Now);

        Assert.Empty(j.H.Email.SentEmails);
        Assert.Empty(j.Notes.Sent);
        Assert.NotNull(cycle.ReminderSentAt);
    }

    // ---- T7 / T8: 80% và 100%

    [Fact]
    public async Task T7_At80Percent_OneNotificationPerCycle()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        j.Store.Use(owner.Id, cycle, 799);
        Assert.Equal(0, await j.Jobs.SendUsageNotificationsAsync(Now));   // 79%: chưa tới

        j.Store.Use(owner.Id, cycle, 1);
        Assert.Equal(1, await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(1)));
        j.Store.Use(owner.Id, cycle, 50);
        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(2));

        var note = Assert.Single(j.Notes.Sent);
        Assert.Equal((owner.Id, NotificationTypes.BillingUsage80, "Bạn đã dùng 80% lượt của kỳ này"), (note.UserId, note.Type, note.Title));
        Assert.Equal(Now.AddMinutes(1), cycle.Warned80At);
        Assert.Null(cycle.ExhaustedNotifiedAt);
    }

    [Fact]
    public async Task T8_AtOrOver100Percent_OneNotificationPerCycle_AfterThe80Warning()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        j.Store.Use(owner.Id, cycle, 850);
        await j.Jobs.SendUsageNotificationsAsync(Now);
        j.Store.Use(owner.Id, cycle, 300);   // lượt cuối vượt quá 100%: tính đủ vào kỳ

        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(1));
        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(2));

        Assert.Equal([NotificationTypes.BillingUsage80, NotificationTypes.BillingUsage100], j.Notes.Sent.Select(n => n.Type));
        Assert.Equal("Bạn đã dùng hết lượt của kỳ này", j.Sent(NotificationTypes.BillingUsage100).Single().Title);
    }

    [Fact]
    public async Task T8_JumpingStraightPastBothThresholds_SendsEachOnceAndNeverAgain()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var cycle = j.Store.AddActive(owner.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        j.Store.Use(owner.Id, cycle, 1_000);

        await j.Jobs.SendUsageNotificationsAsync(Now);
        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(1));

        Assert.Equal(2, j.Notes.Sent.Count);
        Assert.Single(j.Sent(NotificationTypes.BillingUsage80));
        Assert.Single(j.Sent(NotificationTypes.BillingUsage100));
    }

    [Fact]
    public async Task T7_UsageOfAnotherKind_OrZeroQuota_NeverNotifies()
    {
        var j = new JobHarness();
        var (a, b) = (j.AddUser(), j.AddUser());
        var small = j.Store.AddActive(a.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        var free = j.Store.AddActive(b.Id, "personal-1m", Now.AddDays(-2), quota: 0);
        j.Store.Calls.Add(new AiCall { UserId = a.Id, Kind = "slm", TotalTokens = 5_000, UserSubscriptionId = small.Id, PlanCode = "personal-1m" });   // SLM không tính
        j.Store.Use(b.Id, free, 10);

        Assert.Equal(0, await j.Jobs.SendUsageNotificationsAsync(Now));

        Assert.Empty(j.Notes.Sent);
    }

    [Fact]
    public async Task T7_FamilyCycle_NotifiesTheOwnerAndActiveMembers_NotPendingInvites_AndEachOnlyOnce()
    {
        var j = new JobHarness();
        var (owner, member) = (j.AddUser(UserRoles.Parent, 40), j.AddUser());
        var cycle = j.Store.AddFamily(owner.Id, member.Id, Now.AddDays(-2));
        j.Store.Members.Add(new FamilyMember { FamilyGroupId = cycle.FamilyGroupId!.Value, InvitedEmail = "p@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending });
        j.Store.Use(owner.Id, cycle, 420_000);   // 84% của 500 000

        await j.Jobs.SendUsageNotificationsAsync(Now);
        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(1));

        Assert.Equal(new[] { owner.Id, member.Id }.Order(), j.Sent(NotificationTypes.BillingUsage80).Select(n => n.UserId).Order());
    }

    [Fact]
    public async Task RecurringCycle_Notifies80And100PerWindow_AndNeverEndsEarlyWhenExhausted()
    {
        var j = new JobHarness();
        var owner = j.AddUser();
        var start = Now.AddDays(-35);   // đang ở cửa sổ 1 (ngày 30..60) của gói 90d/30d
        var cycle = j.Store.AddActive(owner.Id, "personal-3m", start, quota: 1_000);
        // Cửa sổ 0 đã cảnh báo và dùng hết
        j.Store.Use(owner.Id, cycle, 1_000, start.AddDays(5));
        cycle.Warned80At = start.AddDays(4);
        cycle.ExhaustedNotifiedAt = start.AddDays(5);

        // Cửa sổ 1: chưa dùng gì -> không báo
        await j.Jobs.EndExpiredCyclesAsync(Now);
        Assert.Equal(CycleStatuses.Active, cycle.Status);
        Assert.Empty(j.Notes.Sent);
        Assert.Equal(0, await j.Jobs.SendUsageNotificationsAsync(Now));

        // Cửa sổ 1 dùng hết -> báo 80% và 100% lại cho cửa sổ 1; kỳ vẫn chạy
        j.Store.Use(owner.Id, cycle, 1_000, Now.AddMinutes(-1));
        Assert.Equal(1, await j.Jobs.SendUsageNotificationsAsync(Now));
        await j.Jobs.EndExpiredCyclesAsync(Now);
        Assert.Equal(CycleStatuses.Active, cycle.Status);
        Assert.Single(j.Sent(NotificationTypes.BillingUsage80));
        Assert.Single(j.Sent(NotificationTypes.BillingUsage100));

        // Cửa sổ cuối (ngày 65) dùng hết -> vẫn không kết thúc sớm: chạy tới ngày hết hạn (ngày 90)
        var finalNow = start.AddDays(65);
        j.Store.Use(owner.Id, cycle, 1_000, finalNow.AddMinutes(-1));
        await j.Jobs.EndExpiredCyclesAsync(finalNow);
        Assert.Equal(CycleStatuses.Active, cycle.Status);
        await j.Jobs.EndExpiredCyclesAsync(start.AddDays(90));
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired), (cycle.Status, cycle.EndedReason));
    }

    // Mục lỗi làm bước dừng (lỗi lưu / gửi thường là sự cố chung); lượt sau nạp lại và làm tiếp.
    // Quan trọng: kỳ sau không được gửi thông báo khi dấu chưa lưu, nếu không lượt sau sẽ gửi lặp.
    [Fact]
    public async Task Usage_AFailingItem_StopsTheStep_TheRestIsDoneOnTheNextTick_EachOnce()
    {
        var j = new JobHarness();
        var (a, b) = (j.AddUser(), j.AddUser());
        var first = j.Store.AddActive(a.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        var second = j.Store.AddActive(b.Id, "personal-1m", Now.AddDays(-2), quota: 1_000);
        j.Store.Use(a.Id, first, 900);
        j.Store.Use(b.Id, second, 900);
        j.Notes.Throw = true;   // dịch vụ thật không bao giờ ném; ở đây thay cho lỗi lưu

        Assert.Equal(0, await j.Jobs.SendUsageNotificationsAsync(Now));

        Assert.NotNull(first.Warned80At);   // ghi dấu trước khi gửi: mất một lần báo, không bao giờ báo lặp
        Assert.Null(second.Warned80At);     // chưa xét tới: không dấu, không gửi
        j.Notes.Throw = false;
        Assert.Equal(1, await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(1)));
        await j.Jobs.SendUsageNotificationsAsync(Now.AddMinutes(2));

        Assert.Equal(b.Id, Assert.Single(j.Sent(NotificationTypes.BillingUsage80)).UserId);
    }

    [Fact]
    public async Task R8_AFailingItem_StopsTheStep_TheRestIsRemindedOnTheNextTick_EachOnce()
    {
        var j = new JobHarness();
        var (a, b) = (j.AddUser(), j.AddUser());
        var first = j.Store.AddActive(a.Id, "personal-1m", Now.AddDays(-25));
        var second = j.Store.AddActive(b.Id, "personal-1m", Now.AddDays(-25));
        j.Notes.Throw = true;

        Assert.Equal(0, await j.Jobs.SendRenewalRemindersAsync(Now));

        Assert.NotNull(first.ReminderSentAt);
        Assert.Null(second.ReminderSentAt);
        Assert.Single(j.H.Email.SentEmails);   // chỉ kỳ đầu đã gửi email; kỳ sau chưa
        j.Notes.Throw = false;
        Assert.Equal(1, await j.Jobs.SendRenewalRemindersAsync(Now.AddMinutes(1)));
        await j.Jobs.SendRenewalRemindersAsync(Now.AddMinutes(2));

        Assert.Equal(2, j.H.Email.SentEmails.Count);
        Assert.Equal(b.Id, Assert.Single(j.Notes.Sent).UserId);
    }

    // ---- BillingBackgroundService: mỗi bước một scope, lỗi một bước không chặn các bước sau

    private sealed class RecordingJob(string? failingStep = null) : IBillingJobService
    {
        public List<string> Steps { get; } = [];

        private Task<int> Run(string step)
        {
            Steps.Add(step);
            return step == failingStep ? throw new InvalidOperationException("bước lỗi") : Task.FromResult(0);
        }

        public Task<int> ReconcilePendingOrdersAsync(DateTime nowUtc, CancellationToken ct = default) => Run("reconcile");
        public Task<int> EndExpiredCyclesAsync(DateTime nowUtc, CancellationToken ct = default) => Run("advance");
        public Task<int> ExpireFamilyInvitesAsync(DateTime nowUtc, CancellationToken ct = default) => Run("invites");
        public Task<int> SendRenewalRemindersAsync(DateTime nowUtc, CancellationToken ct = default) => Run("reminders");
        public Task<int> SendUsageNotificationsAsync(DateTime nowUtc, CancellationToken ct = default) => Run("usage");
    }

    private static BillingBackgroundService Service(RecordingJob job, int intervalSeconds = 1, Action? onScope = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IBillingJobService>(_ =>
        {
            onScope?.Invoke();
            return job;
        });
        return new BillingBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new BillingOptions { JobIntervalSeconds = intervalSeconds },
            NullLogger<BillingBackgroundService>.Instance);
    }

    [Fact]
    public async Task Tick_RunsTheFiveStepsInOrder_EachInItsOwnScope()
    {
        var job = new RecordingJob();
        var scopes = 0;

        await Service(job, onScope: () => scopes++).RunTickAsync(Now, default);

        Assert.Equal(["reconcile", "advance", "invites", "reminders", "usage"], job.Steps);
        Assert.Equal(5, scopes);
    }

    [Theory]
    [InlineData("reconcile")]
    [InlineData("advance")]
    [InlineData("invites")]
    [InlineData("reminders")]
    public async Task Tick_AFailingStep_IsLogged_AndTheLaterStepsStillRun(string failing)
    {
        var job = new RecordingJob(failing);

        await Service(job).RunTickAsync(Now, default);

        Assert.Equal(["reconcile", "advance", "invites", "reminders", "usage"], job.Steps);
    }

    [Fact]
    public async Task Service_RunsTicksOnTheInterval_AndStopsCleanlyWhenCancelled()
    {
        var job = new RecordingJob();
        var service = Service(job);

        await service.StartAsync(default);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (job.Steps.Count < 5 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        await service.StopAsync(default);

        Assert.True(job.Steps.Count >= 5);
        Assert.Equal("reconcile", job.Steps[0]);
    }

    [Fact]
    public async Task Service_ZeroInterval_IsClampedInsteadOfCrashing()
    {
        var service = Service(new RecordingJob(), intervalSeconds: 0);

        await service.StartAsync(default);
        await service.StopAsync(default);
    }
}
