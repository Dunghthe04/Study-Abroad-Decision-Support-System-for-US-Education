using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.AiUsage;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Gộp ở database (GROUP BY), chỉ đọc nên không theo dõi thay đổi (AsNoTracking).</summary>
public class AiUsageReportRepository(AppDbContext db) : IAiUsageReportRepository
{
    public async Task<IReadOnlyList<AiUsageGroupRow>> ByKindAndPurposeAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default) =>
        await AiUsageAggregator.ByKindAndPurpose(db.AiCalls.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);

    public async Task<IReadOnlyList<AiUsageGroupRow>> ByDayAndKindAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default) =>
        await AiUsageAggregator.ByDayAndKind(db.AiCalls.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);
}
