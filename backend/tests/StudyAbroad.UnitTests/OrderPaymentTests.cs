using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Áp dụng thanh toán và webhook: đường tiền (P1, P12, P14–P18, P22, R1, R4, R5, R10, F1, F26, F38/C15). Tạo đơn ở OrderServiceTests.
public class OrderPaymentTests
{
    private static readonly DateTime Now = OrderHarness.Now;
    private static readonly DateTime PayAt = Now.AddMinutes(1);

    private static async Task<(OrderHarness H, User Buyer, OrderDto Order)> Ordered(string plan = "personal-1m", string role = UserRoles.Student, int age = 17)
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(role, age);
        return (h, buyer, await h.CreateOrder(buyer, plan));
    }

    private static Task<PaymentApplyResult> Pay(OrderHarness h, OrderDto order, long? amount = null, string? payload = null, PaymentApplyOptions? options = null) =>
        h.Orders.ApplyPaymentAsync(order.OrderCode, amount ?? order.AmountVnd, payload, PayAt, default, options);

    private static UserSubscription CycleOf(OrderHarness h, OrderDto order) =>
        h.Store.Cycles.Single(c => c.Id == h.Order(order.OrderCode).UserSubscriptionId);

    // ---- P1: kích hoạt ngay

    [Fact]
    public async Task P1_Pay_ActivatesTheCycleNow_WithSnapshotsFromTheOrder()
    {
        var (h, buyer, order) = await Ordered();

        var result = await Pay(h, order, payload: "{\"x\":1}");

        var cycle = Assert.Single(h.Store.Cycles);
        Assert.Equal(new PaymentApplyResult(PaymentApplyStatus.Activated, cycle.Id), result);
        Assert.Equal((buyer.Id, "personal", CycleStatuses.Active, (DateTime?)PayAt, (DateTime?)PayAt.AddDays(30)),
            (cycle.UserId, cycle.Kind, cycle.Status, cycle.StartsAt, cycle.EndsAt));
        Assert.Equal((h.Store.Plan("personal-1m").Id, 500_000L, "Cá nhân 1 tháng", 79_000m, 30, (Guid?)null),
            (cycle.SubscriptionPlanId, cycle.TokenQuota, cycle.PlanName, cycle.PriceVnd, cycle.DurationDays, cycle.FamilyGroupId));
        var stored = h.Order(order.OrderCode);
        Assert.Equal((OrderStatuses.Paid, (DateTime?)PayAt, (Guid?)cycle.Id, "{\"x\":1}"), (stored.Status, stored.PaidAt, stored.UserSubscriptionId, stored.PayloadJson));
        Assert.Equal(stored.Id, cycle.ActivatedFromOrderId);
        Assert.Contains(order.OrderCode, h.Store.LockedCodes);   // khóa dòng đơn trong transaction
        Assert.Equal(1, h.Store.Transactions);
        Assert.True(h.Store.TrackingCleared >= 1);
    }

    [Fact]
    public async Task P1_PaidAccountIsPremium_ReceiptAndNotificationSent()
    {
        var (h, buyer, order) = await Ordered();

        await Pay(h, order);

        Assert.Equal(AccountTiers.Premium, (await h.Entitlements.GetSummaryAsync(buyer.Id, PayAt)).Tier);
        Assert.Equal((buyer.Email, "payment_receipt", order.OrderCode.ToString()), Assert.Single(h.Email.SentEmails));
        var note = Assert.Single(h.Notes.Sent);
        Assert.Equal((buyer.Id, NotificationTypes.BillingPaymentPaid, "Thanh toán thành công", $"/orders/{order.OrderCode}"), (note.UserId, note.Type, note.Title, note.Link));
        Assert.Contains("đã được kích hoạt", note.Body);
    }

    [Fact]
    public async Task P12_TheOrdersBuyerGetsThePlan_NoOneElse()
    {
        var (h, buyer, order) = await Ordered();
        var bystander = h.AddUser();

        await Pay(h, order);   // ai quét mã trả tiền cũng vậy: gói thuộc người tạo đơn

        Assert.Equal(buyer.Id, Assert.Single(h.Store.Cycles).UserId);
        Assert.Equal(AccountTiers.Free, (await h.Entitlements.GetSummaryAsync(bystander.Id, PayAt)).Tier);
    }

    [Fact]
    public async Task P21_ActivationHappensInsideTheWebhookCall()
    {
        var (h, _, order) = await Ordered();

        Assert.True(await h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode)), PayAt));

        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);   // không chờ job
    }

    // ---- P14: lặp lại

    [Fact]
    public async Task P14_SecondApply_IsAlreadyPaid_OneCycleOneReceipt()
    {
        var (h, _, order) = await Ordered();
        var first = await Pay(h, order);

        var second = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt.AddSeconds(30));

        Assert.Equal(new PaymentApplyResult(PaymentApplyStatus.AlreadyPaid, first.CycleId), second);
        Assert.Single(h.Store.Cycles);
        Assert.Single(h.Email.SentEmails);
        Assert.Single(h.Notes.Sent);
        Assert.Equal(PayAt, h.Order(order.OrderCode).PaidAt);
    }

    [Fact]
    public async Task P14_WebhookRepeated_OneCycleOneReceipt()
    {
        var (h, _, order) = await Ordered();
        var body = h.WebhookBody(h.Order(order.OrderCode));

        Assert.True(await h.Orders.HandleWebhookAsync(body, PayAt));
        Assert.True(await h.Orders.HandleWebhookAsync(body, PayAt.AddSeconds(5)));
        Assert.True(await h.Orders.HandleWebhookAsync(body, PayAt.AddSeconds(9)));

        Assert.Single(h.Store.Cycles);
        Assert.Single(h.Email.SentEmails);
    }

    [Fact]
    public async Task P14_WebhookThenReconcileJob_StillOneCycle()
    {
        var (h, _, order) = await Ordered();
        await h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode)), PayAt);

        var job = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt.AddSeconds(20));

        Assert.Equal(PaymentApplyStatus.AlreadyPaid, job.Status);
        Assert.Single(h.Store.Cycles);
    }

    // ---- P15: sai số tiền

    [Theory]
    [InlineData(78_999L)]
    [InlineData(79_001L)]
    [InlineData(158_000L)]
    [InlineData(0L)]
    public async Task P15_WrongAmount_Mismatch_NoCycle_BuyerToldAdminWillHandle(long paid)
    {
        var (h, buyer, order) = await Ordered();

        var result = await Pay(h, order, paid, "{\"p\":1}");

        Assert.Equal(new PaymentApplyResult(PaymentApplyStatus.Mismatch), result);
        Assert.Empty(h.Store.Cycles);
        var stored = h.Order(order.OrderCode);
        Assert.Equal((OrderStatuses.Mismatch, "{\"p\":1}", (DateTime?)null, (Guid?)null), (stored.Status, stored.PayloadJson, stored.PaidAt, stored.UserSubscriptionId));
        var note = Assert.Single(h.Notes.Sent);
        Assert.Equal((buyer.Id, NotificationTypes.BillingPaymentMismatch, $"/orders/{order.OrderCode}"), (note.UserId, note.Type, note.Link));
        Assert.Contains("quản trị viên sẽ xử lý", note.Body);
        Assert.Empty(h.Email.SentEmails);
        Assert.Equal(AccountTiers.Free, (await h.Entitlements.GetSummaryAsync(buyer.Id, PayAt)).Tier);
    }

    [Fact]
    public async Task P15_WebhookWithWrongAmount_Mismatch_AndRepeatsDoNotNotifyAgain()
    {
        var (h, _, order) = await Ordered();
        var body = h.WebhookBody(h.Order(order.OrderCode), amount: 50_000);

        Assert.True(await h.Orders.HandleWebhookAsync(body, PayAt));
        Assert.True(await h.Orders.HandleWebhookAsync(body, PayAt.AddSeconds(5)));

        Assert.Equal(OrderStatuses.Mismatch, h.Order(order.OrderCode).Status);
        Assert.Empty(h.Store.Cycles);
        Assert.Single(h.Notes.Sent);
    }

    // ---- P16: trả muộn

    [Theory]
    [InlineData(OrderStatuses.Expired)]
    [InlineData(OrderStatuses.Cancelled)]
    public async Task P16_CorrectPaymentOnExpiredOrCancelledOrder_StillActivates(string status)
    {
        var (h, buyer, order) = await Ordered();
        h.Order(order.OrderCode).Status = status;

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);
        Assert.Equal(buyer.Id, Assert.Single(h.Store.Cycles).UserId);
    }

    // ---- P17, P18: webhook

    [Fact]
    public async Task P17_InvalidSignature_ReturnsFalse_NothingChanges()
    {
        var (h, _, order) = await Ordered();
        var stored = h.Order(order.OrderCode);

        Assert.False(await h.Orders.HandleWebhookAsync(h.WebhookBody(stored, signingKey: "kẻ-gian"), PayAt));
        Assert.False(await h.Orders.HandleWebhookAsync(h.WebhookBody(stored).Replace("\"amount\":79000", "\"amount\":1"), PayAt));   // sửa số tiền sau khi ký
        Assert.False(await h.Orders.HandleWebhookAsync("không phải json", PayAt));
        Assert.False(await h.Orders.HandleWebhookAsync("", PayAt));

        Assert.Equal(OrderStatuses.Pending, stored.Status);
        Assert.Empty(h.Store.Cycles);
        Assert.Empty(h.Notes.Sent);
        Assert.Equal(0, h.Store.Transactions);
    }

    [Fact]
    public async Task P18_WebhookForAnOrderWeDoNotKnow_ReturnsTrue_NothingChanges()
    {
        var h = new OrderHarness();
        var stranger = new PaymentTransaction { OrderCode = 123, AmountVnd = 2000, Description = "VQRIO123" };

        Assert.True(await h.Orders.HandleWebhookAsync(h.WebhookBody(stranger), PayAt));

        Assert.Empty(h.Store.Orders);
        Assert.Empty(h.Store.Cycles);
        Assert.Empty(h.Notes.Sent);
    }

    [Fact]
    public async Task UnknownOrder_ApplyPayment_ReportsUnknownOrder()
    {
        var h = new OrderHarness();

        Assert.Equal(new PaymentApplyResult(PaymentApplyStatus.UnknownOrder), await h.Orders.ApplyPaymentAsync(999, 1, null, PayAt));
    }

    [Theory]
    [InlineData("01", null)]
    [InlineData("00", "01")]    // data.code là mã thật vì được ký; mã ngoài không ký nên không thể biến thất bại thành thành công
    [InlineData("01", "00")]
    public async Task FailedPaymentCode_ReturnsTrue_AndDoesNothing(string dataCode, string? outerCode)
    {
        var (h, _, order) = await Ordered();

        Assert.True(await h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode), code: dataCode, outerCode: outerCode), PayAt));

        Assert.Equal(OrderStatuses.Pending, h.Order(order.OrderCode).Status);
        Assert.Empty(h.Store.Cycles);
        Assert.Equal(0, h.Store.Transactions);
    }

    [Fact]
    public async Task Webhook_StoresTheSanitizedPayload_WithoutCounterAccountFields()
    {
        var (h, _, order) = await Ordered();

        await h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode)), PayAt);

        using var payload = JsonDocument.Parse(h.Order(order.OrderCode).PayloadJson!);
        var names = payload.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("orderCode", names);
        Assert.Contains("reference", names);
        Assert.DoesNotContain(names, n => n.StartsWith("counterAccount") || n.StartsWith("virtualAccount"));
        Assert.DoesNotContain("NGUYEN VAN A", h.Order(order.OrderCode).PayloadJson);
    }

    // ---- P22 và kích hoạt của admin

    [Fact]
    public async Task P22_LaneFull_MoneyReceivedButNoSlot_MismatchWithLaneFullNote()
    {
        var (h, buyer, order) = await Ordered();
        h.Store.AddActive(buyer.Id, "personal-1m", startsAt: PayAt.AddDays(-2));   // sau khi đặt đơn, kỳ chạy và kỳ chờ cùng xuất hiện
        h.Store.AddQueued(buyer.Id, "personal-3m");

        var result = await Pay(h, order);

        Assert.Equal(new PaymentApplyResult(PaymentApplyStatus.LaneFull), result);
        var stored = h.Order(order.OrderCode);
        Assert.Equal((OrderStatuses.Mismatch, "lane_full: đã có kỳ đang chạy và kỳ chờ"), (stored.Status, stored.AdminNote));
        Assert.Equal(2, h.Store.Cycles.Count);
        var note = Assert.Single(h.Notes.Sent);
        Assert.Equal((buyer.Id, NotificationTypes.BillingPaymentMismatch, $"/orders/{order.OrderCode}"), (note.UserId, note.Type, note.Link));
        Assert.Empty(h.Email.SentEmails);
    }

    [Fact]
    public async Task P22_AdminActivatesTheLaneFullOrderOnceTheLaneHasRoom()
    {
        var (h, buyer, order) = await Ordered();
        var active = h.Store.AddActive(buyer.Id, "personal-1m", startsAt: PayAt.AddDays(-2));
        var queued = h.Store.AddQueued(buyer.Id, "personal-3m");
        await Pay(h, order);
        queued.Status = CycleStatuses.Ended;   // kỳ chờ đã chạy xong / được xử lý
        active.Status = CycleStatuses.Ended;
        var admin = Guid.NewGuid();

        var result = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt.AddHours(1), default,
            new PaymentApplyOptions(SkipAmountCheck: true, AdminUserId: admin, AdminNote: "đã đối chiếu sao kê"));

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        var stored = h.Order(order.OrderCode);
        Assert.Equal((OrderStatuses.Paid, "đã đối chiếu sao kê", (Guid?)admin), (stored.Status, stored.AdminNote, stored.HandledByUserId));
    }

    [Fact]
    public async Task Admin_SkipAmountCheck_ActivatesAnOrderThatWasMismatch()
    {
        var (h, _, order) = await Ordered();
        await Pay(h, order, 50_000, "{\"a\":1}");
        var admin = Guid.NewGuid();

        var result = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt.AddHours(2), default,
            new PaymentApplyOptions(true, admin, "khách chuyển thiếu, đã bù"));

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        var stored = h.Order(order.OrderCode);
        Assert.Equal((OrderStatuses.Paid, (Guid?)admin, "{\"a\":1}"), (stored.Status, stored.HandledByUserId, stored.PayloadJson));   // payload cũ được giữ
        Assert.Single(h.Store.Cycles);
        Assert.Single(h.Email.SentEmails);
    }

    [Fact]
    public async Task SkipAmountCheck_IgnoresTheAmountEntirely()
    {
        var (h, _, order) = await Ordered();

        var result = await Pay(h, order, 1, options: new PaymentApplyOptions(SkipAmountCheck: true));

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
    }

    // ---- R1, R4, R5: đặt chỗ lúc thanh toán

    [Fact]
    public async Task R1_RenewalWhileActiveWithTokensLeft_Queues_WithNoDates()
    {
        var (h, buyer, order) = await Ordered("personal-3m");
        var active = h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-5));
        h.Store.Use(buyer.Id, active, 100_000);

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Queued, result.Status);
        var queued = h.Store.Cycles.Single(c => c.Id == result.CycleId);
        Assert.Equal((CycleStatuses.Queued, (DateTime?)null, (DateTime?)null, 90, 1_800_000L), (queued.Status, queued.StartsAt, queued.EndsAt, queued.DurationDays, queued.TokenQuota));
        Assert.Equal(CycleStatuses.Active, active.Status);
        Assert.Contains("bắt đầu khi kỳ hiện tại kết thúc", Assert.Single(h.Notes.Sent).Body);
        Assert.Single(h.Email.SentEmails);
        var summary = await h.Entitlements.GetSummaryAsync(buyer.Id, PayAt);
        Assert.Equal(("Cá nhân 3 tháng", active.EndsAt), (summary.Personal!.Queued!.PlanName, summary.Personal.Queued.ExpectedStartsAt));
    }

    [Fact]
    public async Task R1_TheQueuedCycleStartsExactlyWhenTheActiveOneEnds()
    {
        var (h, buyer, order) = await Ordered("personal-3m");
        var active = h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-5));
        await Pay(h, order);

        await h.Entitlements.GetSummaryAsync(buyer.Id, active.EndsAt!.Value.AddHours(1));

        var started = h.Store.Cycles.Single(c => c.ActivatedFromOrderId == h.Order(order.OrderCode).Id);
        Assert.Equal((CycleStatuses.Active, active.EndsAt, active.EndsAt!.Value.AddDays(90)), (started.Status, started.StartsAt, started.EndsAt));
    }

    [Fact]
    public async Task R4_PayWhileActiveIsExhausted_OldEndsNow_NewStartsNow()
    {
        var (h, buyer, order) = await Ordered("personal-3m");
        var old = h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-5));
        h.Store.Use(buyer.Id, old, 500_000);

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.ExhaustedRollover, (DateTime?)PayAt, (DateTime?)PayAt), (old.Status, old.EndedReason, old.EndedAt, old.EndsAt));
        var created = h.Store.Cycles.Single(c => c.Id == result.CycleId);
        Assert.Equal((CycleStatuses.Active, (DateTime?)PayAt, (DateTime?)PayAt.AddDays(90)), (created.Status, created.StartsAt, created.EndsAt));
        Assert.Equal(2, h.Store.Cycles.Count);
        Assert.Contains("đã được kích hoạt", Assert.Single(h.Notes.Sent).Body);
    }

    [Fact]
    public async Task R5_RenewsAfterExpiry_StartsNow_OldCycleEndedAsExpired()
    {
        var (h, buyer, order) = await Ordered();
        var old = h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-40));

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired), (old.Status, old.EndedReason));
        var created = h.Store.Cycles.Single(c => c.Id == result.CycleId);
        Assert.Equal((CycleStatuses.Active, (DateTime?)PayAt), (created.Status, created.StartsAt));
    }

    // ---- R10: hai kích hoạt đua nhau trên một làn

    [Fact]
    public async Task R10_RaceOnTheLane_TheLoserRetriesOnceAndQueuesBehindTheWinner()
    {
        var (h, buyer, order) = await Ordered();
        // Giữa lúc đọc làn (trống) và lúc ghi, một request khác đã commit một kỳ active cho cùng làn
        h.Store.RaceBeforeCycleInsert = s => s.AddActive(buyer.Id, "personal-3m", startsAt: PayAt);

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Queued, result.Status);
        Assert.Equal(2, h.Store.Transactions);       // lần đầu bị quay lui, lần hai thành công
        Assert.True(h.Store.TrackingCleared >= 2);   // mỗi lần thử đọc lại từ đầu
        Assert.Equal([CycleStatuses.Active, CycleStatuses.Queued], h.Store.Cycles.Select(c => c.Status).OrderBy(s => s));
        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);
        Assert.Equal(h.Order(order.OrderCode).UserSubscriptionId, h.Store.Cycles.Single(c => c.Status == CycleStatuses.Queued).Id);
        // Lần thử bị hủy không để lại biên nhận hay thông báo
        Assert.Single(h.Email.SentEmails);
        Assert.Single(h.Notes.Sent);
    }

    [Fact]
    public async Task R10_RaceThatPersists_IsRetriedOnlyOnce_ThenFails_AndTheOrderStaysPending()
    {
        var (h, buyer, order) = await Ordered();
        h.Store.AlwaysConflictOnCycleInsert = true;

        await Assert.ThrowsAsync<UniqueViolationException>(() => Pay(h, order));

        Assert.Equal(2, h.Store.Transactions);
        Assert.Equal(OrderStatuses.Pending, h.Order(order.OrderCode).Status);
        Assert.Empty(h.Store.Cycles);
        Assert.Empty(h.Email.SentEmails);
        Assert.Empty(h.Notes.Sent);
        Assert.Equal(AccountTiers.Free, (await h.Entitlements.GetSummaryAsync(buyer.Id, PayAt)).Tier);
    }

    [Fact]
    public async Task OtherErrors_AreNotRetried_AndRollBackEverything()
    {
        var (h, _, order) = await Ordered();
        h.Store.FailOnCycleInsert = new InvalidOperationException("DB sập");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(h, order));

        Assert.Equal(1, h.Store.Transactions);
        Assert.Equal(OrderStatuses.Pending, h.Order(order.OrderCode).Status);
        Assert.Empty(h.Store.Cycles);
    }

    [Fact]
    public async Task A_PaymentAppliedAfterAFailedAttempt_CanBeRetriedByTheWebhookSender()
    {
        var (h, _, order) = await Ordered();
        h.Store.FailOnCycleInsert = new InvalidOperationException("DB sập");
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode)), PayAt));

        // payOS gửi lại webhook (vì nhận 500): lần này xử lý được, đúng một kỳ
        Assert.True(await h.Orders.HandleWebhookAsync(h.WebhookBody(h.Order(order.OrderCode)), PayAt.AddMinutes(1)));

        Assert.Single(h.Store.Cycles);
        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);
    }

    // ---- bản chụp (P7, P8)

    [Fact]
    public async Task Snapshot_PlanChangedOrTakenOffSaleAfterOrdering_TheCycleStillUsesTheOrder()
    {
        var (h, _, order) = await Ordered();
        var plan = h.Store.Plan("personal-1m");
        (plan.TokenQuota, plan.PriceVnd, plan.DurationDays, plan.Name, plan.IsActive) = (1, 1_000_000, 90, "Tên mới", false);

        var result = await Pay(h, order, 79_000);   // vẫn trả đúng giá lúc đặt (P7, P8)

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        var cycle = Assert.Single(h.Store.Cycles);
        Assert.Equal((500_000L, "Cá nhân 1 tháng", 79_000m, 30, (DateTime?)PayAt.AddDays(30)), (cycle.TokenQuota, cycle.PlanName, cycle.PriceVnd, cycle.DurationDays, cycle.EndsAt));
    }

    // ---- gói Gia đình

    [Fact]
    public async Task F1_BuyingFamily_CreatesTheGroupAndTheCycleTogether_OwnerIsPremium()
    {
        var (h, buyer, order) = await Ordered("family-1m", UserRoles.Parent, 40);

        var result = await Pay(h, order);

        var group = Assert.Single(h.Store.Groups);
        var cycle = Assert.Single(h.Store.Cycles);
        Assert.Equal((buyer.Id, FamilyGroupStatuses.Active), (group.OwnerUserId, group.Status));
        Assert.Equal((PaymentApplyStatus.Activated, (Guid?)group.Id, PlanKinds.Family), (result.Status, cycle.FamilyGroupId, cycle.Kind));
        Assert.Empty(h.Store.Members);
        var summary = await h.Entitlements.GetSummaryAsync(buyer.Id, PayAt);
        Assert.Equal((AccountTiers.Premium, "owner"), (summary.Tier, summary.Family!.Role));
    }

    [Fact]
    public async Task F26_FamilyRenewal_QueuesOnTheOwnersExistingGroup_MembersCarryOver()
    {
        var (h, owner, order) = await Ordered("family-3m", UserRoles.Parent, 40);
        var member = h.AddUser();
        var current = h.Store.AddFamily(owner.Id, member: member.Id, startsAt: Now.AddDays(-5));
        var group = h.Store.Groups.Single();

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Queued, result.Status);
        var queued = h.Store.Cycles.Single(c => c.Id == result.CycleId);
        Assert.Equal((CycleStatuses.Queued, (Guid?)group.Id), (queued.Status, queued.FamilyGroupId));
        Assert.Single(h.Store.Groups);   // không tạo nhóm thứ hai
        Assert.Equal(FamilyMemberStatuses.Active, h.Store.Members.Single().Status);

        // Hết kỳ cũ: kỳ gia hạn chạy, thành viên vẫn ở trong nhóm và vẫn Premium
        var later = current.EndsAt!.Value.AddHours(1);
        Assert.Equal(AccountTiers.Premium, (await h.Entitlements.GetSummaryAsync(member.Id, later)).Tier);
        Assert.Equal(CycleStatuses.Active, queued.Status);
        Assert.Equal(FamilyMemberStatuses.Active, h.Store.Members.Single().Status);
    }

    [Fact]
    public async Task SW9_F28_BuyingFamilyAgainAfterALapse_CreatesANewEmptyGroup()
    {
        var (h, owner, order) = await Ordered("family-1m", UserRoles.Parent, 40);
        var member = h.AddUser();
        h.Store.AddFamily(owner.Id, member: member.Id, startsAt: Now.AddDays(-40));   // kỳ đã hết hạn, nhóm cũ chưa ai đọc nên còn "active"
        var oldGroup = h.Store.Groups.Single();
        h.Store.Members.Add(new FamilyMember
        {
            FamilyGroupId = oldGroup.Id, InvitedEmail = "p@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "tok",
        });

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.FamilyLapsed), (oldGroup.Status, oldGroup.EndedReason));
        Assert.All(h.Store.Members, m => Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.FamilyLapsed, (string?)null), (m.Status, m.EndedReason, m.TokenHash)));
        var newGroup = Assert.Single(h.Store.Groups, g => g.Status == FamilyGroupStatuses.Active);
        Assert.NotEqual(oldGroup.Id, newGroup.Id);
        Assert.Equal(newGroup.Id, h.Store.Cycles.Single(c => c.Id == result.CycleId).FamilyGroupId);
        Assert.DoesNotContain(h.Store.Members, m => m.FamilyGroupId == newGroup.Id);   // nhóm mới trống, chủ mời lại
    }

    [Fact]
    public async Task F38_C15_AMemberWhoPaysForFamilyAnyway_LeavesTheOtherGroup_BothOwnersNotified()
    {
        var h = new OrderHarness();
        var otherOwner = h.AddUser(UserRoles.Parent, 45, name: "Chủ nhóm cũ");
        var member = h.AddUser(UserRoles.Parent, 40, name: "Người mua");
        h.Store.AddFamily(otherOwner.Id);
        var otherGroup = h.Store.Groups.Single();
        h.Store.Members.Add(new FamilyMember
        {
            FamilyGroupId = otherGroup.Id, UserId = member.Id, InvitedEmail = member.Email, SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "tok",
        });
        var order = await h.CreateOrder(member, "family-1m");   // đơn tạo lúc còn chưa là thành viên (đua với lời mời, F37)
        var membership = h.Store.Members.Single();
        membership.Status = FamilyMemberStatuses.Active;         // chấp nhận lời mời sau đó
        membership.TokenHash = "kept";
        var bystander = h.AddUser(UserRoles.Student, 17);
        h.Store.Members.Add(new FamilyMember
        {
            FamilyGroupId = otherGroup.Id, UserId = bystander.Id, InvitedEmail = bystander.Email, SeatRole = FamilySeats.Student, Status = FamilyMemberStatuses.Active,
        });

        var result = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, PayAt);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.JoinedOwnFamily, (DateTime?)PayAt, (string?)null),
            (membership.Status, membership.EndedReason, membership.EndedAt, membership.TokenHash));
        Assert.Equal(FamilyMemberStatuses.Active, h.Store.Members.Single(m => m.UserId == bystander.Id).Status);   // người khác không bị đụng tới
        Assert.Equal(FamilyGroupStatuses.Active, otherGroup.Status);
        var ownGroup = Assert.Single(h.Store.Groups, g => g.OwnerUserId == member.Id);
        Assert.Equal(ownGroup.Id, h.Store.Cycles.Single(c => c.Id == result.CycleId).FamilyGroupId);
        Assert.Contains(h.Notes.Sent, n => n.UserId == otherOwner.Id && n.Type == NotificationTypes.FamilyMemberLeft && n.Body!.Contains("Người mua"));
        Assert.Contains(h.Notes.Sent, n => n.UserId == member.Id && n.Type == NotificationTypes.FamilyMemberLeft && n.Body!.Contains("Chủ nhóm cũ"));
        Assert.Contains(h.Notes.Sent, n => n.UserId == member.Id && n.Type == NotificationTypes.BillingPaymentPaid);
    }

    [Fact]
    public async Task C15_AMemberOfAFamilyThatAlreadyLapsed_HasNothingToLeave_NoLeftNotifications()
    {
        var h = new OrderHarness();
        var otherOwner = h.AddUser(UserRoles.Parent, 45);
        var member = h.AddUser(UserRoles.Parent, 40);
        h.Store.AddFamily(otherOwner.Id, startsAt: Now.AddDays(-5));
        var order = await h.CreateOrder(member, "family-1m");   // lúc đặt đơn chưa là thành viên
        h.Store.Members.Add(new FamilyMember
        {
            FamilyGroupId = h.Store.Groups.Single().Id, UserId = member.Id, InvitedEmail = member.Email, SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Active,
        });
        var membership = h.Store.Members.Single();

        // Thanh toán tới sau khi kỳ của nhóm kia đã hết (job chưa kịp chạy): nhóm kết thúc ngay khi đọc làn, không còn gì để rời
        var result = await h.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, Now.AddDays(30));

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal(FamilyMemberEndReasons.FamilyLapsed, membership.EndedReason);   // đã kết thúc vì hết hạn, không phải vì mua gói riêng
        Assert.DoesNotContain(h.Notes.Sent, n => n.Type == NotificationTypes.FamilyMemberLeft);
    }

    [Fact]
    public async Task C15_APersonalPurchaseNeverTouchesAFamilyMembership()
    {
        var (h, buyer, order) = await Ordered();
        var owner = h.AddUser(UserRoles.Parent, 45);
        h.Store.AddFamily(owner.Id, member: buyer.Id);

        await Pay(h, order);

        Assert.Equal(FamilyMemberStatuses.Active, h.Store.Members.Single().Status);
        Assert.DoesNotContain(h.Notes.Sent, n => n.Type == NotificationTypes.FamilyMemberLeft);
        Assert.Single(h.Store.Groups);
    }

    // ---- người mua không còn / lỗi sau commit

    [Fact]
    public async Task BuyerPurged_NoOwnerToGiveThePlanTo_MismatchForAdmin_NoCrash()
    {
        var (h, _, order) = await Ordered();
        h.Order(order.OrderCode).UserId = null;   // tài khoản đã bị xóa hẳn: FK đặt về null

        var result = await Pay(h, order, payload: "{\"p\":1}");

        Assert.Equal(PaymentApplyStatus.Mismatch, result.Status);
        var stored = h.Order(order.OrderCode);
        Assert.Equal(OrderStatuses.Mismatch, stored.Status);
        Assert.Contains("đã bị xóa", stored.AdminNote);
        Assert.Equal("{\"p\":1}", stored.PayloadJson);
        Assert.Empty(h.Store.Cycles);
        Assert.Empty(h.Notes.Sent);
    }

    [Fact]
    public async Task BuyerSoftDeletedBetweenOrderAndPayment_P19_StillPaidAndActivated()
    {
        var (h, buyer, order) = await Ordered();
        buyer.Status = UserStatuses.Deleted;
        buyer.DeletedAt = Now;

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal(buyer.Id, Assert.Single(h.Store.Cycles).UserId);
    }

    [Fact]
    public async Task NotificationOrEmailFailure_AfterCommit_DoesNotFailThePayment()
    {
        var (h, _, order) = await Ordered();
        h.Notes.Throw = true;

        var result = await Pay(h, order);

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);
        Assert.Single(h.Store.Cycles);
        Assert.Single(h.Email.SentEmails);   // việc sau commit độc lập với nhau: biên nhận vẫn gửi
    }

    [Fact]
    public async Task ApplyPayment_ReadsTheOrderFresh_AfterTheLock()
    {
        var (h, _, order) = await Ordered();

        await Pay(h, order);

        // Trước khi khóa luôn xóa theo dõi (người gọi có thể đã nạp đơn hoặc kỳ cũ); khóa nằm trong transaction
        Assert.Equal(1, h.Store.TrackingCleared);
        Assert.Equal([order.OrderCode], h.Store.LockedCodes);
    }
}
