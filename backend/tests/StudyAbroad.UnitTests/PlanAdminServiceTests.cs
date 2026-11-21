using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.UnitTests;

// Admin quản lý gói (UC-79): các case A1 đến A5, A11, P8, T9 của kế hoạch billing v2.
public class PlanAdminServiceTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    // ---- fake

    private sealed class FakePlanAdminRepository : IPlanAdminRepository
    {
        public List<SubscriptionPlan> Plans { get; } = [];
        public List<UserSubscription> Cycles { get; } = [];
        public List<PaymentTransaction> Orders { get; } = [];
        /// <summary>Mọi lần ghi (add, save, remove). Kỳ và đơn không có đường ghi nào qua repository này, nên chỉ có dòng gói bị đổi.</summary>
        public List<string> Writes { get; } = [];
        /// <summary>Lỗi ràng buộc DB mà repository thật ném khi hai request đua nhau (đơn hoặc kỳ hoặc gói trùng xuất hiện giữa lúc kiểm tra và lúc ghi).</summary>
        public PlanConflictException? ThrowOnAdd { get; set; }
        public PlanConflictException? ThrowOnRemove { get; set; }

        private PlanWithUsage WithUsage(SubscriptionPlan p) => new(p, new PlanUsage(
            Cycles.Count(c => c.SubscriptionPlanId == p.Id && c.Status == CycleStatuses.Active),
            Cycles.Count(c => c.SubscriptionPlanId == p.Id),
            Orders.Count(o => o.SubscriptionPlanId == p.Id)));

        public Task<IReadOnlyList<PlanWithUsage>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlanWithUsage>>(Plans.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name, StringComparer.Ordinal).Select(WithUsage).ToList());

        public Task<PlanWithUsage?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(p => p.Id == id) is { } p ? WithUsage(p) : null);

        public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => Task.FromResult(Plans.Any(p => p.Code == code));
        public Task<bool> KindExistsAsync(string kind, CancellationToken ct = default) => Task.FromResult(Plans.Any(p => p.Kind == kind));

        public Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
        {
            if (ThrowOnAdd is not null)
                throw ThrowOnAdd;
            Writes.Add("add");
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            Writes.Add("save");
            return Task.CompletedTask;
        }

        public Task RemoveAsync(SubscriptionPlan plan, CancellationToken ct = default)
        {
            if (ThrowOnRemove is not null)
                throw ThrowOnRemove;
            Writes.Add("remove");
            Plans.Remove(plan);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public FakePlanAdminRepository Repo { get; } = new();
        public FakeAuditLogRepository Audit { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public PlanAdminService Service => new(Repo, Audit, Uow);

        public SubscriptionPlan Free { get; }
        public SubscriptionPlan Personal { get; }
        public SubscriptionPlan Family { get; }

        public Fixture()
        {
            Free = Plan("free", PlanKinds.Free, 0, null, 100_000, order: 0);
            Personal = Plan("personal-1m", PlanKinds.Personal, 99_000, 30, 500_000, order: 1, quotaPeriodDays: 30);
            Family = Plan("family-1m", PlanKinds.Family, 199_000, 30, 1_500_000, order: 2, quotaPeriodDays: 30);
            Repo.Plans.AddRange([Free, Personal, Family]);
        }

        private static SubscriptionPlan Plan(string code, string kind, decimal price, int? days, long quota, int order, int? quotaPeriodDays = null) => new()
        {
            Code = code, Name = code, Kind = kind, PriceVnd = price, DurationDays = days, QuotaPeriodDays = quotaPeriodDays, TokenQuota = quota,
            DisplayOrder = order, IsActive = true,
        };

        public UserSubscription AddCycle(SubscriptionPlan plan, string status)
        {
            var cycle = new UserSubscription
            {
                UserId = Guid.NewGuid(), SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = status, TokenQuota = plan.TokenQuota,
                PlanName = plan.Name, PriceVnd = plan.PriceVnd, DurationDays = plan.DurationDays ?? 30,
            };
            Repo.Cycles.Add(cycle);
            return cycle;
        }

        public PaymentTransaction AddOrder(SubscriptionPlan plan, string status)
        {
            var order = new PaymentTransaction
            {
                UserId = Guid.NewGuid(), SubscriptionPlanId = plan.Id, Status = status, AmountVnd = plan.PriceVnd, PlanKind = plan.Kind,
                PlanName = plan.Name, PlanDurationDays = plan.DurationDays ?? 30, PlanTokenQuota = plan.TokenQuota,
            };
            Repo.Orders.Add(order);
            return order;
        }
    }

    private static SavePlanRequest Create(
        string? code = "test-2k", string? kind = PlanKinds.Personal, string? name = "Thử 2k", string? description = "Gói thử",
        long price = 2000, int? days = 30, int? quotaPeriodDays = 30, long quota = 3000, bool active = true, int order = 5) =>
        new(code, kind, name, description, price, days, quotaPeriodDays, quota, active, order);

    private static async Task<BillingRuleException> Fails(Task task)
    {
        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => task);
        return ex;
    }

    private static void AssertRule(BillingRuleException ex, string code, int status) => Assert.Equal((code, status), (ex.Code, ex.StatusCode));

    // ---- list

    [Fact]
    public async Task List_OrdersByDisplayOrderThenName_AndCountsUsage()
    {
        var f = new Fixture();
        f.Personal.DisplayOrder = 0;   // cùng thứ tự với Free: xếp theo tên ("free" < "personal-1m")
        f.AddCycle(f.Personal, CycleStatuses.Active);
        f.AddCycle(f.Personal, CycleStatuses.Active);
        f.AddCycle(f.Personal, CycleStatuses.Ended);   // kỳ đã kết thúc không tính vào activeCycles
        f.AddOrder(f.Personal, OrderStatuses.Paid);
        f.AddOrder(f.Personal, OrderStatuses.Cancelled);
        f.AddOrder(f.Personal, OrderStatuses.Pending);

        var list = await f.Service.ListAsync();

        Assert.Equal(["free", "personal-1m", "family-1m"], list.Select(p => p.Code));
        var personal = list[1];
        Assert.Equal((2, 3), (personal.ActiveCycles, personal.OrderCount));
        Assert.Equal((0, 0), (list[0].ActiveCycles, list[0].OrderCount));
    }

    [Fact]
    public async Task List_FamilyPlanHasSeats_OthersNull_PriceIsAnInteger()
    {
        var f = new Fixture();

        var list = await f.Service.ListAsync();

        Assert.Null(list.Single(p => p.Kind == PlanKinds.Personal).Seats);
        Assert.Null(list.Single(p => p.Kind == PlanKinds.Free).Seats);
        Assert.Equal(new PlanSeatsDto(2, 2), list.Single(p => p.Kind == PlanKinds.Family).Seats);
        var json = JsonSerializer.Serialize(list.Single(p => p.Kind == PlanKinds.Personal), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"priceVnd\":99000,", json);
        Assert.Contains("\"tokenQuota\":500000", json);
    }

    // ---- create

    [Fact]
    public async Task Create_ValidPersonalPlan_IsStoredAndAudited()
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create());

        var stored = f.Repo.Plans.Single(p => p.Code == "test-2k");
        Assert.Equal(stored.Id, dto.Id);
        Assert.Equal((PlanKinds.Personal, 2000L, 30, (int?)30, 3000L, true, 5), (dto.Kind, dto.PriceVnd, dto.DurationDays, dto.QuotaPeriodDays, dto.TokenQuota, dto.IsActive, dto.DisplayOrder));
        Assert.Equal((0, 0), (dto.ActiveCycles, dto.OrderCount));
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task A5_Create_WritesAuditRowWithNullBeforeAndTheNewPlanAfter()
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create());

        var row = Assert.Single(f.Audit.Entries);
        Assert.Equal(("plan.create", "subscription_plan", dto.Id.ToString(), Admin), (row.Action, row.EntityType, row.EntityId, row.ActorUserId));
        using var json = JsonDocument.Parse(row.DetailJson!);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("before").ValueKind);
        var after = json.RootElement.GetProperty("after");
        Assert.Equal(("test-2k", 2000L, 3000L), (after.GetProperty("code").GetString(), after.GetProperty("priceVnd").GetInt64(), after.GetProperty("tokenQuota").GetInt64()));
    }

    [Fact]
    public async Task A1_CreateWithExistingCode_ReturnsCodeTaken()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(code: "personal-1m"))), "code_taken", 409);

        Assert.Equal(3, f.Repo.Plans.Count);
        Assert.Empty(f.Audit.Entries);
    }

    // Hai admin tạo cùng một mã gần như cùng lúc: cả hai qua bước kiểm tra, index duy nhất chặn bên chậm hơn → 409 chứ không phải 500
    [Fact]
    public async Task A1_CreateLosesTheRaceOnTheCodeIndex_ReturnsCodeTaken_AndWritesNothing()
    {
        var f = new Fixture();
        f.Repo.ThrowOnAdd = new PlanConflictException(PlanConflict.CodeTaken);

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create())), "code_taken", 409);

        Assert.Equal(3, f.Repo.Plans.Count);
        Assert.Empty(f.Audit.Entries);   // lỗi ném trong transaction nên audit không được ghi
    }

    [Fact]
    public async Task A2_CreateLosesTheRaceOnTheSingleFreeIndex_ReturnsFreePlanExists()
    {
        var f = new Fixture();
        f.Repo.ThrowOnAdd = new PlanConflictException(PlanConflict.FreePlanExists);

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(code: "free-2", kind: PlanKinds.Free, price: 0, days: null, quotaPeriodDays: null))), "free_plan_exists", 409);

        Assert.Empty(f.Audit.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Test-2k")]
    [InlineData("test_2k")]
    [InlineData("test 2k")]
    [InlineData("-test")]
    [InlineData("test-")]
    [InlineData("test--2k")]
    public async Task Create_BadCode_ReturnsInvalidCode(string? code)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(code: code))), "invalid_code", 400);
    }

    [Fact]
    public async Task Create_CodeLongerThan64_ReturnsInvalidCode_Exactly64IsFine()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(code: new string('a', 65)))), "invalid_code", 400);
        await f.Service.CreateAsync(Admin, Create(code: new string('a', 64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pro")]
    [InlineData("Personal")]
    public async Task Create_UnknownKind_ReturnsInvalidKind(string? kind)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(kind: kind))), "invalid_kind", 400);
    }

    [Fact]
    public async Task Create_SecondFreePlan_ReturnsFreePlanExists()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(code: "free-2", kind: PlanKinds.Free, price: 0, days: null, quotaPeriodDays: null))), "free_plan_exists", 409);
    }

    [Fact]
    public async Task Create_FreePlanWhenNoneExists_StoresNoDurationWhateverIsSent()
    {
        var f = new Fixture();
        f.Repo.Plans.Remove(f.Free);

        var dto = await f.Service.CreateAsync(Admin, Create(code: "free", kind: PlanKinds.Free, price: 0, days: 30, quotaPeriodDays: null));

        Assert.Null(dto.DurationDays);
        Assert.Null(f.Repo.Plans.Single(p => p.Code == "free").DurationDays);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_EmptyName_ReturnsInvalidName(string? name)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(name: name))), "invalid_name", 400);
    }

    [Fact]
    public async Task Create_NameOf129Chars_ReturnsInvalidName_128IsFine()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(name: new string('n', 129)))), "invalid_name", 400);
        await f.Service.CreateAsync(Admin, Create(name: new string('n', 128)));
    }

    [Fact]
    public async Task Create_DescriptionOver1000_ReturnsInvalidValue_NullAndBlankBecomeNull()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(description: new string('d', 1001)))), "invalid_value", 400);
        var dto = await f.Service.CreateAsync(Admin, Create(description: "  "));
        Assert.Null(dto.Description);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(10_000_000_000, 0)]   // vượt numeric(12,2)
    public async Task Create_NegativeOrOversizedNumbers_ReturnInvalidValue(long price, long quota)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(price: price, quota: quota))), "invalid_value", 400);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    public async Task Create_PaidPlanDurationOutOfRange_ReturnsInvalidDuration(int? days)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(days: days, quotaPeriodDays: null))), "invalid_duration", 400);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(365)]
    [InlineData(3650)]
    public async Task Create_DynamicPaidDurations_Accepted(int days)
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create(days: days, quotaPeriodDays: null));

        Assert.Equal(days, dto.DurationDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(15)]   // > durationDays (14)
    public async Task Create_PaidQuotaPeriodOutOfRange_ReturnsInvalidQuotaPeriod(int quotaPeriodDays)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.CreateAsync(Admin, Create(days: 14, quotaPeriodDays: quotaPeriodDays))), "invalid_quota_period", 400);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    public async Task Create_PaidQuotaPeriodValid_Accepted(int? quotaPeriodDays)
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create(days: 14, quotaPeriodDays: quotaPeriodDays));

        Assert.Equal(quotaPeriodDays, dto.QuotaPeriodDays);
    }

    [Fact]
    public async Task Create_ComputesQuotaMultiplierAgainstFreePlan()
    {
        var f = new Fixture();
        // Free = 100_000 / 30 days; new plan = 500_000 / 30 days -> 5.0x
        var dto = await f.Service.CreateAsync(Admin, Create(days: 90, quotaPeriodDays: 30, quota: 500_000));

        Assert.Equal(5.0m, dto.QuotaMultiplier);
    }

    [Fact]
    public async Task Create_FreeOfChargePersonalPlan_Accepted()
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create(price: 0, quota: 0));

        Assert.Equal(0, dto.PriceVnd);
        Assert.Null(dto.QuotaMultiplier);
    }

    [Fact]
    public async Task Create_FamilyPlan_ReturnsSeats()
    {
        var f = new Fixture();

        var dto = await f.Service.CreateAsync(Admin, Create(code: "family-3m", kind: PlanKinds.Family, days: 90));

        Assert.Equal(new PlanSeatsDto(2, 2), dto.Seats);
    }

    // ---- update

    [Fact]
    public async Task A5_Update_WritesAuditRowWithBeforeAndAfter()
    {
        var f = new Fixture();

        await f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, kind: null, name: "Cá nhân mới", price: 120_000, days: 90, quota: 700_000));

        var row = Assert.Single(f.Audit.Entries);
        Assert.Equal(("plan.update", "subscription_plan", f.Personal.Id.ToString(), Admin), (row.Action, row.EntityType, row.EntityId, row.ActorUserId));
        using var json = JsonDocument.Parse(row.DetailJson!);
        var before = json.RootElement.GetProperty("before");
        var after = json.RootElement.GetProperty("after");
        Assert.Equal((99_000L, 30, "personal-1m"), (before.GetProperty("priceVnd").GetInt64(), before.GetProperty("durationDays").GetInt32(), before.GetProperty("name").GetString()));
        Assert.Equal((120_000L, 90, "Cá nhân mới", 700_000L), (after.GetProperty("priceVnd").GetInt64(), after.GetProperty("durationDays").GetInt32(), after.GetProperty("name").GetString(), after.GetProperty("tokenQuota").GetInt64()));
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Update_ChangesEveryEditableField()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Personal.Id,
            Create(code: null, kind: null, name: "Mới", description: "Mô tả mới", price: 150_000, days: 90, quotaPeriodDays: 15, quota: 900_000, active: false, order: 9));

        Assert.Equal(("Mới", "Mô tả mới", 150_000L, 90, (int?)15, 900_000L, false, 9), (dto.Name, dto.Description, dto.PriceVnd, dto.DurationDays, dto.QuotaPeriodDays, dto.TokenQuota, dto.IsActive, dto.DisplayOrder));
        Assert.Equal(18.0m, dto.QuotaMultiplier);   // (900k/15) / (100k/30) = 18.0
        Assert.Equal(150_000m, f.Personal.PriceVnd);
        Assert.False(f.Personal.IsActive);
    }

    [Fact]
    public async Task A2_UpdateWithDifferentKind_ReturnsKindImmutable()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, kind: PlanKinds.Family))), "kind_immutable", 409);

        Assert.Equal(PlanKinds.Personal, f.Personal.Kind);
        Assert.Empty(f.Audit.Entries);
        Assert.Empty(f.Repo.Writes);
    }

    [Theory]
    [InlineData(PlanKinds.Personal)]
    [InlineData(null)]
    [InlineData("")]
    public async Task Update_SameOrMissingKind_IsFine(string? kind)
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, kind: kind));

        Assert.Equal(PlanKinds.Personal, dto.Kind);
    }

    [Fact]
    public async Task Update_NeverChangesTheCode()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: "something-else", kind: null));

        Assert.Equal("personal-1m", dto.Code);
        Assert.Equal("personal-1m", f.Personal.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public async Task Update_PaidPlanDurationOutOfRange_ReturnsInvalidDuration(int days)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, days: days, quotaPeriodDays: null))), "invalid_duration", 400);

        Assert.Equal(30, f.Personal.DurationDays);
    }

    [Fact]
    public async Task Update_InvalidFields_ChangeNothing()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, name: ""))), "invalid_name", 400);
        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, quota: -5))), "invalid_value", 400);

        Assert.Equal((99_000m, 500_000L, "personal-1m"), (f.Personal.PriceVnd, f.Personal.TokenQuota, f.Personal.Name));
        Assert.Empty(f.Audit.Entries);
        Assert.Empty(f.Repo.Writes);
    }

    [Fact]
    public async Task Update_UnknownPlan_Returns404()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, Guid.NewGuid(), Create(code: null))), "plan_not_found", 404);
    }

    [Fact]
    public async Task Update_ReturnsUsageCounts()
    {
        var f = new Fixture();
        f.AddCycle(f.Personal, CycleStatuses.Active);
        f.AddOrder(f.Personal, OrderStatuses.Paid);
        f.AddOrder(f.Personal, OrderStatuses.Expired);

        var dto = await f.Service.UpdateAsync(Admin, f.Personal.Id, Create(code: null, kind: null));

        Assert.Equal((1, 2), (dto.ActiveCycles, dto.OrderCount));
    }

    // ---- gói Free

    [Fact]
    public async Task A3_Free_FrontendBody_DurationNullPriceZeroActive_Succeeds()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Free.Id,
            Create(code: null, kind: null, name: "Miễn phí", description: "Hạn mức hằng tháng", price: 0, days: null, quotaPeriodDays: null, quota: 150_000, active: true, order: 0));

        Assert.Equal(("Miễn phí", 0L, (int?)null, (int?)null, 150_000L, true), (dto.Name, dto.PriceVnd, dto.DurationDays, dto.QuotaPeriodDays, dto.TokenQuota, dto.IsActive));
        Assert.Null(dto.QuotaMultiplier);
        Assert.Equal(150_000, f.Free.TokenQuota);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task A3_Free_QuotaPeriodOutOfRange_ReturnsInvalidQuotaPeriod(int quotaPeriodDays)
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: null, price: 0, days: null, quotaPeriodDays: quotaPeriodDays))), "invalid_quota_period", 400);
    }

    [Fact]
    public async Task A3_Free_ValidQuotaPeriod_Accepted()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: null, price: 0, days: null, quotaPeriodDays: 7));

        Assert.Equal(7, dto.QuotaPeriodDays);
    }

    [Fact]
    public async Task A3_Free_FrontendBodyWithKindFree_Succeeds()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: PlanKinds.Free, price: 0, days: null, quotaPeriodDays: null));

        Assert.Equal(PlanKinds.Free, dto.Kind);
    }

    [Fact]
    public async Task A3_Free_DurationSentIsIgnoredAndStoredNull()
    {
        var f = new Fixture();

        var dto = await f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: null, price: 0, days: 30, quotaPeriodDays: null));

        Assert.Null(dto.DurationDays);
        Assert.Null(f.Free.DurationDays);
    }

    [Fact]
    public async Task A3_Free_NonZeroPrice_ReturnsFreePlanLocked()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: null, price: 1000, days: null, quotaPeriodDays: null))), "free_plan_locked", 409);

        Assert.Equal(0m, f.Free.PriceVnd);
        Assert.Empty(f.Audit.Entries);
    }

    [Fact]
    public async Task A3_Free_StopSelling_ReturnsFreePlanLocked()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.UpdateAsync(Admin, f.Free.Id, Create(code: null, kind: null, price: 0, days: null, quotaPeriodDays: null, active: false))), "free_plan_locked", 409);

        Assert.True(f.Free.IsActive);
    }

    [Fact]
    public async Task A3_DeleteFree_ReturnsFreePlanLocked()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.DeleteAsync(Admin, f.Free.Id)), "free_plan_locked", 409);

        Assert.Contains(f.Free, f.Repo.Plans);
        Assert.Empty(f.Audit.Entries);
    }

    // ---- delete

    [Fact]
    public async Task Delete_UnusedPlan_IsRemovedAndAudited()
    {
        var f = new Fixture();
        var dto = await f.Service.CreateAsync(Admin, Create());
        f.Audit.Entries.Clear();

        await f.Service.DeleteAsync(Admin, dto.Id);

        Assert.DoesNotContain(f.Repo.Plans, p => p.Code == "test-2k");
        var row = Assert.Single(f.Audit.Entries);
        Assert.Equal(("plan.delete", "subscription_plan", dto.Id.ToString(), Admin), (row.Action, row.EntityType, row.EntityId, row.ActorUserId));
        using var json = JsonDocument.Parse(row.DetailJson!);
        Assert.Equal("test-2k", json.RootElement.GetProperty("before").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("after").ValueKind);
    }

    [Theory]
    [InlineData(OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Pending)]
    [InlineData(OrderStatuses.Cancelled)]
    [InlineData(OrderStatuses.Expired)]
    public async Task A4_DeletePlanWithAnOrder_ReturnsPlanInUse(string orderStatus)
    {
        var f = new Fixture();
        f.AddOrder(f.Personal, orderStatus);

        AssertRule(await Fails(f.Service.DeleteAsync(Admin, f.Personal.Id)), "plan_in_use", 409);

        Assert.Contains(f.Personal, f.Repo.Plans);
        Assert.Empty(f.Audit.Entries);
    }

    [Theory]
    [InlineData(CycleStatuses.Active)]
    [InlineData(CycleStatuses.Ended)]
    public async Task A4_DeletePlanWithACycle_ReturnsPlanInUse(string cycleStatus)
    {
        var f = new Fixture();
        f.AddCycle(f.Family, cycleStatus);

        AssertRule(await Fails(f.Service.DeleteAsync(Admin, f.Family.Id)), "plan_in_use", 409);

        Assert.Contains(f.Family, f.Repo.Plans);
    }

    // Một đơn hoặc kỳ được tạo giữa lúc đếm và lúc xóa: khóa ngoại Restrict chặn → 409 plan_in_use chứ không phải 500
    [Fact]
    public async Task A4_DeleteHitsAForeignKeyViolation_ReturnsPlanInUse_AndWritesNoAudit()
    {
        var f = new Fixture();
        f.Repo.ThrowOnRemove = new PlanConflictException(PlanConflict.InUse);

        AssertRule(await Fails(f.Service.DeleteAsync(Admin, f.Personal.Id)), "plan_in_use", 409);

        Assert.Contains(f.Personal, f.Repo.Plans);
        Assert.Empty(f.Audit.Entries);
    }

    // Repository thật nhận ra lỗi Postgres theo cách OrderRepository.IsConflict nhận ra vi phạm index: theo SqlState trong DbUpdateException
    private static DbUpdateException Pg(string sqlState, string? constraint = null) =>
        new("save", new PostgresException("lỗi", "ERROR", "ERROR", sqlState, constraintName: constraint));

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "ix_subscription_plans_code", PlanConflict.CodeTaken)]
    [InlineData(PostgresErrorCodes.UniqueViolation, PlanAdminRepository.SingleFreeIndex, PlanConflict.FreePlanExists)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "fk_user_subscriptions_subscription_plans_subscription_plan_id", PlanConflict.InUse)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "fk_payment_transactions_subscription_plans_subscription_plan_id", PlanConflict.InUse)]
    public void Repository_ClassifiesPostgresConstraintViolations(string sqlState, string constraint, PlanConflict expected) =>
        Assert.Equal(expected, PlanAdminRepository.Classify(Pg(sqlState, constraint)));

    [Fact]
    public void Repository_LeavesOtherFailuresAlone()
    {
        Assert.Null(PlanAdminRepository.Classify(Pg(PostgresErrorCodes.CheckViolation, "ck_subscription_plans_duration")));
        Assert.Null(PlanAdminRepository.Classify(new DbUpdateException("save", new InvalidOperationException())));
        Assert.Null(PlanAdminRepository.Classify(new InvalidOperationException("khác")));
    }

    [Fact]
    public async Task Delete_UnknownPlan_Returns404()
    {
        var f = new Fixture();

        AssertRule(await Fails(f.Service.DeleteAsync(Admin, Guid.NewGuid())), "plan_not_found", 404);
    }

    // ---- P8, T9: đổi gói không đụng kỳ và đơn đã có

    [Fact]
    public async Task P8_T9_ChangingPriceDurationAndQuota_TouchesOnlyThePlanRow()
    {
        var f = new Fixture();
        var cycle = f.AddCycle(f.Personal, CycleStatuses.Active);
        var second = f.AddCycle(f.Personal, CycleStatuses.Active);
        var pending = f.AddOrder(f.Personal, OrderStatuses.Pending);

        await f.Service.UpdateAsync(Admin, f.Personal.Id,
            Create(code: null, kind: null, name: "Cá nhân", price: 50_000, days: 90, quotaPeriodDays: 30, quota: 100_000));

        Assert.Equal((500_000L, "personal-1m", 99_000m, 30), (cycle.TokenQuota, cycle.PlanName, cycle.PriceVnd, cycle.DurationDays));
        Assert.Equal((500_000L, 99_000m), (second.TokenQuota, second.PriceVnd));
        Assert.Equal((99_000m, 30, 500_000L, "personal-1m"), (pending.AmountVnd, pending.PlanDurationDays, pending.PlanTokenQuota, pending.PlanName));
        Assert.Equal(["save"], f.Repo.Writes);
        Assert.Equal((50_000m, 90, 100_000L), (f.Personal.PriceVnd, f.Personal.DurationDays, f.Personal.TokenQuota));
    }

    [Fact]
    public async Task P8_StoppingSales_LeavesCyclesAndOrdersAlone()
    {
        var f = new Fixture();
        var cycle = f.AddCycle(f.Family, CycleStatuses.Active);
        var pending = f.AddOrder(f.Family, OrderStatuses.Pending);

        var dto = await f.Service.UpdateAsync(Admin, f.Family.Id, Create(code: null, kind: null, days: 30, quota: 1_500_000, price: 199_000, active: false));

        Assert.False(dto.IsActive);
        Assert.Equal((CycleStatuses.Active, OrderStatuses.Pending), (cycle.Status, pending.Status));
        Assert.Equal((1, 1), (dto.ActiveCycles, dto.OrderCount));
    }

    // ---- A11: quyền

    [Fact]
    public void A11_Controller_IsAdminOnly_EveryActionHasAnHttpVerb_NoAnonymousOverride()
    {
        var type = typeof(AdminPlansController);

        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(UserRoles.Admin, authorize!.Roles);
        Assert.Equal("api/v1/admin/plans", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();
        Assert.Equal(4, actions.Count);
        foreach (var action in actions)
        {
            Assert.NotNull(action.GetCustomAttributes<HttpMethodAttribute>().SingleOrDefault());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
        Assert.Equal(["DELETE", "GET", "POST", "PUT"], actions.Select(a => a.GetCustomAttribute<HttpMethodAttribute>()!.HttpMethods.Single()).Order());
    }

    // ---- controller

    private static AdminPlansController ControllerFor(IPlanAdminService service, Guid? userId)
    {
        var identity = userId is { } id
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, UserRoles.Admin)], "test")
            : new ClaimsIdentity();
        return new AdminPlansController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    [Fact]
    public async Task Controller_Create_Returns201WithThePlan_UpdateReturns200_DeleteReturns204()
    {
        var f = new Fixture();
        var controller = ControllerFor(f.Service, Admin);

        var created = Assert.IsType<ObjectResult>(await controller.Create(Create(), default));
        var dto = Assert.IsType<AdminPlanDto>(created.Value);
        Assert.Equal(201, created.StatusCode);

        var updated = Assert.IsType<OkObjectResult>(await controller.Update(dto.Id, Create(code: null, name: "Đổi tên"), default));
        Assert.Equal("Đổi tên", Assert.IsType<AdminPlanDto>(updated.Value).Name);

        Assert.IsType<NoContentResult>(await controller.Delete(dto.Id, default));
        Assert.DoesNotContain(f.Repo.Plans, p => p.Id == dto.Id);
        Assert.Equal(["plan.create", "plan.update", "plan.delete"], f.Audit.Entries.Select(e => e.Action));
        Assert.All(f.Audit.Entries, e => Assert.Equal(Admin, e.ActorUserId));
    }

    [Fact]
    public async Task Controller_WithoutUserId_ReturnsUnauthorized()
    {
        var f = new Fixture();
        var controller = ControllerFor(f.Service, null);

        Assert.IsType<UnauthorizedResult>(await controller.Create(Create(), default));
        Assert.IsType<UnauthorizedResult>(await controller.Update(f.Personal.Id, Create(code: null), default));
        Assert.IsType<UnauthorizedResult>(await controller.Delete(f.Personal.Id, default));
        Assert.Empty(f.Audit.Entries);
    }
}
