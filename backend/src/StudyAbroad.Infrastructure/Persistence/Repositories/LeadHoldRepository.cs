using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-110 / UC-150] Giữ, gửi lại, thu hồi yêu cầu liên hệ của học sinh vừa về dưới 16 bằng câu UPDATE có điều kiện.</summary>
public class LeadHoldRepository(AppDbContext db) : ILeadHoldRepository
{
    public Task<int> HoldOpenLeadsAsync(Guid userId, DateTime now, DateTime expiresAt, CancellationToken ct = default) =>
        db.Leads
            .Where(l => l.UserId == userId
                && (l.Status == LeadStatuses.New || l.Status == LeadStatuses.Accepted || l.Status == LeadStatuses.Contacted))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.HeldFromStatus, l => l.Status)
                .SetProperty(l => l.Status, LeadStatuses.PendingParent)
                .SetProperty(l => l.HeldAt, now)
                .SetProperty(l => l.ParentTokenExpiresAt, expiresAt)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.ParentEmail, (string?)null)
                .SetProperty(l => l.ParentDecidedAt, (DateTime?)null)
                .SetProperty(l => l.ParentDecisionIp, (string?)null)
                .SetProperty(l => l.UpdatedAt, now), ct);

    public async Task<IReadOnlyList<Lead>> ListHeldAsync(Guid userId, CancellationToken ct = default) =>
        await db.Leads.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == LeadStatuses.PendingParent && l.HeldFromStatus != null)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> SetParentTokenHashAsync(Guid leadId, string tokenHash, DateTime now, CancellationToken ct = default) =>
        await db.Leads
            .Where(l => l.Id == leadId && l.Status == LeadStatuses.PendingParent && l.HeldFromStatus != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ParentTokenHash, tokenHash)
                .SetProperty(l => l.UpdatedAt, now), ct) > 0;

    public Task<int> WithdrawHeldAsync(Guid userId, DateTime now, CancellationToken ct = default) =>
        db.Leads
            .Where(l => l.UserId == userId && l.Status == LeadStatuses.PendingParent && l.HeldFromStatus != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.UpdatedAt, now), ct);

    public async Task<IReadOnlyList<Lead>> ListPendingParentAsync(Guid userId, CancellationToken ct = default) =>
        await db.Leads.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == LeadStatuses.PendingParent)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> ReleaseAsync(Guid leadId, string status, long pausedSeconds, DateTime now, CancellationToken ct = default) =>
        await db.Leads
            .Where(l => l.Id == leadId && l.Status == LeadStatuses.PendingParent)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, status)
                .SetProperty(l => l.PausedSeconds, pausedSeconds)
                .SetProperty(l => l.HeldFromStatus, (string?)null)
                .SetProperty(l => l.HeldAt, (DateTime?)null)
                // Link trong mail phụ huynh hết hiệu lực; không còn hạn phụ huynh nên lead không bị coi là "thu hồi trước khi trung tâm nhận"
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.ParentTokenExpiresAt, (DateTime?)null)
                .SetProperty(l => l.UpdatedAt, now), ct) > 0;

    public async Task<IReadOnlyList<Guid>> ListStudentsToReleaseAsync(DateOnly bornOnOrBefore, CancellationToken ct = default) =>
        await db.Leads.AsNoTracking()
            .Where(l => l.Status == LeadStatuses.PendingParent && l.UserId != null)
            .Join(db.Users.AsNoTracking(), l => l.UserId, u => (Guid?)u.Id, (l, u) => u)
            .Where(u => u.Role == UserRoles.Student && u.DeletedAt == null && u.DateOfBirth != null && u.DateOfBirth <= bornOnOrBefore)
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Lead>> ListHeldPastDeadlineAsync(DateTime now, CancellationToken ct = default) =>
        await db.Leads.AsNoTracking()
            .Where(l => l.Status == LeadStatuses.PendingParent && l.HeldFromStatus != null && l.ParentTokenExpiresAt <= now)
            .OrderBy(l => l.ParentTokenExpiresAt)
            .ToListAsync(ct);

    public async Task<bool> WithdrawHeldLeadAsync(Guid leadId, DateTime now, CancellationToken ct = default) =>
        await db.Leads
            .Where(l => l.Id == leadId && l.Status == LeadStatuses.PendingParent && l.HeldFromStatus != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.UpdatedAt, now), ct) > 0;
}
