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
        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default) =>
            db.Recommendations
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default)
        {
            var rows = await db.UniversityOfferings
                .Where(o => o.StudyLevel == studyLevel)
                .Join(
                    db.Universities.Where(u => u.IsActive),
                    offering => offering.UniversityId,
                    university => university.Id,
                    (offering, university) => new { university, offering })
                .ToListAsync(ct);
            return rows.Select(row => new RecommendSchoolInput(row.university, row.offering)).ToList();
        }

    }
}
