using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories
{
    public class RecommendationRepository(AppDbContext db) : IRecommendationRepository
    {
        public async Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        {
            db.Recommendations.Add(recommendation);
            await db.SaveChangesAsync(ct);
        }

        public Task<string?> GetLatestAnalysisJsonAsync(Guid studentProfileId, CancellationToken ct = default)
        {
            return db.AnalysisResults.AsNoTracking()
                .Where(a => a.StudentProfileId == studentProfileId && a.Kind == "strengths_weaknesses")
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.ResultJson)
                .FirstOrDefaultAsync(ct);
        }

        public Task<StudentProfile?> GetProfileAsync(Guid studentId, CancellationToken ct = default)
        {
            return db.StudentProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == studentId, ct);
        }

        public async Task<IReadOnlyList<SchoolCandidate>> GetSchoolCandidatesAsync(string stuidyLevel, CancellationToken ct = default)
        {
           return await (from o in db.UniversityOfferings.AsNoTracking()
                   join u in db.Universities.AsNoTracking() on o.UniversityId equals u.Id
                   where u.IsActive && o.StudyLevel == stuidyLevel
                   select new SchoolCandidate(
                       u.Id,
                       o.Id,
                       u.Code,
                       u.Name,
                       u.State,
                       o.Majors,
                       o.AvgGpa4,
                       o.Sat25,
                       o.Sat75,
                       o.TuitionUsd,
                       o.LivingUsd,
                       o.FeesUsd
                   )).ToListAsync(ct);
        }

        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default)
        {
            return db.AppSettings.AsNoTracking()
                .Where(s => s.Key == key).Select(s => s.ValueJson).FirstOrDefaultAsync(ct);
        }
    }
}
