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

    public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.StudyCenters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
}
