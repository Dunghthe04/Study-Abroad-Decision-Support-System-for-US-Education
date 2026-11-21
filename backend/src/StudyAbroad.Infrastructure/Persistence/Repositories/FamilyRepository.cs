using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class FamilyRepository(AppDbContext db) : IFamilyRepository
{
    private const string OneActivePerUserIndex = "ux_family_members_one_active_per_user";
    private const string OnePendingPerEmailIndex = "ux_family_members_one_pending_per_email";

    public async Task<bool> LockActiveGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        // Mẫu khóa dòng như AccountPurgeRepository.LockIfStillDueAsync; 0 dòng = nhóm đã mất hoặc không còn active
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM app.family_groups
            WHERE id = {groupId} AND status = {FamilyGroupStatuses.Active}
            FOR UPDATE
            """).ToListAsync(ct);
        return ids.Count > 0;
    }

    public Task<FamilyMember?> FindMemberAsync(Guid memberId, CancellationToken ct = default) =>
        db.FamilyMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct);

    public Task<FamilyMember?> FindMemberByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.FamilyMembers.FirstOrDefaultAsync(m => m.TokenHash == tokenHash, ct);

    public Task<FamilyMember?> FindActiveMembershipAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
        db.FamilyMembers.FirstOrDefaultAsync(
            m => m.FamilyGroupId == groupId && m.UserId == userId && m.Status == FamilyMemberStatuses.Active, ct);

    public Task<FamilyGroup?> FindGroupAsync(Guid groupId, CancellationToken ct = default) =>
        db.FamilyGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<int> CountSeatsAsync(Guid groupId, string seatRole, Guid? excludeMemberId, DateTime nowUtc, CancellationToken ct = default)
    {
        var exclude = excludeMemberId ?? Guid.Empty;
        return db.FamilyMembers.CountAsync(m =>
            m.FamilyGroupId == groupId && m.SeatRole == seatRole && m.Id != exclude
            && (m.Status == FamilyMemberStatuses.Active || (m.Status == FamilyMemberStatuses.Pending && m.ExpiresAt > nowUtc)), ct);
    }

    public Task<bool> HasOpenInviteForEmailAsync(Guid groupId, string email, CancellationToken ct = default) =>
        db.FamilyMembers.AnyAsync(m =>
            m.FamilyGroupId == groupId && m.InvitedEmail == email
            && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active), ct);

    public Task<bool> HasPendingFamilyOrderAsync(Guid userId, CancellationToken ct = default) =>
        db.PaymentTransactions.AnyAsync(
            o => o.UserId == userId && o.Status == OrderStatuses.Pending && o.PlanKind == PlanKinds.Family, ct);

    public async Task<Dictionary<Guid, long>> SumTokensByUserAsync(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var rows = await db.AiCalls
            .Where(MeteringFilters.Cycle(cycleId, fromUtc, toUtc))
            .Where(a => a.UserId != null)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Tokens = g.Sum(a => (long?)a.TotalTokens) ?? 0 })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.UserId, r => r.Tokens);
    }

    public Task AddMemberAsync(FamilyMember member, CancellationToken ct = default)
    {
        db.FamilyMembers.Add(member);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Dòng thành viên đã bị thao tác khác sửa trước.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                                           && pg.ConstraintName is OneActivePerUserIndex or OnePendingPerEmailIndex)
        {
            // Hai request cùng thắng bước kiểm tra: index duy nhất là chốt chặn cuối
            throw pg.ConstraintName == OneActivePerUserIndex ? FamilyErrors.AlreadyInFamily() : FamilyErrors.InviteExists();
        }
    }

    public Task<int> ExpireDueInvitesAsync(DateTime nowUtc, Guid? groupId = null, CancellationToken ct = default) =>
        db.FamilyMembers
            .Where(m => m.Status == FamilyMemberStatuses.Pending && m.ExpiresAt <= nowUtc && (groupId == null || m.FamilyGroupId == groupId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, FamilyMemberStatuses.Expired)
                .SetProperty(m => m.TokenHash, (string?)null)
                .SetProperty(m => m.EndedAt, nowUtc), ct);
}
