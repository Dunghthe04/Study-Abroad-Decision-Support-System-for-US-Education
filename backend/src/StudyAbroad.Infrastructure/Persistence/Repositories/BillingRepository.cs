using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class BillingRepository(AppDbContext db) : IBillingRepository
{
    public async Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default) =>
        await db.SubscriptionPlans.AsNoTracking().ToListAsync(ct);

    public Task<List<UserSubscription>> GetOpenCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default) =>
        db.UserSubscriptions
            .Where(c => c.UserId == ownerUserId && c.Kind == kind && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued))
            .ToListAsync(ct);

    public Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default) =>
        db.FamilyGroups.FirstOrDefaultAsync(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active, ct);

    public Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default) =>
        db.FamilyGroups
            .Where(g => g.Status == FamilyGroupStatuses.Active
                && db.FamilyMembers.Any(m => m.FamilyGroupId == g.Id && m.UserId == userId && m.Status == FamilyMemberStatuses.Active))
            .FirstOrDefaultAsync(ct);

    public Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default) =>
        db.FamilyMembers
            .Where(m => m.FamilyGroupId == familyGroupId && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active))
            .ToListAsync(ct);

    public async Task<long> SumCycleTokensAsync(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default) =>
        await db.AiCalls.Where(MeteringFilters.Cycle(cycleId, fromUtc, toUtc)).SumAsync(a => (long?)a.TotalTokens, ct) ?? 0;

    public async Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default) =>
        await db.AiCalls.Where(MeteringFilters.Free(userId, fromUtc)).SumAsync(a => (long?)a.TotalTokens, ct) ?? 0;

    public Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

    public Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default) =>
        db.PaymentTransactions
            .Where(o => o.UserId == userId && o.Status == OrderStatuses.Pending)
            .Select(o => (long?)o.OrderCode)
            .FirstOrDefaultAsync(ct);

    public async Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default)
    {
        if (steps.Count == 0)
            return;
        await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        foreach (var step in steps)
        {
            step();
            await db.SaveChangesAsync(ct);
        }
        if (own is not null)
            await own.CommitAsync(ct);
    }

    public async Task<bool> SaveLockedStepsAsync(
        Guid ownerUserId, string kind, bool lockGroup, Func<Task<IReadOnlyList<Action>>> plan, CancellationToken ct = default)
    {
        await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Bản đang theo dõi có thể cũ: bỏ đi để sau khi khóa, truy vấn trong plan đọc dòng mới nhất (cách OrderRepository.LockByCodeAsync)
            DetachLane(ownerUserId, kind, lockGroup);
            // Thứ tự khóa: kỳ của làn trước, nhóm sau (cùng thứ tự với mọi nơi khác); ORDER BY id để hai bên khóa cùng tập dòng theo cùng thứ tự
            await db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM app.user_subscriptions
                WHERE user_id = {ownerUserId} AND kind = {kind} AND status IN ({CycleStatuses.Active}, {CycleStatuses.Queued})
                ORDER BY id
                FOR UPDATE
                """).ToListAsync(ct);
            if (lockGroup)
                await db.Database.SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM app.family_groups
                    WHERE owner_user_id = {ownerUserId} AND status = {FamilyGroupStatuses.Active}
                    ORDER BY id
                    FOR UPDATE
                    """).ToListAsync(ct);

            var steps = await plan();
            foreach (var step in steps)
            {
                step();
                await db.SaveChangesAsync(ct);
            }
            if (own is not null)
                await own.CommitAsync(ct);
            return steps.Count > 0;
        }
        catch (DbUpdateConcurrencyException) when (own is not null)
        {
            // Dòng thành viên (xmin) vừa bị request khác đổi: transaction quay lui, request kia là bên đã làm
            DetachLane(ownerUserId, kind, lockGroup);
            return false;
        }
        catch
        {
            DetachLane(ownerUserId, kind, lockGroup);   // phần sửa dang dở không được lưu lại ở lần SaveChanges sau của request
            throw;
        }
    }

    private void DetachLane(Guid ownerUserId, string kind, bool includeGroup)
    {
        var groupIds = includeGroup ? db.FamilyGroups.Local.Where(g => g.OwnerUserId == ownerUserId).Select(g => g.Id).ToHashSet() : [];
        var stale = db.ChangeTracker.Entries().Where(e => e.State != EntityState.Added && e.Entity switch
        {
            UserSubscription c => c.UserId == ownerUserId && c.Kind == kind,
            FamilyGroup g => includeGroup && g.OwnerUserId == ownerUserId,
            FamilyMember m => includeGroup && groupIds.Contains(m.FamilyGroupId),
            _ => false,
        }).ToList();
        foreach (var entry in stale)
            entry.State = EntityState.Detached;
    }
}
