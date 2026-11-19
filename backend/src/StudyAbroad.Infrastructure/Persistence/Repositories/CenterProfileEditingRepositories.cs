using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-92 / UC-114] Thay đổi website / fanpage chờ Moderator kiểm tra.</summary>
public class CenterProfileChangeRepository(AppDbContext db) : ICenterProfileChangeRepository
{
    public Task<CenterProfileChange?> GetPendingAsync(Guid studyCenterId, string field, CancellationToken ct = default) =>
        db.CenterProfileChanges.FirstOrDefaultAsync(
            x => x.StudyCenterId == studyCenterId && x.Field == field && x.Status == CenterProfileChangeStatuses.Pending, ct);

    public Task<CenterProfileChange?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.CenterProfileChanges.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(CenterProfileChange change, CancellationToken ct = default)
    {
        db.CenterProfileChanges.Add(change);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CenterProfileChange change, CancellationToken ct = default)
    {
        change.UpdatedAt = DateTime.UtcNow;
        db.CenterProfileChanges.Update(change);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<CenterProfileChange>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var q = db.CenterProfileChanges.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(x => x.Status == CenterProfileChangeStatuses.Pending ? 0 : 1)
            .ThenBy(x => x.Status == CenterProfileChangeStatuses.Pending ? x.CreatedAt : DateTime.MinValue)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);
        return new PagedResult<CenterProfileChange>(items, safePage, safeSize, total);
    }

    public async Task<IReadOnlyList<CenterChangeCenterInfo>> GetCenterInfosAsync(IReadOnlyCollection<Guid> centerIds, CancellationToken ct = default) =>
        await db.StudyCenters.AsNoTracking()
            .Where(c => centerIds.Contains(c.Id))
            .Select(c => new CenterChangeCenterInfo(c.Id, c.Name, c.OwnerUserId, c.VerificationStatus, c.IsActive))
            .ToListAsync(ct);

    public async Task AddNotificationAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>[UC-92 / UC-93] Yêu cầu xóa tài khoản trung tâm.</summary>
public class CenterDeletionRequestRepository(AppDbContext db) : ICenterDeletionRequestRepository
{
    public Task<CenterDeletionRequest?> GetOpenByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
        db.CenterDeletionRequests.FirstOrDefaultAsync(
            x => x.StudyCenterId == studyCenterId && x.Status == CenterDeletionRequestStatuses.Pending, ct);

    public Task<CenterDeletionRequest?> GetLatestByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
        db.CenterDeletionRequests.AsNoTracking()
            .Where(x => x.StudyCenterId == studyCenterId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<CenterDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.CenterDeletionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default)
    {
        db.CenterDeletionRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CenterDeletionRequest request, CancellationToken ct = default)
    {
        request.UpdatedAt = DateTime.UtcNow;
        db.CenterDeletionRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<CenterDeletionRequest>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var q = db.CenterDeletionRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(x => x.Status == CenterDeletionRequestStatuses.Pending ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);
        return new PagedResult<CenterDeletionRequest>(items, safePage, safeSize, total);
    }
}
