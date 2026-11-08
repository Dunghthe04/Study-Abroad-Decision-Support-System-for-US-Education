using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class StudyCenterRepository(AppDbContext db) : IStudyCenterRepository
{
    public async Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
    {
        var q = db.StudyCenters.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(x => EF.Functions.ILike(x.Name, $"%{query.Search}%"));
        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(x => x.City != null && EF.Functions.ILike(x.City, query.City));
        if (!string.IsNullOrWhiteSpace(query.Service))
            q = q.Where(x => x.Services.Contains(query.Service));
        if (!string.IsNullOrWhiteSpace(query.StudyLevel))
            q = q.Where(x => x.StudyLevels.Contains(query.StudyLevel));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<StudyCenter>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
    await db.StudyCenters.AsNoTracking()
        .Where(x => x.IsActive && x.VerificationStatus == CenterReferral.Verified)
        .ToListAsync(ct);

    public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.StudyCenters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.StudyCenters.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
        db.StudyCenters.AnyAsync(x => x.Code.ToLower() == code.ToLower(), ct);

    public async Task AddAsync(StudyCenter center, CancellationToken ct = default)
    {
        await db.StudyCenters.AddAsync(center, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StudyCenter center, CancellationToken ct = default)
    {
        center.UpdatedAt = DateTime.UtcNow;
        db.StudyCenters.Update(center);
        await db.SaveChangesAsync(ct);
    }

    public async Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default)
    {
        var center = await db.StudyCenters.FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId, ct);
        if (center is not null) return center;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLower();
            center = await db.StudyCenters.FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == normalized, ct);
            if (center is not null && center.OwnerUserId is null)
            {
                center.OwnerUserId = ownerUserId;
                center.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        return center;
    }
}
