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

    public async Task<long> SumCycleTokensAsync(Guid cycleId, CancellationToken ct = default) =>
        await db.AiCalls
            .Where(a => a.UserSubscriptionId == cycleId && a.Kind == BillingRules.MeteredAiKind)
            .SumAsync(a => (long?)a.TotalTokens, ct) ?? 0;

    // plan_code chỉ được đóng dấu trên lượt chat, nên plan_code có mà không có kỳ = lượt chat dùng hạn mức Free.
    // Không lọc theo chat_message_id: tắt lưu tin (Chat:StoreMessages) hay lưu tin lỗi thì cột đó trống
    public async Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default) =>
        await db.AiCalls
            .Where(a => a.UserId == userId && a.UserSubscriptionId == null && a.PlanCode != null
                && a.Kind == BillingRules.MeteredAiKind && a.CreatedAt >= fromUtc)
            .SumAsync(a => (long?)a.TotalTokens, ct) ?? 0;

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
}
