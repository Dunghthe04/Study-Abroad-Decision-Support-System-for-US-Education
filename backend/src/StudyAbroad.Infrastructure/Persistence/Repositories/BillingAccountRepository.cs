using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [UC-80] Các bước gói dịch vụ khi xóa tài khoản, chạy chung transaction do AuthService / AccountPurgeService mở.
/// Mọi khóa ngoại tới users đều Cascade hoặc SetNull (family_groups.owner_user_id Cascade, family_members.user_id SetNull,
/// user_subscriptions.user_id Cascade, payment_transactions.user_id SetNull), nên không dòng nào chặn được việc xóa user.
/// </summary>
public class BillingAccountRepository(AppDbContext db) : IBillingAccountRepository
{
    public async Task<EndedMembership?> EndActiveMembershipAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var row = await (from m in db.FamilyMembers
                         join g in db.FamilyGroups on m.FamilyGroupId equals g.Id
                         join u in db.Users on m.UserId equals u.Id
                         where m.UserId == userId && m.Status == FamilyMemberStatuses.Active
                         select new { m.Id, g.OwnerUserId, u.FullName }).FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        // Điều kiện status = active ngay trong câu lệnh: chủ nhóm gỡ người này cùng lúc thì một bên thắng, bên kia 0 dòng
        var ended = await db.FamilyMembers.Where(m => m.Id == row.Id && m.Status == FamilyMemberStatuses.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, FamilyMemberStatuses.Ended)
                .SetProperty(m => m.EndedReason, FamilyMemberEndReasons.MemberDeleted)
                .SetProperty(m => m.EndedAt, nowUtc)
                .SetProperty(m => m.TokenHash, (string?)null)
                .SetProperty(m => m.UpdatedAt, nowUtc), ct);
        return ended == 0 ? null : new EndedMembership(row.OwnerUserId, row.FullName);
    }

    public async Task<IReadOnlyList<long>> CancelPendingOrdersAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Khóa dòng đơn như OrderService.CancelAsync / ApplyPaymentAsync: webhook trả tiền chạy cùng lúc phải chờ commit
        var codes = await db.Database.SqlQuery<long>($"""
            SELECT order_code AS "Value" FROM app.payment_transactions
            WHERE user_id = {userId} AND status = {OrderStatuses.Pending}
            FOR UPDATE
            """).ToListAsync(ct);
        if (codes.Count == 0)
            return codes;

        await db.PaymentTransactions.Where(o => codes.Contains(o.OrderCode) && o.Status == OrderStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatuses.Cancelled)
                .SetProperty(o => o.UpdatedAt, nowUtc), ct);
        return codes;
    }

    public async Task<EndedOwnedGroups?> EndOwnedGroupsAsync(Guid ownerUserId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Cùng khóa dòng nhóm như FamilyService: mời, nhận lời mời, gỡ chạy cùng lúc phải chờ
        var groupIds = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM app.family_groups
            WHERE owner_user_id = {ownerUserId} AND status = {FamilyGroupStatuses.Active}
            FOR UPDATE
            """).ToListAsync(ct);
        if (groupIds.Count == 0)
            return null;

        var memberUserIds = await db.FamilyMembers
            .Where(m => groupIds.Contains(m.FamilyGroupId) && m.Status == FamilyMemberStatuses.Active && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .ToListAsync(ct);
        var ownerName = await db.Users.Where(u => u.Id == ownerUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;

        await db.FamilyMembers
            .Where(m => groupIds.Contains(m.FamilyGroupId) && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, FamilyMemberStatuses.Ended)
                .SetProperty(m => m.EndedReason, FamilyMemberEndReasons.OwnerPurged)
                .SetProperty(m => m.EndedAt, nowUtc)
                .SetProperty(m => m.TokenHash, (string?)null)
                .SetProperty(m => m.UpdatedAt, nowUtc), ct);
        await db.FamilyGroups.Where(g => groupIds.Contains(g.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Status, FamilyGroupStatuses.Ended)
                .SetProperty(g => g.EndedReason, CycleEndReasons.OwnerPurged)
                .SetProperty(g => g.EndedAt, nowUtc)
                .SetProperty(g => g.UpdatedAt, nowUtc), ct);
        return new EndedOwnedGroups(ownerName, memberUserIds);
    }

    public Task<int> EndOpenCyclesAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) =>
        db.UserSubscriptions
            .Where(c => c.UserId == userId && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, CycleStatuses.Ended)
                .SetProperty(c => c.EndedReason, CycleEndReasons.OwnerPurged)
                .SetProperty(c => c.EndedAt, nowUtc)
                .SetProperty(c => c.UpdatedAt, nowUtc), ct);
}
