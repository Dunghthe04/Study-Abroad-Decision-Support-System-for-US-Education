using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Dashboard.Roadmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class RoadmapRepository(AppDbContext db) : IRoadmapRepository
{
    public async Task<IReadOnlyDictionary<Guid, RoadmapOfferingRow>> GetOfferingsAsync(
        IReadOnlyCollection<Guid> universityIds, string studyLevel, CancellationToken ct = default)
    {
        if (universityIds.Count == 0)
            return new Dictionary<Guid, RoadmapOfferingRow>();

        var rows = await db.UniversityOfferings
            .AsNoTracking()
            .Where(o => o.StudyLevel == studyLevel && universityIds.Contains(o.UniversityId))
            .Select(o => new RoadmapOfferingRow(
                o.UniversityId, o.AvgGpa4, o.MinIelts, o.MinToefl, o.MinDuolingo,
                o.Sat25, o.Sat75, o.SatPolicy, o.DeadlinesJson))
            .ToListAsync(ct);

        // mỗi trường một offering cho mỗi bậc học; phòng dữ liệu trùng thì lấy dòng đầu
        return rows.GroupBy(r => r.UniversityId).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<IReadOnlyList<RoadmapScholarshipRow>> GetScholarshipsAsync(
        IReadOnlyCollection<Guid> universityIds, string studyLevel, CancellationToken ct = default)
    {
        if (universityIds.Count == 0)
            return [];

        return await db.Scholarships
            .AsNoTracking()
            .Where(s => s.IsActive
                && s.UniversityId != null && universityIds.Contains(s.UniversityId.Value)
                && s.StudyLevel == studyLevel
                && (s.VnEligible == true || s.IntlEligible == true))
            .OrderBy(s => s.Deadline)
            .Select(s => new RoadmapScholarshipRow(
                s.Id, s.UniversityId!.Value, s.Name, s.MinGpa4, s.MinIelts, s.MinToefl, s.MinDuolingo, s.MinSat, s.Deadline))
            .ToListAsync(ct);
    }
}
