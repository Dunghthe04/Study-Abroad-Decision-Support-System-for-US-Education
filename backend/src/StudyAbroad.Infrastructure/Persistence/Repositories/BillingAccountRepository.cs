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

    public Task<string?> GetUserEmailAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).Select(u => (string?)u.Email).FirstOrDefaultAsync(ct);

    public async Task<int> BlankMembershipEmailsAsync(Guid userId, string? email, DateTime nowUtc, CancellationToken ct = default)
    {
        var hasEmail = !string.IsNullOrEmpty(email);
        var ended = 0;
        if (hasEmail)
            // Lời mời còn pending gửi tới email này: kết thúc rồi xóa email trong cùng câu lệnh để dòng rời index pending ngay (email lưu chữ thường)
            ended = await db.FamilyMembers.Where(m => m.Status == FamilyMemberStatuses.Pending && m.InvitedEmail == email)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, FamilyMemberStatuses.Cancelled)
                    .SetProperty(m => m.EndedAt, nowUtc)
                    .SetProperty(m => m.TokenHash, (string?)null)
                    .SetProperty(m => m.InvitedEmail, string.Empty)
                    .SetProperty(m => m.UpdatedAt, nowUtc), ct);

        // Không đụng dòng pending còn lại (index một-lời-mời-pending-mỗi-email dùng cột này; lời mời pending gửi cho email khác chưa gắn user_id)
        var rows = db.FamilyMembers.Where(m => m.Status != FamilyMemberStatuses.Pending);
        rows = hasEmail ? rows.Where(m => m.UserId == userId || m.InvitedEmail == email) : rows.Where(m => m.UserId == userId);
        return ended + await rows.ExecuteUpdateAsync(s => s
            .SetProperty(m => m.InvitedEmail, string.Empty)
            .SetProperty(m => m.UpdatedAt, nowUtc), ct);
    }

    public Task<int> EndOpenCyclesAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) =>
        db.UserSubscriptions
            .Where(c => c.UserId == userId && c.Status == CycleStatuses.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, CycleStatuses.Ended)
                .SetProperty(c => c.EndedReason, CycleEndReasons.OwnerPurged)
                .SetProperty(c => c.EndedAt, nowUtc)
                .SetProperty(c => c.UpdatedAt, nowUtc), ct);
}
