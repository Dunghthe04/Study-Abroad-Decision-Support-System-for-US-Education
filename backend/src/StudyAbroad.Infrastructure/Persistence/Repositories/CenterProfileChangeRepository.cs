using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-92, UC-114] Đọc / ghi app.center_profile_changes.</summary>
public class CenterProfileChangeRepository(AppDbContext db) : ICenterProfileChangeRepository
{
    public Task<CenterProfileChange?> GetPendingAsync(Guid centerId, string field, CancellationToken ct = default) =>
        db.CenterProfileChanges
            .Where(c => c.StudyCenterId == centerId && c.Field == field && c.Status == CenterProfileChangeService.StatusPending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<CenterProfileChange?> GetForUpdateAsync(Guid changeId, CancellationToken ct = default) =>
        db.CenterProfileChanges.FirstOrDefaultAsync(c => c.Id == changeId, ct);

    public async Task AddAsync(CenterProfileChange change, CancellationToken ct = default)
    {
        db.CenterProfileChanges.Add(change);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CenterProfileChange change, CancellationToken ct = default)
    {
        change.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<CenterProfileChangeDto>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = from c in db.CenterProfileChanges.AsNoTracking()
                    join s in db.StudyCenters.AsNoTracking() on c.StudyCenterId equals s.Id
                    select new { Change = c, CenterName = s.Name };

        if (status != "all")
            query = query.Where(x => x.Change.Status == status);

        var total = await query.CountAsync(ct);
        // Hàng đợi chờ kiểm tra: chờ lâu nhất lên đầu. Các danh sách khác: mới nhất lên đầu.
        var ordered = status == CenterProfileChangeService.StatusPending
            ? query.OrderBy(x => x.Change.CreatedAt).ThenBy(x => x.Change.Id)
            : query.OrderByDescending(x => x.Change.CreatedAt).ThenByDescending(x => x.Change.Id);

        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(x => new CenterProfileChangeDto(
            x.Change.Id, x.Change.StudyCenterId, x.CenterName, x.Change.Field, x.Change.OldValue, x.Change.NewValue,
            x.Change.Status, x.Change.CreatedAt, x.Change.ReviewedByUserId, x.Change.ReviewedAt, x.Change.ReviewReason)).ToList();

        return new PagedResult<CenterProfileChangeDto>(items, page, pageSize, total);
    }

    public async Task<(string Name, Guid? OwnerUserId)?> GetCenterInfoAsync(Guid centerId, CancellationToken ct = default)
    {
        var row = await db.StudyCenters.AsNoTracking()
            .Where(s => s.Id == centerId)
            .Select(s => new { s.Name, s.OwnerUserId })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.Name, row.OwnerUserId);
    }
}
