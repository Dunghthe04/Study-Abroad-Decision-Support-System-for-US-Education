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

        public Task<Recommendation?> GetLatestRecommendationAsync(Guid userId, CancellationToken ct = default)
        {
            return db.Recommendations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)   // mới nhất lên đầu
            .FirstOrDefaultAsync(ct);              // lấy 1 dòng, không có thì null
        }

        public Task<StudentProfile?> GetProfileAsync(Guid studentId, CancellationToken ct = default)
        {
            return db.StudentProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == studentId, ct);
        }

        public async Task<IReadOnlyList<SchoolCandidate>> GetSchoolCandidatesAsync(string studyLevel, CancellationToken ct = default)
        {
           return await (from o in db.UniversityOfferings.AsNoTracking()
                   join u in db.Universities.AsNoTracking() on o.UniversityId equals u.Id
                   where u.IsActive && o.StudyLevel == studyLevel
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
                       o.FeesUsd,
                       u.AcceptanceRate,
                       o.MinIelts,
                       o.MinToefl,
                       o.MinDuolingo,
                       u.City,
                       u.Control,
                       u.Website,
                       u.InternationalStudentCount,
                       o.SatPolicy
                   )).ToListAsync(ct);
        }

        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default)
        {
            return db.AppSettings.AsNoTracking()
                .Where(s => s.Key == key).Select(s => s.ValueJson).FirstOrDefaultAsync(ct);
        }
    }
}
