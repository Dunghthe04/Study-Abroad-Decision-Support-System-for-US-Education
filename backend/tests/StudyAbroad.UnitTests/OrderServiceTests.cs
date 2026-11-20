using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Tạo, xem, liệt kê, hủy đơn gói (kế hoạch billing v2: P2–P10, P20, R6, F29). Áp dụng thanh toán ở OrderPaymentTests.
public class OrderServiceTests
{
    private static readonly DateTime Now = OrderHarness.Now;

    private static async Task<BillingRuleException> Fails(Task task) => await Assert.ThrowsAsync<BillingRuleException>(() => task);

    // ---- tạo đơn

    [Fact]
    public async Task P1_Create_PendingOrderWithPayOsLinkAndSnapshots()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(age: 17);

        var dto = await h.CreateOrder(buyer, "personal-1m");

        var order = h.Order(dto.OrderCode);
        Assert.Equal(new DateTimeOffset(Now).ToUnixTimeMilliseconds(), dto.OrderCode);
        Assert.Equal(("pending", "personal-1m", "Cá nhân 1 tháng", "personal", 79_000L), (dto.Status, dto.PlanCode, dto.PlanName, dto.PlanKind, dto.AmountVnd));
        Assert.Equal("USAS" + (dto.OrderCode % 100_000).ToString("D5"), dto.Description);
        Assert.Equal(9, dto.Description.Length);
        Assert.Equal(Now.AddMinutes(30), dto.ExpiresAt);
        Assert.Equal(("https://pay.test/" + dto.OrderCode, "QR-" + dto.OrderCode), (dto.CheckoutUrl, dto.QrCode));
        Assert.Null(dto.Cycle);
        Assert.Null(dto.PaidAt);
        // Bản chụp gói nằm trong đơn
        Assert.Equal((buyer.Id, h.Store.Plan("personal-1m").Id, "personal", "Cá nhân 1 tháng", 30, 500_000L, "payos"),
            (order.UserId, order.SubscriptionPlanId, order.PlanKind, order.PlanName, order.PlanDurationDays, order.PlanTokenQuota, order.Gateway));
        Assert.Equal(79_000m, order.AmountVnd);
        Assert.Equal($"link-{dto.OrderCode}", order.GatewayTransactionId);
        // payOS nhận đúng mã, số tiền, nội dung, hạn và địa chỉ quay về
        var created = Assert.Single(h.Gateway.Created);
        Assert.Equal((dto.OrderCode, 79_000L, dto.Description, Now.AddMinutes(30)), (created.Code, created.Amount, created.Description, created.ExpiresAt));
        Assert.Equal($"https://web.test/orders/{dto.OrderCode}", created.ReturnUrl);
        Assert.Equal(created.ReturnUrl, created.CancelUrl);
    }

    [Fact]
    public async Task Create_UsesTheConfiguredExpiryMinutes()
    {
        var h = new OrderHarness();
        h.Billing.OrderExpiryMinutes = 1;

        var dto = await h.CreateOrder(h.AddUser(), "personal-1m");

        Assert.Equal(Now.AddMinutes(1), dto.ExpiresAt);
    }

    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(17, true)]
    [InlineData(40, true)]
    public async Task P2_P3_Age16OrOlderCanBuy_Under16Cannot(int age, bool allowed)
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(age: age);

        if (allowed)
        {
            Assert.Equal("pending", (await h.CreateOrder(buyer, "personal-1m")).Status);
            return;
        }
        var ex = await Fails(h.CreateOrder(buyer, "personal-1m"));
        Assert.Equal(("age_restricted", 403, "Tài khoản dưới 16 tuổi chưa thể mua gói."), (ex.Code, ex.StatusCode, ex.Message));
        Assert.Empty(h.Store.Orders);
        Assert.Empty(h.Gateway.Created);
    }

    [Fact]
    public async Task P3_TurnsSixteenTodayByVietnamDate_CanBuy_ButNotTheDayBefore()
    {
        var h = new OrderHarness();
        // Now = 03:00 UTC ngày 20/11 = 10:00 ngày 20/11 giờ Việt Nam
        var today = DateOnly.FromDateTime(Now.AddHours(7));
        var birthday = h.AddUser();
        birthday.DateOfBirth = today.AddYears(-16);
        var tomorrow = h.AddUser();
        tomorrow.DateOfBirth = today.AddYears(-16).AddDays(1);

        Assert.Equal("pending", (await h.CreateOrder(birthday, "personal-1m")).Status);
        Assert.Equal("age_restricted", (await Fails(h.CreateOrder(tomorrow, "personal-1m"))).Code);
    }

    [Fact]
    public async Task VietnamDateDecidesAge_NotTheUtcDate()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        buyer.DateOfBirth = new DateOnly(2010, 11, 21);   // tròn 16 vào 21/11/2026 giờ Việt Nam
        var lateUtc = new DateTime(2026, 11, 20, 17, 30, 0, DateTimeKind.Utc);   // đã là 00:30 ngày 21/11 ở Việt Nam

        Assert.Equal("age_restricted", (await Fails(h.CreateOrder(buyer, "personal-1m", Now))).Code);
        Assert.Equal("pending", (await h.CreateOrder(buyer, "personal-1m", lateUtc)).Status);
    }

    [Fact]
    public async Task UnknownDateOfBirth_IsAllowed()
    {
        var h = new OrderHarness();

        Assert.Equal("pending", (await h.CreateOrder(h.AddUser(age: null), "personal-1m")).Status);
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Center)]
    [InlineData("")]
    public async Task P4_StaffAndUnknownRoles_Get403_BeforeAnyOtherCheck(string role)
    {
        var h = new OrderHarness();
        var user = h.AddUser(role, age: 10);   // dưới 16 mà vẫn là role_not_allowed trước age_restricted

        var ex = await Fails(h.Orders.CreateAsync(user.Id, role, "personal-1m", Now));

        Assert.Equal(("role_not_allowed", 403), (ex.Code, ex.StatusCode));
        Assert.Empty(h.Store.Orders);
    }

    [Fact]
    public async Task ParentsCanBuy()
    {
        var h = new OrderHarness();

        Assert.Equal("pending", (await h.CreateOrder(h.AddUser(UserRoles.Parent, age: 45), "family-1m")).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("free")]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PERSONAL-1M")]
    public async Task UnknownOrFreePlan_IsNotForSale(string? code)
    {
        var h = new OrderHarness();

        var ex = await Fails(h.CreateOrder(h.AddUser(), code));

        Assert.Equal(("plan_not_for_sale", 400), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task PlanTakenOffSale_IsNotForSale()
    {
        var h = new OrderHarness();
        h.Store.Plan("personal-3m").IsActive = false;

        Assert.Equal("plan_not_for_sale", (await Fails(h.CreateOrder(h.AddUser(), "personal-3m"))).Code);
    }

    [Fact]
    public async Task P5_PendingOrderExists_ReturnsConflictWithOrderCode()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var first = await h.CreateOrder(buyer, "personal-1m");

        var ex = await Fails(h.CreateOrder(buyer, "family-1m", Now.AddSeconds(5)));

        Assert.Equal(("pending_order_exists", 409), (ex.Code, ex.StatusCode));
        Assert.Equal(first.OrderCode, ex.Extra["orderCode"]);
        Assert.Single(h.Store.Orders);
    }

    [Fact]
    public async Task P5_PendingOrderBlocksOnlyThatUser()
    {
        var h = new OrderHarness();
        await h.CreateOrder(h.AddUser(), "personal-1m");

        Assert.Equal("pending", (await h.CreateOrder(h.AddUser(), "personal-1m", Now.AddSeconds(1))).Status);
    }

    [Fact]
    public async Task P6_DoubleClickRace_OnlyOnePendingOrder_TheLoserGetsTheWinnersCode()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        // Request kia chen đơn chờ vào giữa lúc request này kiểm tra và lúc ghi: index một-đơn-chờ-mỗi-người chặn
        h.Store.RaceBeforeOrderInsert = s => s.Orders.Add(new PaymentTransaction
        {
            UserId = buyer.Id, OrderCode = 42, Status = OrderStatuses.Pending, Gateway = "payos", PlanKind = PlanKinds.Personal,
        });

        var ex = await Fails(h.CreateOrder(buyer, "personal-1m"));

        Assert.Equal(("pending_order_exists", 409), (ex.Code, ex.StatusCode));
        Assert.Equal(42L, ex.Extra["orderCode"]);
        Assert.Single(h.Store.Orders.Where(o => o.Status == OrderStatuses.Pending));
        Assert.Empty(h.Gateway.Created);
    }

    [Fact]
    public async Task OrderCodeCollision_RetriesWithTheNextCode()
    {
        var h = new OrderHarness();
        var first = await h.CreateOrder(h.AddUser(), "personal-1m");

        var second = await h.CreateOrder(h.AddUser(), "personal-1m");   // cùng một mili giây

        Assert.Equal(first.OrderCode + 1, second.OrderCode);
        Assert.Equal(2, h.Store.Orders.Select(o => o.OrderCode).Distinct().Count());
    }

    [Fact]
    public async Task OrderCodeCollision_GivesUpAfterThreeTries()
    {
        var h = new OrderHarness();
        var baseCode = new DateTimeOffset(Now).ToUnixTimeMilliseconds();
        foreach (var i in new[] { 0, 1, 2 })
            h.Store.Orders.Add(new PaymentTransaction { UserId = Guid.NewGuid(), OrderCode = baseCode + i, Status = OrderStatuses.Paid, Gateway = "payos", PlanKind = "personal" });

        var ex = await Fails(h.CreateOrder(h.AddUser(), "personal-1m"));

        Assert.Equal(("order_busy", 503), (ex.Code, ex.StatusCode));
        Assert.Equal(3, h.Store.Orders.Count);
        Assert.Empty(h.Gateway.Created);
    }

    [Fact]
    public async Task P9_GatewayFails_OrderCancelled_502_UserCanRetryAtOnce()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        h.Gateway.FailCreate = true;

        var ex = await Fails(h.CreateOrder(buyer, "personal-1m"));

        Assert.Equal(("gateway_unavailable", 502), (ex.Code, ex.StatusCode));
        Assert.Equal("cancelled", Assert.Single(h.Store.Orders).Status);

        h.Gateway.FailCreate = false;
        var retry = await h.CreateOrder(buyer, "personal-1m", Now.AddSeconds(3));
        Assert.Equal("pending", retry.Status);
    }

    [Fact]
    public async Task P8_PriceChangeAfterOrdering_PendingKeepsTheOldAmount_NewOrdersUseTheNewPrice()
    {
        var h = new OrderHarness();
        var first = h.AddUser();
        var second = h.AddUser();
        var order = await h.CreateOrder(first, "personal-1m");

        h.Store.Plan("personal-1m").PriceVnd = 99_000;
        h.Store.Plan("personal-1m").TokenQuota = 1;
        var newer = await h.CreateOrder(second, "personal-1m", Now.AddSeconds(1));

        Assert.Equal(79_000m, h.Order(order.OrderCode).AmountVnd);
        Assert.Equal(99_000L, newer.AmountVnd);
        Assert.Equal(500_000L, h.Order(order.OrderCode).PlanTokenQuota);
    }

    // ---- R6, F29: luật làn

    [Fact]
    public async Task R1_ActiveWithTokensLeft_CanBuyTheRenewal()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        h.Store.AddActive(buyer.Id, "personal-1m");

        Assert.Equal("pending", (await h.CreateOrder(buyer, "personal-1m")).Status);
    }

    [Fact]
    public async Task R6_SecondRenewalWhileOneIsQueued_AlreadyRenewed()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        h.Store.AddActive(buyer.Id, "personal-1m");
        h.Store.AddQueued(buyer.Id, "personal-3m");

        var ex = await Fails(h.CreateOrder(buyer, "personal-1m"));

        Assert.Equal(("already_renewed", 409), (ex.Code, ex.StatusCode));
        Assert.Empty(h.Store.Orders);
    }

    [Fact]
    public async Task R6_TheQueueIsPerLane_PersonalQueuedDoesNotBlockFamily()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(UserRoles.Parent, age: 40);
        h.Store.AddActive(buyer.Id, "personal-1m");
        h.Store.AddQueued(buyer.Id, "personal-3m");

        Assert.Equal("pending", (await h.CreateOrder(buyer, "family-1m")).Status);
    }

    [Fact]
    public async Task R6_TheQueuedCycleStartsFirstWhenTheActiveOneHasExpired_SoRenewingIsAllowedAgain()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        h.Store.AddActive(buyer.Id, "personal-1m", startsAt: Now.AddDays(-31));
        h.Store.AddQueued(buyer.Id, "personal-1m");

        // AdvanceLane thấy kỳ cũ hết hạn: kỳ chờ chạy, làn không còn kỳ chờ nên mua gia hạn được
        Assert.Equal("pending", (await h.CreateOrder(buyer, "personal-3m")).Status);
    }

    [Fact]
    public async Task F29_MemberOfAnotherCurrentFamily_CannotBuyFamily_ButCanBuyPersonal()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, age: 40);
        var member = h.AddUser();
        h.Store.AddFamily(owner.Id, member: member.Id);

        var ex = await Fails(h.CreateOrder(member, "family-1m"));

        Assert.Equal(("already_in_family", 409), (ex.Code, ex.StatusCode));
        Assert.Equal("pending", (await h.CreateOrder(member, "personal-1m")).Status);
    }

    [Fact]
    public async Task F29_MemberOfALapsedFamily_CanBuyFamily()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, age: 40);
        var member = h.AddUser();
        h.Store.AddFamily(owner.Id, member: member.Id, startsAt: Now.AddDays(-31));   // kỳ đã hết hạn, nhóm chưa ai đọc nên chưa bị kết thúc

        Assert.Equal("pending", (await h.CreateOrder(member, "family-1m")).Status);
        Assert.Equal(FamilyGroupStatuses.Ended, h.Store.Groups.Single().Status);
    }

    [Fact]
    public async Task OwnerOfACurrentFamily_CanBuyAFamilyRenewal()
    {
        var h = new OrderHarness();
        var owner = h.AddUser(UserRoles.Parent, age: 40);
        h.Store.AddFamily(owner.Id);

        Assert.Equal("pending", (await h.CreateOrder(owner, "family-3m")).Status);
    }

    [Fact]
    public async Task P20_AgeIsCheckedOnlyWhenTheOrderIsCreated()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser(age: 17);
        var order = await h.CreateOrder(buyer, "personal-1m");

        buyer.DateOfBirth = DateOnly.FromDateTime(Now).AddYears(-15);   // admin sửa ngày sinh
        var result = await h.Orders.ApplyPaymentAsync(order.OrderCode, 79_000, null, Now.AddMinutes(1));

        Assert.Equal(PaymentApplyStatus.Activated, result.Status);
    }

    // ---- xem, liệt kê

    [Fact]
    public async Task Get_OwnOrder_AndAnotherUsersOrderIs404()
    {
        var h = new OrderHarness();
        var mine = h.AddUser();
        var other = h.AddUser();
        var order = await h.CreateOrder(mine, "personal-1m");

        Assert.Equal(order.OrderCode, (await h.Orders.GetMineAsync(mine.Id, order.OrderCode)).OrderCode);
        var ex = await Fails(h.Orders.GetMineAsync(other.Id, order.OrderCode));
        Assert.Equal(("order_not_found", 404), (ex.Code, ex.StatusCode));
        Assert.Equal("order_not_found", (await Fails(h.Orders.GetMineAsync(mine.Id, 12345))).Code);
    }

    [Fact]
    public async Task Get_PaidOrder_ShowsItsCycle_AndQueuedFlag()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var first = await h.CreateOrder(buyer, "personal-1m");
        await h.Orders.ApplyPaymentAsync(first.OrderCode, 79_000, null, Now);
        var renewal = await h.CreateOrder(buyer, "personal-3m", Now.AddMinutes(5));
        await h.Orders.ApplyPaymentAsync(renewal.OrderCode, 199_000, null, Now.AddMinutes(6));

        var active = await h.Orders.GetMineAsync(buyer.Id, first.OrderCode);
        var queued = await h.Orders.GetMineAsync(buyer.Id, renewal.OrderCode);

        Assert.Equal(("paid", new OrderCycleDto(Now, Now.AddDays(30), false)), (active.Status, active.Cycle));
        Assert.Equal(("paid", new OrderCycleDto(null, null, true)), (queued.Status, queued.Cycle));
        Assert.Equal(Now, active.PaidAt);
    }

    [Fact]
    public async Task List_NewestFirst_OnlyMine_PagedAndClamped()
    {
        var h = new OrderHarness();
        var me = h.AddUser();
        var other = h.AddUser();
        var codes = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var order = await h.CreateOrder(me, "personal-1m", Now.AddMinutes(i));
            await h.Orders.CancelAsync(me.Id, order.OrderCode, Now.AddMinutes(i));
            codes.Add(order.OrderCode);
        }
        await h.CreateOrder(other, "personal-1m", Now.AddMinutes(10));

        var page1 = await h.Orders.ListMineAsync(me.Id, 1, 2);
        var page3 = await h.Orders.ListMineAsync(me.Id, 3, 2);
        var clampedLow = await h.Orders.ListMineAsync(me.Id, 0, 0);
        var clampedHigh = await h.Orders.ListMineAsync(me.Id, 1, 500);

        Assert.Equal(new[] { codes[4], codes[3] }, page1.Items.Select(o => o.OrderCode));
        Assert.Equal((5, 3, 1, 2), (page1.TotalCount, page1.TotalPages, page3.Items.Count, page1.PageSize));
        Assert.Equal((1, 1, 50), (clampedLow.Page, clampedLow.PageSize, clampedHigh.PageSize));
        Assert.Equal(5, clampedHigh.Items.Count);
    }

    // ---- hủy

    [Fact]
    public async Task P10_CancelPending_CancelsTheLink_AndAllowsANewOrder()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");

        var cancelled = await h.Orders.CancelAsync(buyer.Id, order.OrderCode, Now.AddMinutes(1));

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal("cancelled", h.Order(order.OrderCode).Status);
        Assert.Equal(order.OrderCode, Assert.Single(h.Gateway.Cancelled).Code);
        Assert.Contains(order.OrderCode, h.Store.LockedCodes);
        Assert.Equal("pending", (await h.CreateOrder(buyer, "personal-3m", Now.AddMinutes(2))).Status);
    }

    [Fact]
    public async Task Cancel_GatewayFailureIsIgnored_TheOrderIsStillCancelled()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");
        h.Gateway.FailCancel = true;

        var cancelled = await h.Orders.CancelAsync(buyer.Id, order.OrderCode, Now);

        Assert.Equal("cancelled", cancelled.Status);
    }

    [Theory]
    [InlineData(OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Cancelled)]
    [InlineData(OrderStatuses.Expired)]
    [InlineData(OrderStatuses.Mismatch)]
    public async Task Cancel_NotPending_409NotPending_AndTheStatusIsUntouched(string status)
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");
        h.Order(order.OrderCode).Status = status;

        var ex = await Fails(h.Orders.CancelAsync(buyer.Id, order.OrderCode, Now));

        Assert.Equal(("not_pending", 409), (ex.Code, ex.StatusCode));
        Assert.Equal(status, h.Order(order.OrderCode).Status);
        Assert.Empty(h.Gateway.Cancelled);
    }

    [Fact]
    public async Task Cancel_AnotherUsersOrder_404_AndNothingChanges()
    {
        var h = new OrderHarness();
        var order = await h.CreateOrder(h.AddUser(), "personal-1m");
        var stranger = h.AddUser();

        var ex = await Fails(h.Orders.CancelAsync(stranger.Id, order.OrderCode, Now));

        Assert.Equal(("order_not_found", 404), (ex.Code, ex.StatusCode));
        Assert.Equal("pending", h.Order(order.OrderCode).Status);
        Assert.Empty(h.Gateway.Cancelled);
    }

    // ---- hình dạng JSON (frontend/src/types/billing.ts)

    [Fact]
    public async Task OrderDto_JsonMatchesTheFrontendContract()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(order, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = doc.RootElement;

        Assert.Equal(
            ["amountVnd", "checkoutUrl", "createdAt", "cycle", "description", "expiresAt", "orderCode", "paidAt", "planCode", "planKind", "planName", "qrCode", "status"],
            root.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Number, root.GetProperty("amountVnd").ValueKind);
        Assert.Equal("79000", root.GetProperty("amountVnd").GetRawText());   // số nguyên, không phải 79000.00
        Assert.Equal(JsonValueKind.Number, root.GetProperty("orderCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cycle").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("paidAt").ValueKind);
        Assert.Equal("personal", root.GetProperty("planKind").GetString());
    }

    [Fact]
    public void OrderDto_HasNoTokenField()
    {
        foreach (var type in new[] { typeof(OrderDto), typeof(OrderCycleDto) })
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("quota", property.Name, StringComparison.OrdinalIgnoreCase);
            }
    }
}
