using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Admin xử lý đơn hàng (UC-132..134): A6 đến A10, kích hoạt khi làn đầy, hợp đồng JSON với frontend.
public class AdminOrderServiceTests
{
    private static readonly DateTime Now = BillingStore.Now;
    private static readonly DateTime ActAt = Now.AddMinutes(5);

    private static async Task<BillingRuleException> Fails(Task task) => await Assert.ThrowsAsync<BillingRuleException>(() => task);

    /// <summary>Đơn đã lệch tiền: người mua chuyển 1 000 đ thay vì 79 000 đ.</summary>
    private static async Task<(JobHarness J, User Buyer, User Admin, OrderDto Order)> Mismatched(string plan = "personal-1m")
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, plan);
        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, 1_000, "{\"amount\":1000}", Now.AddMinutes(1));
        j.Notes.Sent.Clear();
        return (j, buyer, admin, order);
    }

    // ---- A6: lọc và tìm

    [Fact]
    public async Task A6_ListFiltersByLowercaseStatus_AndSearchesByCodeOrBuyerEmail_NewestFirst()
    {
        var j = new JobHarness();
        var (alice, bob) = (j.AddUser(email: "alice@e2e.test", name: "Alice"), j.AddUser(email: "Bob.Smith@e2e.test", name: "Bob"));
        var first = await j.H.CreateOrder(alice, "personal-1m", Now);
        var second = await j.H.CreateOrder(bob, "family-1m", Now.AddMinutes(1));
        await j.H.Orders.ApplyPaymentAsync(second.OrderCode, 2_000, null, Now.AddMinutes(2));   // lệch tiền

        var all = await j.Admin.ListAsync(null, null, 1, 20);
        var pending = await j.Admin.ListAsync("pending", null, 1, 20);
        var mismatch = await j.Admin.ListAsync("MISMATCH", null, 1, 20);
        var byCode = await j.Admin.ListAsync(null, first.OrderCode.ToString(), 1, 20);
        var byEmail = await j.Admin.ListAsync(null, "bob.SMITH", 1, 20);
        var none = await j.Admin.ListAsync(null, "nobody", 1, 20);
        var unknownStatus = await j.Admin.ListAsync("refunded", null, 1, 20);

        Assert.Equal([second.OrderCode, first.OrderCode], all.Items.Select(i => i.OrderCode));
        Assert.Equal(first.OrderCode, Assert.Single(pending.Items).OrderCode);
        Assert.Equal(second.OrderCode, Assert.Single(mismatch.Items).OrderCode);
        Assert.Equal(first.OrderCode, Assert.Single(byCode.Items).OrderCode);
        var hit = Assert.Single(byEmail.Items);
        Assert.Equal((second.OrderCode, "Bob", "Bob.Smith@e2e.test"), (hit.OrderCode, hit.BuyerName, hit.BuyerEmail));
        Assert.Empty(none.Items);
        Assert.Empty(unknownStatus.Items);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task A6_NumericSearch_IsAnExactOrderCode_NotAPartialMatch()
    {
        var (j, _, _, order) = await Mismatched();

        Assert.Empty((await j.Admin.ListAsync(null, (order.OrderCode / 10).ToString(), 1, 20)).Items);
        Assert.Single((await j.Admin.ListAsync(null, order.OrderCode.ToString(), 1, 20)).Items);
    }

    [Fact]
    public async Task A6_Paging_ClampsPageAndPageSize_AndCountsPages()
    {
        var j = new JobHarness();
        for (var i = 0; i < 3; i++)
            await j.H.CreateOrder(j.AddUser(), "personal-1m", Now.AddSeconds(i));

        var page2 = await j.Admin.ListAsync(null, null, 2, 2);
        var clampedLow = await j.Admin.ListAsync(null, null, 0, 0);
        var clampedHigh = await j.Admin.ListAsync(null, null, 1, 500);

        Assert.Equal((2, 2, 3, 2, 1), (page2.Page, page2.PageSize, page2.TotalCount, page2.TotalPages, page2.Items.Count));
        Assert.Equal((1, 1, 3, 3), (clampedLow.Page, clampedLow.PageSize, clampedLow.TotalCount, clampedLow.TotalPages));
        Assert.Equal((50, 3), (clampedHigh.PageSize, clampedHigh.Items.Count));
    }

    [Fact]
    public async Task A6_PurgedBuyer_HasNullNameAndEmail_AndHandledByIsTheAdminsFullName()
    {
        var (j, buyer, admin, order) = await Mismatched();
        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "Đã đối chiếu", ActAt);
        j.H.Order(order.OrderCode).UserId = null;   // tài khoản người mua đã bị xóa hẳn: giao dịch giữ lại ẩn danh

        var item = Assert.Single((await j.Admin.ListAsync(null, null, 1, 20)).Items);
        var detail = await j.Admin.GetAsync(order.OrderCode);

        Assert.Equal((null, null, admin.FullName), (item.BuyerName, item.BuyerEmail, item.HandledBy));
        Assert.Equal((null, null, "personal-1m"), (detail.BuyerName, detail.BuyerEmail, detail.PlanCode));
        Assert.NotEqual(buyer.Email, detail.BuyerEmail);
    }

    [Fact]
    public async Task Detail_ReturnsThePayloadJson_AndUnknownCodeIs404()
    {
        var (j, _, _, order) = await Mismatched();

        var detail = await j.Admin.GetAsync(order.OrderCode);
        var missing = await Fails(j.Admin.GetAsync(1));

        Assert.Equal("{\"amount\":1000}", detail.PayloadJson);
        Assert.Equal(("order_not_found", 404), (missing.Code, missing.StatusCode));
    }

    // ---- A7: kích hoạt có ghi chú, hoặc từ chối

    [Fact]
    public async Task A7_ActivateMismatch_WithNote_PaysOnce_CreatesTheCycle_NotifiesTheBuyer_AndAudits()
    {
        var (j, buyer, admin, order) = await Mismatched();

        var dto = await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "  Đã đối chiếu sao kê  ", ActAt);

        var cycle = Assert.Single(j.Store.Cycles);
        Assert.Equal((OrderStatuses.Paid, "Đã đối chiếu sao kê", admin.FullName), (dto.Status, dto.AdminNote, dto.HandledBy));
        Assert.Equal((buyer.Id, CycleStatuses.Active, (DateTime?)ActAt, 500_000L, 79_000m), (cycle.UserId, cycle.Status, cycle.StartsAt, cycle.TokenQuota, cycle.PriceVnd));   // số tiền và hạn mức theo đơn
        Assert.Equal(ActAt, dto.PaidAt);
        Assert.Equal(new OrderCycleDto(ActAt, ActAt.AddDays(30), false), dto.Cycle);
        Assert.Equal(admin.Id, j.H.Order(order.OrderCode).HandledByUserId);
        Assert.Equal(buyer.Id, Assert.Single(j.Sent(NotificationTypes.BillingPaymentPaid)).UserId);
        var audit = Assert.Single(j.Audit.Entries);
        Assert.Equal(("order.activate", "payment_transaction", admin.Id, j.H.Order(order.OrderCode).Id.ToString()), (audit.Action, audit.EntityType, audit.ActorUserId, audit.EntityId));
        Assert.Contains("Đã đối chiếu sao kê", audit.DetailJson);
        Assert.Equal(AccountTiers.Premium, (await j.H.Entitlements.GetSummaryAsync(buyer.Id, ActAt)).Tier);
    }

    [Fact]
    public async Task F6_ActivateCommitted_ThenTheRequestIsCancelled_TheAuditEntryIsStillWritten()
    {
        var (j, _, admin, order) = await Mismatched();
        using var cts = new CancellationTokenSource();
        j.Notes.OnSent = cts.Cancel;   // admin đóng trang ngay sau khi gói đã kích hoạt (thông báo cho người mua)

        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "Đã đối chiếu sao kê", ActAt, cts.Token);

        Assert.Equal("order.activate", Assert.Single(j.Audit.Entries).Action);
        Assert.Single(j.Store.Cycles);
    }

    [Fact]
    public async Task F6_RecheckAppliedAPayment_ThenTheRequestIsCancelled_TheAuditEntryIsStillWritten()
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(GatewayStatuses.Paid, 79_000);
        using var cts = new CancellationTokenSource();
        j.Notes.OnSent = cts.Cancel;

        await j.Admin.RecheckAsync(admin.Id, order.OrderCode, ActAt, cts.Token);

        Assert.Equal("order.recheck", Assert.Single(j.Audit.Entries).Action);
        Assert.Equal(OrderStatuses.Paid, j.H.Order(order.OrderCode).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A7_ActivateAndReject_WithoutANote_Are400_AndChangeNothing(string? note)
    {
        var (j, _, admin, order) = await Mismatched();

        var activate = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, note, ActAt));
        var reject = await Fails(j.Admin.RejectAsync(admin.Id, order.OrderCode, note, ActAt));

        Assert.Equal(("note_required", 400), (activate.Code, activate.StatusCode));
        Assert.Equal(("note_required", 400), (reject.Code, reject.StatusCode));
        Assert.Equal(OrderStatuses.Mismatch, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.Store.Cycles);
        Assert.Empty(j.Audit.Entries);
    }

    [Fact]
    public async Task A7_NoteOverTheColumnLimit_Is400()
    {
        var (j, _, admin, order) = await Mismatched();

        var error = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, new string('x', 1001), ActAt));

        Assert.Equal(("note_too_long", 400), (error.Code, error.StatusCode));
    }

    [Fact]
    public async Task A7_RejectMismatch_CancelsIt_StoresTheNote_NotifiesTheBuyer_AndAudits()
    {
        var (j, buyer, admin, order) = await Mismatched();

        var dto = await j.Admin.RejectAsync(admin.Id, order.OrderCode, "Chuyển sai số tiền", ActAt);

        Assert.Equal((OrderStatuses.Cancelled, "Chuyển sai số tiền", admin.FullName), (dto.Status, dto.AdminNote, dto.HandledBy));
        Assert.Empty(j.Store.Cycles);
        var note = Assert.Single(j.Notes.Sent);
        Assert.Equal((buyer.Id, $"/orders/{order.OrderCode}"), (note.UserId, note.Link));
        Assert.DoesNotContain("Chuyển sai số tiền", note.Body);   // ghi chú nội bộ của admin không gửi cho người mua
        var audit = Assert.Single(j.Audit.Entries);
        Assert.Equal(("order.reject", admin.Id), (audit.Action, audit.ActorUserId));
        Assert.Contains(order.OrderCode, j.H.Store.LockedCodes);   // khóa dòng đơn khi đổi trạng thái
    }

    [Theory]
    [InlineData(OrderStatuses.Pending)]
    [InlineData(OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Expired)]
    [InlineData(OrderStatuses.Cancelled)]
    public async Task A7_RejectOnlyAppliesToMismatch(string status)
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Order(order.OrderCode).Status = status;

        var error = await Fails(j.Admin.RejectAsync(admin.Id, order.OrderCode, "x", ActAt));

        Assert.Equal(("not_mismatch", 409), (error.Code, error.StatusCode));
        Assert.Equal(status, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.Audit.Entries);
    }

    [Fact]
    public async Task A7_RejectUnknownOrder_Is404()
    {
        var j = new JobHarness();

        var error = await Fails(j.Admin.RejectAsync(j.AddAdmin().Id, 42, "x", ActAt));

        Assert.Equal(("order_not_found", 404), (error.Code, error.StatusCode));
    }

    // ---- A8: kiểm lại với payOS

    [Theory]
    [InlineData(OrderStatuses.Pending, "PAID", null, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Pending, "UNDERPAID", 5_000L, OrderStatuses.Mismatch)]
    [InlineData(OrderStatuses.Pending, "CANCELLED", 0L, OrderStatuses.Cancelled)]
    [InlineData(OrderStatuses.Pending, "EXPIRED", 0L, OrderStatuses.Expired)]
    [InlineData(OrderStatuses.Pending, "PENDING", 0L, OrderStatuses.Pending)]
    [InlineData(OrderStatuses.Mismatch, "PAID", null, OrderStatuses.Paid)]          // đã bù đủ tiền
    [InlineData(OrderStatuses.Mismatch, "PAID", 1_000L, OrderStatuses.Mismatch)]    // vẫn thiếu
    [InlineData(OrderStatuses.Mismatch, "CANCELLED", 0L, OrderStatuses.Mismatch)]   // có tiền về rồi: không tự hủy
    [InlineData(OrderStatuses.Mismatch, "EXPIRED", 0L, OrderStatuses.Mismatch)]
    [InlineData(OrderStatuses.Expired, "PAID", null, OrderStatuses.Paid)]            // trả muộn: vẫn kích hoạt như webhook (P16)
    [InlineData(OrderStatuses.Cancelled, "EXPIRED", 0L, OrderStatuses.Cancelled)]
    public async Task A8_Recheck_AppliesThePayOsState_ToAnyUnpaidOrder(string from, string gatewayStatus, long? amountPaid, string expected)
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Order(order.OrderCode).Status = from;
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(gatewayStatus, amountPaid ?? order.AmountVnd);

        var dto = await j.Admin.RecheckAsync(admin.Id, order.OrderCode, ActAt);

        Assert.Equal(expected, dto.Status);
        Assert.Equal(expected == OrderStatuses.Paid ? 1 : 0, j.Store.Cycles.Count);
        var audit = Assert.Single(j.Audit.Entries);
        Assert.Equal("order.recheck", audit.Action);
        Assert.Contains(gatewayStatus, audit.DetailJson);
    }

    [Fact]
    public async Task A8_Recheck_ThatActivates_RecordsTheAdminAsHandler()
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus("PAID", order.AmountVnd);

        var dto = await j.Admin.RecheckAsync(admin.Id, order.OrderCode, ActAt);

        Assert.Equal((OrderStatuses.Paid, admin.FullName), (dto.Status, dto.HandledBy));
    }

    [Fact]
    public async Task A8_Recheck_PendingPastExpiryAndGrace_ExpiresIt()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");

        var dto = await j.Admin.RecheckAsync(admin.Id, order.OrderCode, order.ExpiresAt.AddMinutes(3));

        Assert.Equal(OrderStatuses.Expired, dto.Status);
        Assert.Single(j.H.Gateway.Cancelled);
    }

    [Fact]
    public async Task A8_Recheck_WhenPayOsIsDown_Is502_AndChangesNothing()
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Gateway.FailStatusFor.Add(order.OrderCode);

        var error = await Fails(j.Admin.RecheckAsync(admin.Id, order.OrderCode, ActAt));

        Assert.Equal(("gateway_unavailable", 502), (error.Code, error.StatusCode));
        Assert.Equal(OrderStatuses.Mismatch, j.H.Order(order.OrderCode).Status);
        Assert.Empty(j.Audit.Entries);
    }

    [Fact]
    public async Task A8_Recheck_OfAPaidOrderIs409_AndUnknownIs404()
    {
        var (j, _, admin, order) = await Mismatched();
        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "ok", ActAt);

        var paid = await Fails(j.Admin.RecheckAsync(admin.Id, order.OrderCode, ActAt));
        var unknown = await Fails(j.Admin.RecheckAsync(admin.Id, 7, ActAt));

        Assert.Equal(("already_paid", 409), (paid.Code, paid.StatusCode));
        Assert.Equal(("order_not_found", 404), (unknown.Code, unknown.StatusCode));
    }

    // ---- A9, A10: đơn đã trả, hai admin cùng lúc

    [Fact]
    public async Task A9_ActivateAPaidOrder_Is409AlreadyPaid_NoSecondCycle_NoAudit()
    {
        var (j, _, admin, order) = await Mismatched();
        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "lần 1", ActAt);

        var error = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, "lần 2", ActAt.AddMinutes(1)));

        Assert.Equal(("already_paid", 409), (error.Code, error.StatusCode));
        Assert.Single(j.Store.Cycles);
        Assert.Single(j.Audit.Entries);
        Assert.Equal("lần 1", j.H.Order(order.OrderCode).AdminNote);
    }

    [Fact]
    public async Task A10_TwoAdminsActivate_OneWins_TheOtherGetsAlreadyPaid()
    {
        var (j, _, first, order) = await Mismatched();
        var second = j.AddAdmin("Admin Hai");

        var results = await Task.WhenAll(
            Attempt(j.Admin.ActivateAsync(first.Id, order.OrderCode, "một", ActAt)),
            Attempt(j.Admin.ActivateAsync(second.Id, order.OrderCode, "hai", ActAt)));

        Assert.Equal(1, results.Count(r => r is null));
        Assert.Equal("already_paid", Assert.Single(results, r => r is not null)!.Code);
        Assert.Single(j.Store.Cycles);
        Assert.Single(j.Audit.Entries);
        Assert.Single(j.Sent(NotificationTypes.BillingPaymentPaid));

        static async Task<BillingRuleException?> Attempt(Task<AdminOrderDto> task)
        {
            try
            {
                await task;
                return null;
            }
            catch (BillingRuleException ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public async Task A10_ActivateThenReject_TheLoserGetsNotMismatch()
    {
        var (j, _, first, order) = await Mismatched();
        var second = j.AddAdmin("Admin Hai");
        await j.Admin.ActivateAsync(first.Id, order.OrderCode, "một", ActAt);

        var error = await Fails(j.Admin.RejectAsync(second.Id, order.OrderCode, "hai", ActAt));

        Assert.Equal("not_mismatch", error.Code);
        Assert.Equal(OrderStatuses.Paid, j.H.Order(order.OrderCode).Status);
        Assert.Equal(first.Id, j.H.Order(order.OrderCode).HandledByUserId);
    }

    [Fact]
    public async Task A10_RejectThenReject_TheSecondGetsNotMismatch()
    {
        var (j, _, first, order) = await Mismatched();
        var second = j.AddAdmin("Admin Hai");
        await j.Admin.RejectAsync(first.Id, order.OrderCode, "một", ActAt);

        var error = await Fails(j.Admin.RejectAsync(second.Id, order.OrderCode, "hai", ActAt));

        Assert.Equal("not_mismatch", error.Code);
        Assert.Single(j.Audit.Entries);
        Assert.Single(j.Notes.Sent);
    }

    // ---- Kích hoạt khi làn đầy (P22) và các trường hợp biên

    [Fact]
    public async Task P22_ActivateWhenTheLaneIsFull_Is409LaneFull_AndNothingIsGranted()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        j.Store.AddActive(buyer.Id, "personal-1m", Now.AddDays(-1));
        j.Store.AddQueued(buyer.Id, "personal-3m");
        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, Now.AddMinutes(1));   // tiền về nhưng làn đầy: mismatch
        Assert.Contains("lane_full", j.H.Order(order.OrderCode).AdminNote);

        var error = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, "thử", ActAt));

        Assert.Equal(("lane_full", 409), (error.Code, error.StatusCode));
        Assert.Equal(OrderStatuses.Mismatch, j.H.Order(order.OrderCode).Status);
        Assert.Equal(2, j.Store.Cycles.Count);
        var audit = Assert.Single(j.Audit.Entries);   // L6: admin đã xác nhận tiền nhưng không kích hoạt được vẫn để lại dấu vết
        Assert.Equal(("order.activate", admin.Id), (audit.Action, audit.ActorUserId));
        Assert.Contains("\"outcome\":\"lane_full\"", audit.DetailJson);
        Assert.Contains("\"result\":\"LaneFull\"", audit.DetailJson);
    }

    [Fact]
    public async Task P22_AfterTheLaneFreesUp_TheSameOrderCanBeActivated()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        var active = j.Store.AddActive(buyer.Id, "personal-1m", Now.AddDays(-1));
        var queued = j.Store.AddQueued(buyer.Id, "personal-3m");
        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, order.AmountVnd, null, Now.AddMinutes(1));
        queued.Status = CycleStatuses.Ended;   // kỳ chờ đã qua: làn có chỗ cho một kỳ chờ mới

        var dto = await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "đã có chỗ", ActAt);

        Assert.Equal((OrderStatuses.Paid, true), (dto.Status, dto.Cycle!.Queued));
        Assert.Equal(CycleStatuses.Active, active.Status);
    }

    [Fact]
    public async Task Activate_ExpiredOrderWhoseMoneyArrivedAnyway_IsGrantedWithoutAnAmountCheck()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        j.H.Order(order.OrderCode).Status = OrderStatuses.Expired;

        var dto = await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "khách đưa biên lai", ActAt);

        Assert.Equal(OrderStatuses.Paid, dto.Status);
        Assert.Equal(buyer.Id, Assert.Single(j.Store.Cycles).UserId);
    }

    [Fact]
    public async Task Activate_WhenTheBuyerAccountIsGone_Is409BuyerDeleted()
    {
        var (j, _, admin, order) = await Mismatched();
        j.H.Order(order.OrderCode).UserId = null;

        var error = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, "x", ActAt));

        Assert.Equal(("buyer_deleted", 409), (error.Code, error.StatusCode));
        Assert.Empty(j.Store.Cycles);
        var audit = Assert.Single(j.Audit.Entries);   // L6
        Assert.Equal("order.activate", audit.Action);
        Assert.Contains("\"outcome\":\"buyer_deleted\"", audit.DetailJson);
    }

    // ---- L6: kích hoạt đơn còn pending thì đóng link payOS

    [Fact]
    public async Task L6_ActivateAPendingOrder_ClosesItsPayOsLink()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");

        var dto = await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "khách chuyển khoản tay", ActAt);

        Assert.Equal(OrderStatuses.Paid, dto.Status);
        Assert.Equal(order.OrderCode, Assert.Single(j.H.Gateway.Cancelled).Code);
    }

    [Fact]
    public async Task L6_ActivateAPendingOrder_ALinkCancelFailureDoesNotBreakTheActivation()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        j.H.Gateway.FailCancel = true;

        var dto = await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "khách chuyển khoản tay", ActAt);

        Assert.Equal(OrderStatuses.Paid, dto.Status);
        Assert.Single(j.Store.Cycles);
        Assert.Equal("order.activate", Assert.Single(j.Audit.Entries).Action);
    }

    [Fact]
    public async Task L6_ActivateAMismatchOrder_DoesNotTouchThePayOsLink()
    {
        var (j, _, admin, order) = await Mismatched();

        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "đã đối chiếu", ActAt);

        Assert.Empty(j.H.Gateway.Cancelled);
    }

    [Fact]
    public async Task L6_ActivateAPendingOrderWhoseLaneIsFull_StillClosesTheLink_AndAudits_Then409()
    {
        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        j.Store.AddActive(buyer.Id, "personal-1m", Now.AddDays(-1));
        j.Store.AddQueued(buyer.Id, "personal-3m");

        var error = await Fails(j.Admin.ActivateAsync(admin.Id, order.OrderCode, "chuyển tay", ActAt));

        Assert.Equal("lane_full", error.Code);
        Assert.Equal(order.OrderCode, Assert.Single(j.H.Gateway.Cancelled).Code);
        Assert.Contains("lane_full", Assert.Single(j.Audit.Entries).DetailJson);
    }

    [Fact]
    public async Task Activate_UnknownOrder_Is404()
    {
        var j = new JobHarness();

        var error = await Fails(j.Admin.ActivateAsync(j.AddAdmin().Id, 99, "x", ActAt));

        Assert.Equal(("order_not_found", 404), (error.Code, error.StatusCode));
    }

    // ---- Hợp đồng với frontend (frontend/src/types/admin-billing.ts, api.ts)

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string[] Keys(object value) => JsonDocument.Parse(JsonSerializer.Serialize(value, Web)).RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();

    [Fact]
    public async Task Contract_AdminOrderDtoAndDetailAndPagedResult_HaveExactlyTheFrontendKeys()
    {
        var (j, _, admin, order) = await Mismatched();
        await j.Admin.ActivateAsync(admin.Id, order.OrderCode, "ok", ActAt);
        var list = await j.Admin.ListAsync(null, null, 1, 20);
        var detail = await j.Admin.GetAsync(order.OrderCode);

        string[] orderDto = ["amountVnd", "checkoutUrl", "createdAt", "cycle", "description", "expiresAt", "orderCode", "paidAt", "planCode", "planKind", "planName", "qrCode", "status"];
        string[] admin4 = ["adminNote", "buyerEmail", "buyerName", "handledBy"];

        Assert.Equal(orderDto.Concat(admin4).Order(), Keys(list.Items[0]));
        Assert.Equal(orderDto.Concat(admin4).Append("payloadJson").Order(), Keys(detail));
        Assert.Equal(["items", "page", "pageSize", "totalCount", "totalPages"], Keys(list));
        Assert.Equal(["endsAt", "queued", "startsAt"], Keys(detail.Cycle!));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(detail, Web));
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("amountVnd").ValueKind);
        Assert.Equal("paid", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void Contract_AdminOrderDto_IsAnOrderDtoSuperset()
    {
        var orderProps = typeof(OrderDto).GetProperties().Select(p => p.Name).Where(n => n != "EqualityContract").Order();
        var adminProps = typeof(AdminOrderDto).GetProperties().Select(p => p.Name).Where(n => n != "EqualityContract").ToHashSet();

        Assert.All(orderProps, n => Assert.Contains(n, adminProps));
    }

    // ---- Quyền

    [Fact]
    public void Controller_IsAdminOnly_AndExposesTheFiveDocumentedRoutes()
    {
        var type = typeof(AdminOrdersController);

        Assert.Equal(UserRoles.Admin, type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/v1/admin/orders", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (Verb: m.GetCustomAttributes<HttpMethodAttribute>().Single(), Anon: m.GetCustomAttribute<AllowAnonymousAttribute>()))
            .ToList();
        Assert.All(routes, r => Assert.Null(r.Anon));
        Assert.Equal(
            ["GET:", "GET:{orderCode:long}", "POST:{orderCode:long}/activate", "POST:{orderCode:long}/recheck", "POST:{orderCode:long}/reject"],
            routes.Select(r => $"{r.Verb.HttpMethods.Single()}:{r.Verb.Template}").Order());
    }
}
