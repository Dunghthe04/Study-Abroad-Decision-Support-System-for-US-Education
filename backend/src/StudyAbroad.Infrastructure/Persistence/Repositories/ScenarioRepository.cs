using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ScenarioRepository(AppDbContext db) : IScenarioRepository
{
    public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.WhatIfSimulations.CountAsync(x => x.UserId == userId, ct);

    public Task<List<WhatIfSimulation>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.WhatIfSimulations.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<WhatIfSimulation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.WhatIfSimulations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(WhatIfSimulation row, CancellationToken ct = default)
    {
        db.WhatIfSimulations.Add(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WhatIfSimulation row, CancellationToken ct = default)
    {
        db.WhatIfSimulations.Update(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(WhatIfSimulation row, CancellationToken ct = default)
    {
        db.WhatIfSimulations.Remove(row);
        await db.SaveChangesAsync(ct);
    }
}
