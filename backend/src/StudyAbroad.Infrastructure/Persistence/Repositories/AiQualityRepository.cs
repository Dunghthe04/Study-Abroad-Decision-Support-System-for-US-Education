using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.AiQuality;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Chỉ đọc nên không theo dõi thay đổi (AsNoTracking).</summary>
public class AiQualityRepository(AppDbContext db) : IAiQualityRepository
{
    public async Task<IReadOnlyList<AiQualityTurnRow>> TurnsAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default) =>
        await AiQualityQuery.Turns(db.ChatMessages.AsNoTracking(), db.AiCalls.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);
}
