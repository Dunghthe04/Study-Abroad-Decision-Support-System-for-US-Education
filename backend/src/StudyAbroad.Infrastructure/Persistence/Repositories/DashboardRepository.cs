using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Dashboard;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cài đặt truy vấn chi phí trường phục vụ UC-94 (Dashboard KPI).
/// </summary>
public class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    public async Task<IReadOnlyDictionary<Guid, UniversityCostRow>> GetUniversityCostsAsync(
        IEnumerable<Guid> universityIds,
        string level = "undergraduate",
        CancellationToken ct = default)
    {
        var idList = universityIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, UniversityCostRow>();

        var offerings = await db.UniversityOfferings
            .AsNoTracking()
            .Where(o => idList.Contains(o.UniversityId) && (o.StudyLevel == level || o.StudyLevel == null))
            .Select(o => new
            {
                o.UniversityId,
                o.StudyLevel,
                o.TuitionUsd,
                o.LivingUsd,
                o.FeesUsd,
                o.OtherExpensesUsd
            })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, UniversityCostRow>();

        // Ưu tiên offering đúng studyLevel
        foreach (var id in idList)
        {
            var matched = offerings.FirstOrDefault(o => o.UniversityId == id && string.Equals(o.StudyLevel, level, StringComparison.OrdinalIgnoreCase))
                       ?? offerings.FirstOrDefault(o => o.UniversityId == id);

            if (matched is not null)
            {
                result[id] = new UniversityCostRow(
                    matched.UniversityId,
                    matched.TuitionUsd,
                    matched.LivingUsd,
                    matched.FeesUsd,
                    matched.OtherExpensesUsd
                );
            }
        }

        return result;
    }
}
