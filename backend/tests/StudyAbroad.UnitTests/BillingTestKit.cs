using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>Ném thay cho vi phạm index duy nhất của Postgres (IOrderRepository.IsConflict nhận ra).</summary>
internal sealed class UniqueViolationException(string message) : Exception(message);

/// <summary>
/// Cơ sở dữ liệu trong bộ nhớ cho test đơn hàng: cùng một đối tượng là IBillingRepository, IOrderRepository và IUnitOfWork như DbContext dùng chung.
/// Giữ các luật của DB mà tiền phụ thuộc vào: một active và một queued mỗi làn, một nhóm active mỗi chủ, một đơn chờ mỗi người,
/// mã đơn duy nhất, FOR UPDATE chỉ dùng trong transaction, và transaction lỗi thì quay về trạng thái trước (thực thể đã trả ra thành cũ).
/// </summary>
internal sealed class BillingStore : IBillingRepository, IOrderRepository, IUnitOfWork
{
    public static readonly DateTime Now = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);

    public List<SubscriptionPlan> Plans { get; } =
    [
        new() { Code = "free", Name = "Miễn phí", Kind = PlanKinds.Free, TokenQuota = 120_000, DisplayOrder = 0 },
        new() { Code = "personal-1m", Name = "Cá nhân 1 tháng", Kind = PlanKinds.Personal, PriceVnd = 79_000, DurationDays = 30, TokenQuota = 500_000, DisplayOrder = 1 },
        new() { Code = "personal-3m", Name = "Cá nhân 3 tháng", Kind = PlanKinds.Personal, PriceVnd = 199_000, DurationDays = 90, TokenQuota = 1_800_000, DisplayOrder = 2 },
        new() { Code = "family-1m", Name = "Gia đình 1 tháng", Kind = PlanKinds.Family, PriceVnd = 99_000, DurationDays = 30, TokenQuota = 500_000, DisplayOrder = 3 },
        new() { Code = "family-3m", Name = "Gia đình 3 tháng", Kind = PlanKinds.Family, PriceVnd = 249_000, DurationDays = 90, TokenQuota = 1_800_000, DisplayOrder = 4 },
    ];
    public List<UserSubscription> Cycles { get; private set; } = [];
    public List<FamilyGroup> Groups { get; private set; } = [];
    public List<FamilyMember> Members { get; private set; } = [];
    public List<AiCall> Calls { get; } = [];
    public List<PaymentTransaction> Orders { get; private set; } = [];
    public Dictionary<Guid, string> Names { get; } = [];

    public int Transactions { get; private set; }
    public int Saves { get; private set; }
    public int TrackingCleared { get; private set; }
    public List<long> LockedCodes { get; } = [];

    /// <summary>Một request khác ghi trước khi bước lưu kỳ đầu tiên của transaction này (chạy một lần).</summary>
    public Action<BillingStore>? RaceBeforeCycleInsert { get; set; }
    /// <summary>Một request khác ghi trước khi đơn mới được thêm (chạy một lần).</summary>
    public Action<BillingStore>? RaceBeforeOrderInsert { get; set; }
    /// <summary>Mọi lần lưu kỳ mới đều vướng index (để kiểm lần thử lại cuối cùng cũng thất bại thì báo lỗi).</summary>
    public bool AlwaysConflictOnCycleInsert { get; set; }
    /// <summary>Ném lỗi này ở lần lưu kỳ mới đầu tiên (lỗi không phải xung đột thì không được thử lại).</summary>
    public Exception? FailOnCycleInsert { get; set; }

    private readonly List<UserSubscription> stagedCycles = [];
    private readonly List<FamilyGroup> stagedGroups = [];
    private readonly List<Action<BillingStore>> externalWrites = [];
    private bool inTransaction;

    // ---- dựng dữ liệu

    public SubscriptionPlan Plan(string code) => Plans.Single(p => p.Code == code);

    public UserSubscription AddActive(Guid owner, string planCode, DateTime? startsAt = null, long? quota = null)
    {
        var plan = Plan(planCode);
        var start = startsAt ?? Now.AddDays(-1);
        var cycle = new UserSubscription
        {
            UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = CycleStatuses.Active, StartsAt = start,
            EndsAt = start.AddDays(plan.DurationDays!.Value), DurationDays = plan.DurationDays!.Value, TokenQuota = quota ?? plan.TokenQuota, PlanName = plan.Name,
            PriceVnd = plan.PriceVnd,
        };
        Cycles.Add(cycle);
        return cycle;
    }

    public UserSubscription AddQueued(Guid owner, string planCode)
    {
        var plan = Plan(planCode);
        var cycle = new UserSubscription
        {
            UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = CycleStatuses.Queued,
            DurationDays = plan.DurationDays!.Value, TokenQuota = plan.TokenQuota, PlanName = plan.Name, PriceVnd = plan.PriceVnd,
            FamilyGroupId = plan.Kind == PlanKinds.Family ? Groups.First(g => g.OwnerUserId == owner && g.Status == FamilyGroupStatuses.Active).Id : null,
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
                FamilyGroupId = group.Id, UserId = m, InvitedEmail = "m@x.vn", SeatRole = FamilySeats.Student, Status = FamilyMemberStatuses.Active, TokenHash = "h",
            });
        var cycle = AddActive(owner, "family-1m", startsAt);
        cycle.FamilyGroupId = group.Id;
        return cycle;
    }

    public void Use(Guid userId, UserSubscription cycle, int tokens) => Calls.Add(new AiCall
    {
        UserId = userId, Kind = BillingRules.MeteredAiKind, TotalTokens = tokens, UserSubscriptionId = cycle.Id, PlanCode = Plans.Single(p => p.Id == cycle.SubscriptionPlanId).Code,
        CreatedAt = Now.AddHours(-1),
    });

    // ---- IBillingRepository

    public Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SubscriptionPlan>>(Plans.ToList());

    public Task<List<UserSubscription>> GetOpenCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default) =>
        Task.FromResult(Cycles.Where(c => c.UserId == ownerUserId && c.Kind == kind
            && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued)).ToList());

    public Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default) =>
        Task.FromResult(Groups.FirstOrDefault(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active));

    public Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Groups.FirstOrDefault(g => g.Status == FamilyGroupStatuses.Active
            && Members.Any(m => m.FamilyGroupId == g.Id && m.UserId == userId && m.Status == FamilyMemberStatuses.Active)));

    public Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default) =>
        Task.FromResult(Members.Where(m => m.FamilyGroupId == familyGroupId
            && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)).ToList());

    public Task<long> SumCycleTokensAsync(Guid cycleId, CancellationToken ct = default) =>
        Task.FromResult(Calls.AsQueryable().Where(MeteringFilters.Cycle(cycleId)).Sum(a => (long)(a.TotalTokens ?? 0)));

    public Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default) =>
        Task.FromResult(Calls.AsQueryable().Where(MeteringFilters.Free(userId, fromUtc)).Sum(a => (long)(a.TotalTokens ?? 0)));

    public Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<string?>(Names.TryGetValue(userId, out var n) ? n : null);

    public Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Orders.Where(o => o.UserId == userId && o.Status == OrderStatuses.Pending).Select(o => (long?)o.OrderCode).FirstOrDefault());

    public Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default)
    {
        foreach (var step in steps)
        {
            step();
            Flush();
        }
        return Task.CompletedTask;
    }

    // ---- IOrderRepository

    public Task<PaymentTransaction?> FindByCodeAsync(long orderCode, CancellationToken ct = default) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.OrderCode == orderCode));

    public Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> ListByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var all = Orders.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderCode).ToList();
        return Task.FromResult<(IReadOnlyList<PaymentTransaction>, int)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<IReadOnlyDictionary<Guid, UserSubscription>> GetCyclesAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UserSubscription>>(Cycles.Where(c => cycleIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<OrderInsertResult> TryAddAsync(PaymentTransaction order, CancellationToken ct = default)
    {
        Assert.False(inTransaction, "Thêm đơn không được nằm trong transaction ngoài (vi phạm index làm hỏng transaction)");
        if (RaceBeforeOrderInsert is { } race)
        {
            RaceBeforeOrderInsert = null;
            race(this);
        }
        if (Orders.Any(o => o.OrderCode == order.OrderCode))
            return Task.FromResult(OrderInsertResult.CodeTaken);
        if (order.Status == OrderStatuses.Pending && Orders.Any(o => o.UserId == order.UserId && o.Status == OrderStatuses.Pending))
            return Task.FromResult(OrderInsertResult.PendingExists);
        if (!Orders.Contains(order))
            Orders.Add(order);
        return Task.FromResult(OrderInsertResult.Added);
    }

    public Task<PaymentTransaction?> LockByCodeAsync(long orderCode, CancellationToken ct = default)
    {
        Assert.True(inTransaction, "SELECT ... FOR UPDATE phải chạy trong transaction");
        LockedCodes.Add(orderCode);
        return Task.FromResult(Orders.FirstOrDefault(o => o.OrderCode == orderCode));
    }

    public void AddCycle(UserSubscription cycle) => stagedCycles.Add(cycle);

    public void AddGroup(FamilyGroup group) => stagedGroups.Add(group);

    public Task SaveAsync(CancellationToken ct = default)
    {
        Flush();
        return Task.CompletedTask;
    }

    public void ClearTracking()
    {
        TrackingCleared++;
        stagedCycles.Clear();
        stagedGroups.Clear();
    }

    public bool IsConflict(Exception ex) => ex is UniqueViolationException;

    // ---- IUnitOfWork: lỗi thì quay về trạng thái đầu transaction, rồi áp lại các ghi của "request khác" (đã commit độc lập)

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        Assert.False(inTransaction, "Transaction lồng nhau");
        Transactions++;
        externalWrites.Clear();
        var snapshot = (Clone(Cycles), Clone(Groups), Clone(Members), Clone(Orders));
        inTransaction = true;
        try
        {
            await work(ct);
        }
        catch
        {
            (Cycles, Groups, Members, Orders) = snapshot;
            foreach (var write in externalWrites)
                write(this);
            stagedCycles.Clear();
            stagedGroups.Clear();
            throw;
        }
        finally
        {
            inTransaction = false;
        }
    }

    private void Flush()
    {
        Saves++;
        if (stagedCycles.Count > 0)
        {
            if (RaceBeforeCycleInsert is { } race)
            {
                RaceBeforeCycleInsert = null;
                race(this);
                if (inTransaction)
                    externalWrites.Add(race);   // request kia đã commit độc lập: còn nguyên khi transaction này quay lui
            }
            if (FailOnCycleInsert is { } failure)
            {
                FailOnCycleInsert = null;
                throw failure;
            }
            if (AlwaysConflictOnCycleInsert)
                throw new UniqueViolationException("ux_user_subscriptions_one_active_per_lane");
        }
        Groups.AddRange(stagedGroups);
        Cycles.AddRange(stagedCycles);
        stagedGroups.Clear();
        stagedCycles.Clear();
        CheckIndexes();
    }

    private void CheckIndexes()
    {
        foreach (var lane in Cycles.GroupBy(c => (c.UserId, c.Kind)))
        {
            if (lane.Count(c => c.Status == CycleStatuses.Active) > 1)
                throw new UniqueViolationException("ux_user_subscriptions_one_active_per_lane");
            if (lane.Count(c => c.Status == CycleStatuses.Queued) > 1)
                throw new UniqueViolationException("ux_user_subscriptions_one_queued_per_lane");
        }
        if (Groups.Where(g => g.Status == FamilyGroupStatuses.Active).GroupBy(g => g.OwnerUserId).Any(g => g.Count() > 1))
            throw new UniqueViolationException("ux_family_groups_one_active_per_owner");
        if (Members.Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId != null).GroupBy(m => m.UserId).Any(g => g.Count() > 1))
            throw new UniqueViolationException("ux_family_members_one_active_per_user");
    }

    private static List<T> Clone<T>(List<T> list) => list.Select(x => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x))!).ToList();
}

/// <summary>Cổng thanh toán giả của test: ghi lại các lần gọi, chọn được lỗi; webhook kiểm chữ ký thật bằng checksum key.</summary>
internal sealed class TestGateway(string checksumKey) : IPaymentGateway
{
    public List<(long Code, long Amount, string Description, DateTime ExpiresAt, string ReturnUrl, string CancelUrl)> Created { get; } = [];
    public List<(long Code, string Reason)> Cancelled { get; } = [];
    public bool FailCreate { get; set; }
    public bool FailCancel { get; set; }

    public Task<GatewayLink> CreateLinkAsync(long orderCode, long amountVnd, string description, DateTime expiresAtUtc, string returnUrl, string cancelUrl, CancellationToken ct)
    {
        if (FailCreate)
            throw new HttpRequestException("payOS không phản hồi");
        Created.Add((orderCode, amountVnd, description, expiresAtUtc, returnUrl, cancelUrl));
        return Task.FromResult(new GatewayLink($"link-{orderCode}", $"https://pay.test/{orderCode}", $"QR-{orderCode}"));
    }

    public Task<GatewayStatus> GetStatusAsync(long orderCode, CancellationToken ct) => Task.FromResult(new GatewayStatus("PENDING", 0));

    public Task CancelAsync(long orderCode, string reason, CancellationToken ct)
    {
        Cancelled.Add((orderCode, reason));
        return FailCancel ? throw new HttpRequestException("payOS không phản hồi") : Task.CompletedTask;
    }

    public Task<VerifiedWebhook?> VerifyWebhookAsync(string rawBody, CancellationToken ct) =>
        Task.FromResult(PayOsWebhook.Verify(rawBody, checksumKey));
}

internal sealed class RecordingNotifications : INotificationService
{
    public List<(Guid UserId, string Type, string Title, string? Body, string? Link)> Sent { get; } = [];
    public bool Throw { get; set; }

    public Task NotifyAsync(Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default)
    {
        if (Throw)
            throw new InvalidOperationException("thông báo lỗi");
        Sent.Add((userId, type, title, body, linkUrl));
        return Task.CompletedTask;
    }

    public Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>OrderService thật trên EntitlementService thật và cơ sở dữ liệu giả, để luật làn và đơn hàng được kiểm đúng như chạy thật.</summary>
internal sealed class OrderHarness
{
    public const string ChecksumKey = "ck-test";
    public static readonly DateTime Now = BillingStore.Now;

    public BillingStore Store { get; } = new();
    public FakeUserRepository Users { get; } = new();
    public TestGateway Gateway { get; } = new(ChecksumKey);
    public FakeEmailSender Email { get; } = new();
    public RecordingNotifications Notes { get; } = new();
    public BillingOptions Billing { get; } = new();
    public PayOsOptions PayOs { get; } = new() { ChecksumKey = ChecksumKey, ReturnUrlBase = "https://web.test/" };
    public EntitlementService Entitlements { get; }
    public OrderService Orders { get; }

    public OrderHarness()
    {
        Entitlements = new EntitlementService(Store);
        Orders = new OrderService(Store, Store, Entitlements, Users, Gateway, Store, Email, Notes, Billing, PayOs);
    }

    /// <summary>Người dùng với tuổi (null = chưa có ngày sinh) tính theo ngày Việt Nam của Now.</summary>
    public User AddUser(string role = UserRoles.Student, int? age = 17, string? email = null, string? name = null)
    {
        var user = new User
        {
            Role = role, Status = UserStatuses.Active, Email = email ?? $"u{Users.Users.Count}@e2e.test", FullName = name ?? $"Người dùng {Users.Users.Count}",
            DateOfBirth = age is { } a ? DateOnly.FromDateTime(Now.AddHours(7)).AddYears(-a) : null,
        };
        Users.Users.Add(user);
        Store.Names[user.Id] = user.FullName;
        return user;
    }

    public Task<OrderDto> CreateOrder(User user, string planCode, DateTime? now = null) =>
        Orders.CreateAsync(user.Id, user.Role, planCode, now ?? Now);

    public PaymentTransaction Order(long code) => Store.Orders.Single(o => o.OrderCode == code);

    /// <summary>Body webhook payOS đã ký như thật cho một đơn.</summary>
    public string WebhookBody(PaymentTransaction order, long? amount = null, string code = "00", string signingKey = ChecksumKey, string? outerCode = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["orderCode"] = order.OrderCode, ["amount"] = amount ?? (long)order.AmountVnd, ["description"] = order.Description, ["accountNumber"] = "12345678",
            ["reference"] = "TF0001", ["transactionDateTime"] = "2026-11-20 10:00:00", ["currency"] = "VND", ["paymentLinkId"] = $"link-{order.OrderCode}",
            ["code"] = code, ["desc"] = "success", ["counterAccountName"] = "NGUYEN VAN A", ["counterAccountNumber"] = "999", ["virtualAccountNumber"] = "VA1",
        };
        var dataJson = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(dataJson);
        return $"{{\"code\":\"{outerCode ?? code}\",\"desc\":\"success\",\"success\":true,\"data\":{dataJson},\"signature\":\"{PayOsSignature.Sign(doc.RootElement, signingKey)}\"}}";
    }
}
