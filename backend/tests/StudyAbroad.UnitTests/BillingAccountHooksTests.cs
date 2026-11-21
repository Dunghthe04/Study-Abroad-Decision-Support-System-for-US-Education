using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// [UC-80] Xóa tài khoản và gói dịch vụ: các case D1 đến D6 của kế hoạch billing v2. Hook thật chạy trên một kho trong bộ nhớ;
// "sau commit" kiểm bằng cách chỉ chạy hàm hook trả về khi test muốn.
public class BillingAccountHooksTests
{
    private static readonly DateTime Now = BillingStore.Now;

    /// <summary>Bản trong bộ nhớ của BillingAccountRepository: cùng luật, cùng giá trị lý do kết thúc.</summary>
    private sealed class StoreAccountRepository(BillingStore store) : IBillingAccountRepository
    {
        public Task<EndedMembership?> EndActiveMembershipAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var row = store.Members.FirstOrDefault(m => m.UserId == userId && m.Status == FamilyMemberStatuses.Active);
            if (row is null)
                return Task.FromResult<EndedMembership?>(null);
            row.Status = FamilyMemberStatuses.Ended;
            row.EndedReason = FamilyMemberEndReasons.MemberDeleted;
            row.EndedAt = nowUtc;
            row.TokenHash = null;
            return Task.FromResult<EndedMembership?>(new EndedMembership(store.Groups.Single(g => g.Id == row.FamilyGroupId).OwnerUserId, store.Names[userId]));
        }

        public Task<IReadOnlyList<long>> CancelPendingOrdersAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var pending = store.Orders.Where(o => o.UserId == userId && o.Status == OrderStatuses.Pending).ToList();
            foreach (var o in pending)
                o.Status = OrderStatuses.Cancelled;
            return Task.FromResult<IReadOnlyList<long>>(pending.Select(o => o.OrderCode).ToList());
        }

        public Task<EndedOwnedGroups?> EndOwnedGroupsAsync(Guid ownerUserId, DateTime nowUtc, CancellationToken ct = default)
        {
            var groups = store.Groups.Where(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active).ToList();
            if (groups.Count == 0)
                return Task.FromResult<EndedOwnedGroups?>(null);
            var ids = groups.Select(g => g.Id).ToHashSet();
            var open = store.Members.Where(m => ids.Contains(m.FamilyGroupId)
                && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)).ToList();
            var memberIds = open.Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId is not null).Select(m => m.UserId!.Value).ToList();
            foreach (var m in open)
            {
                m.Status = FamilyMemberStatuses.Ended;
                m.EndedReason = FamilyMemberEndReasons.OwnerPurged;
                m.EndedAt = nowUtc;
                m.TokenHash = null;
            }
            foreach (var g in groups)
            {
                g.Status = FamilyGroupStatuses.Ended;
                g.EndedReason = CycleEndReasons.OwnerPurged;
                g.EndedAt = nowUtc;
            }
            return Task.FromResult<EndedOwnedGroups?>(new EndedOwnedGroups(store.Names[ownerUserId], memberIds));
        }

        public Task<int> BlankMembershipEmailsAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var rows = store.Members.Where(m => m.UserId == userId && m.Status != FamilyMemberStatuses.Pending).ToList();
            foreach (var m in rows)
                m.InvitedEmail = string.Empty;
            return Task.FromResult(rows.Count);
        }

        public Task<int> EndOpenCyclesAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var open = store.Cycles.Where(c => c.UserId == userId && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued)).ToList();
            foreach (var c in open)
            {
                c.Status = CycleStatuses.Ended;
                c.EndedReason = CycleEndReasons.OwnerPurged;
                c.EndedAt = nowUtc;
            }
            return Task.FromResult(open.Count);
        }
    }

    private sealed class Env
    {
        public BillingStore Store { get; } = new();
        public RecordingNotifications Notes { get; } = new();
        public TestGateway Gateway { get; } = new("ck");
        public BillingAccountHooks Hooks { get; }
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Member { get; } = Guid.NewGuid();

        public Env()
        {
            Store.Names[Owner] = "Chủ Gói";
            Store.Names[Member] = "Thành Viên";
            Hooks = new BillingAccountHooks(new StoreAccountRepository(Store), Notes, Gateway);
        }

        /// <summary>Nhóm của Owner với kỳ family-1m đang chạy và Member là thành viên active.</summary>
        public UserSubscription Family() => Store.AddFamily(Owner, Member);

        public FamilyGroup Group => Store.Groups.Single();

        public PaymentTransaction AddOrder(Guid userId, string status, long code)
        {
            var order = new PaymentTransaction { UserId = userId, Status = status, OrderCode = code, PlanKind = PlanKinds.Personal };
            Store.Orders.Add(order);
            return order;
        }

        public Task<SubscriptionSummaryDto> Summary(Guid userId) => new EntitlementService(Store).GetSummaryAsync(userId, Now);
    }

    // ---- xóa mềm

    [Fact]
    public async Task D1_MemberSoftDeletes_MembershipEndedSeatFreedOwnerNotifiedAfterCommit()
    {
        var env = new Env();
        env.Family();

        var afterCommit = await env.Hooks.OnAccountSoftDeletedAsync(env.Member, Now);

        var row = env.Store.Members.Single();
        Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.MemberDeleted, Now), (row.Status, row.EndedReason, row.EndedAt));
        Assert.Null(row.TokenHash);
        Assert.Empty(await env.Store.GetOpenMembersAsync(env.Group.Id));   // chỗ ngồi đã trống
        Assert.Empty(env.Notes.Sent);   // thông báo chỉ gửi sau commit

        await afterCommit(CancellationToken.None);

        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Owner, NotificationTypes.FamilyMemberLeft), (note.UserId, note.Type));
        Assert.Contains("Thành Viên", note.Body);
        Assert.NotNull(await env.Store.FindActiveOwnedGroupAsync(env.Owner));   // nhóm của chủ vẫn chạy
        Assert.Equal(AccountTiers.Free, (await env.Summary(env.Member)).Tier);
        Assert.Equal(AccountTiers.Premium, (await env.Summary(env.Owner)).Tier);
    }

    [Fact]
    public async Task D1_UserWithoutFamilyOrOrders_DoesNothingAndNotifiesNobody()
    {
        var env = new Env();
        env.Family();

        var afterCommit = await env.Hooks.OnAccountSoftDeletedAsync(Guid.NewGuid(), Now);
        await afterCommit(CancellationToken.None);

        Assert.Equal(FamilyMemberStatuses.Active, env.Store.Members.Single().Status);
        Assert.Empty(env.Notes.Sent);
        Assert.Empty(env.Gateway.Cancelled);
    }

    [Fact]
    public async Task D2_MemberRestores_StaysFreeBecauseNothingBringsTheMembershipBack()
    {
        var env = new Env();
        env.Family();
        await (await env.Hooks.OnAccountSoftDeletedAsync(env.Member, Now))(CancellationToken.None);

        // Khôi phục chỉ đổi trạng thái tài khoản, không gọi hook nào: người này lại là Free và nhóm có chỗ trống
        Assert.Equal(AccountTiers.Free, (await env.Summary(env.Member)).Tier);
        Assert.Null(await env.Store.FindActiveMemberGroupAsync(env.Member));
        Assert.Equal(FamilyMemberStatuses.Ended, env.Store.Members.Single().Status);
    }

    [Fact]
    public async Task D3_OwnerSoftDeletes_GroupCyclesAndMembersKeepRunning_NoOneNotified()
    {
        var env = new Env();
        var cycle = env.Family();
        var endsAt = cycle.EndsAt;

        var afterCommit = await env.Hooks.OnAccountSoftDeletedAsync(env.Owner, Now);
        await afterCommit(CancellationToken.None);

        Assert.Equal(FamilyGroupStatuses.Active, env.Group.Status);
        Assert.Null(env.Group.EndedReason);
        Assert.Equal((CycleStatuses.Active, endsAt, null), (cycle.Status, cycle.EndsAt, cycle.EndedReason));
        var member = env.Store.Members.Single();
        Assert.Equal((FamilyMemberStatuses.Active, "h"), (member.Status, member.TokenHash));
        Assert.Empty(env.Notes.Sent);
        // Thành viên vẫn dùng gói tới hết hạn (O1); D5: chủ khôi phục thì không có gì để hoàn tác
        Assert.Equal(AccountTiers.Premium, (await env.Summary(env.Member)).Tier);
    }

    [Fact]
    public async Task D3_PendingOrders_CancelledInDbAndAtGatewayOnlyAfterCommit()
    {
        var env = new Env();
        var pending = env.AddOrder(env.Member, OrderStatuses.Pending, 1001);
        var paid = env.AddOrder(env.Member, OrderStatuses.Paid, 1002);
        var mismatch = env.AddOrder(env.Member, OrderStatuses.Mismatch, 1003);
        var others = env.AddOrder(env.Owner, OrderStatuses.Pending, 1004);

        var afterCommit = await env.Hooks.OnAccountSoftDeletedAsync(env.Member, Now);

        Assert.Equal(OrderStatuses.Cancelled, pending.Status);
        Assert.Equal((OrderStatuses.Paid, OrderStatuses.Mismatch, OrderStatuses.Pending), (paid.Status, mismatch.Status, others.Status));
        Assert.Empty(env.Gateway.Cancelled);   // chưa commit thì chưa đụng tới cổng thanh toán

        await afterCommit(CancellationToken.None);

        Assert.Equal(1001, Assert.Single(env.Gateway.Cancelled).Code);
    }

    [Fact]
    public async Task D3_GatewayCancelFails_StillOkAndOrderStaysCancelled()
    {
        var env = new Env();
        var pending = env.AddOrder(env.Member, OrderStatuses.Pending, 1001);
        env.Gateway.FailCancel = true;

        var afterCommit = await env.Hooks.OnAccountSoftDeletedAsync(env.Member, Now);
        await afterCommit(CancellationToken.None);   // không ném: tài khoản đã xóa mềm xong

        Assert.Equal(OrderStatuses.Cancelled, pending.Status);
        Assert.Single(env.Gateway.Cancelled);
    }

    // ---- xóa hẳn

    [Fact]
    public async Task D4_OwnerPurged_GroupMembersAndCyclesEnded_MembersNotified()
    {
        var env = new Env();
        var cycle = env.Family();
        var queued = env.Store.AddQueued(env.Owner, "family-3m");
        var pendingInvite = new FamilyMember
        {
            FamilyGroupId = env.Group.Id, InvitedEmail = "p@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "p-hash",
        };
        var lapsedBefore = new FamilyMember
        {
            FamilyGroupId = env.Group.Id, UserId = Guid.NewGuid(), InvitedEmail = "old@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Removed,
        };
        env.Store.Members.AddRange([pendingInvite, lapsedBefore]);
        var order = env.AddOrder(env.Owner, OrderStatuses.Paid, 2001);

        var afterCommit = await env.Hooks.OnBeforePurgeAsync(env.Owner, Now);

        Assert.Equal((FamilyGroupStatuses.Ended, CycleEndReasons.OwnerPurged, Now), (env.Group.Status, env.Group.EndedReason, env.Group.EndedAt));
        var active = env.Store.Members[0];
        Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.OwnerPurged, null), (active.Status, active.EndedReason, active.TokenHash));
        Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberEndReasons.OwnerPurged, null), (pendingInvite.Status, pendingInvite.EndedReason, pendingInvite.TokenHash));
        Assert.Equal(FamilyMemberStatuses.Removed, lapsedBefore.Status);   // dòng đã kết thúc từ trước giữ nguyên lý do cũ
        foreach (var c in new[] { cycle, queued })
            Assert.Equal((CycleStatuses.Ended, CycleEndReasons.OwnerPurged, Now), (c.Status, c.EndedReason, c.EndedAt));
        Assert.Equal((OrderStatuses.Paid, env.Owner), (order.Status, order.UserId));   // thanh toán không bị đụng, DB tự bỏ liên kết khi xóa user
        Assert.Empty(env.Notes.Sent);

        await afterCommit(CancellationToken.None);

        var note = Assert.Single(env.Notes.Sent);   // chỉ thành viên đang active; lời mời chưa nhận chưa có tài khoản để báo
        Assert.Equal((env.Member, NotificationTypes.FamilyEnded), (note.UserId, note.Type));
        Assert.Equal("Gói Gia đình của Chủ Gói đã kết thúc.", note.Body);
    }

    [Fact]
    public async Task F8_MemberPurged_TheirMembershipHistoryLosesTheEmail_OthersAreUntouched()
    {
        var env = new Env();
        env.Family();
        var mine = env.Store.Members.Single();   // dòng thành viên của Member (InvitedEmail = m@x.vn)
        await env.Hooks.OnAccountSoftDeletedAsync(env.Member, Now);   // xóa mềm trước: tư cách đã kết thúc nhưng còn giữ email
        Assert.Equal(("m@x.vn", FamilyMemberStatuses.Ended), (mine.InvitedEmail, mine.Status));
        var otherInvite = new FamilyMember
        {
            FamilyGroupId = env.Group.Id, InvitedEmail = "someone@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, TokenHash = "p-hash",
        };
        var otherMember = new FamilyMember
        {
            FamilyGroupId = env.Group.Id, UserId = Guid.NewGuid(), InvitedEmail = "other@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Ended,
        };
        env.Store.Members.AddRange([otherInvite, otherMember]);

        await env.Hooks.OnBeforePurgeAsync(env.Member, Now);

        Assert.Equal(string.Empty, mine.InvitedEmail);
        Assert.Equal(("someone@x.vn", "other@x.vn"), (otherInvite.InvitedEmail, otherMember.InvitedEmail));   // lời mời gửi cho email khác và người khác giữ nguyên
        Assert.Equal(FamilyMemberStatuses.Pending, otherInvite.Status);
    }

    [Fact]
    public async Task D4_OwnerPurged_MembersLoseThePremiumTier()
    {
        var env = new Env();
        env.Family();

        await env.Hooks.OnBeforePurgeAsync(env.Owner, Now);

        Assert.Equal(AccountTiers.Free, (await env.Summary(env.Member)).Tier);
    }

    [Fact]
    public async Task D6_PersonalBuyerPurged_OpenCyclesEndedNothingNotified()
    {
        var env = new Env();
        var active = env.Store.AddActive(env.Owner, "personal-1m");
        var queued = env.Store.AddQueued(env.Owner, "personal-1m");
        var ended = env.Store.AddActive(env.Owner, "personal-3m");
        ended.Status = CycleStatuses.Ended;
        ended.EndedReason = CycleEndReasons.Expired;
        var order = env.AddOrder(env.Owner, OrderStatuses.Paid, 3001);

        var afterCommit = await env.Hooks.OnBeforePurgeAsync(env.Owner, Now);
        await afterCommit(CancellationToken.None);

        Assert.All(new[] { active, queued }, c => Assert.Equal((CycleStatuses.Ended, CycleEndReasons.OwnerPurged), (c.Status, c.EndedReason)));
        Assert.Equal(CycleEndReasons.Expired, ended.EndedReason);
        Assert.Equal(OrderStatuses.Paid, order.Status);
        Assert.Empty(env.Notes.Sent);
    }

    [Fact]
    public async Task D4_OwnerPurgedWithAnEmptyGroup_NotifiesNobody()
    {
        var env = new Env();
        env.Store.Groups.Add(new FamilyGroup { OwnerUserId = env.Owner, Status = FamilyGroupStatuses.Active });
        env.Store.AddActive(env.Owner, "family-1m");

        await (await env.Hooks.OnBeforePurgeAsync(env.Owner, Now))(CancellationToken.None);

        Assert.Equal(FamilyGroupStatuses.Ended, env.Store.Groups.Single().Status);
        Assert.Empty(env.Notes.Sent);
    }

    // ---- khóa ngoại: không dòng nào của gói chặn được DeleteUserAsync

    [Fact]
    public void D4_ForeignKeysFromBillingTables_AreCascadeOrSetNull_SoNothingBlocksDeletingTheUser()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=usas_schema_check").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);
        Type[] principals = [typeof(User), typeof(FamilyGroup), typeof(UserSubscription)];
        Type[] dependents = [typeof(FamilyGroup), typeof(FamilyMember), typeof(UserSubscription), typeof(PaymentTransaction), typeof(AiCall)];

        var foreignKeys = dependents
            .SelectMany(t => db.Model.FindEntityType(t)!.GetForeignKeys())
            .Where(fk => principals.Contains(fk.PrincipalEntityType.ClrType))
            .ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, fk => Assert.True(fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull,
            $"{fk.DeclaringEntityType.ClrType.Name} -> {fk.PrincipalEntityType.ClrType.Name}: {fk.DeleteBehavior}"));
    }
}
