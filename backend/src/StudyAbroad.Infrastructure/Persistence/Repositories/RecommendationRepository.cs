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
            var offeringIds = rows.Select(row => row.offering.Id).ToList();
            var majorsByOffering = await LoadMajorNamesAsync(offeringIds, ct);

            var schools = new List<RecommendSchoolInput>();
            foreach (var row in rows)
            {
                var majors = majorsByOffering.GetValueOrDefault(row.offering.Id) ?? [];
                schools.Add(new RecommendSchoolInput(row.university, row.offering, majors));
            }
            return schools;
        }

        public async Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default)
        {
            var row = await db.UniversityOfferings
                .Where(o => o.StudyLevel == studyLevel)
                .Join(
                    db.Universities.Where(u => u.IsActive && u.Code == code),
                    offering => offering.UniversityId,
                    university => university.Id,
                    (offering, university) => new { university, offering })
                .FirstOrDefaultAsync(ct);
            if (row is null) return null;

            var majorsByOffering = await LoadMajorNamesAsync([row.offering.Id], ct);
            var majors = majorsByOffering.GetValueOrDefault(row.offering.Id) ?? [];
            return new RecommendSchoolInput(row.university, row.offering, majors);
        }

        private async Task<Dictionary<Guid, List<string>>> LoadMajorNamesAsync(List<Guid> offeringIds, CancellationToken ct)
        {
            var links = await db.UniversityOfferingMajors
                .Where(link => offeringIds.Contains(link.UniversityOfferingId))
                .Join(
                    db.Majors,
                    link => link.MajorId,
                    major => major.Id,
                    (link, major) => new { link.UniversityOfferingId, major.Name })
                .ToListAsync(ct);

            var majorsByOffering = new Dictionary<Guid, List<string>>();
            foreach (var link in links)
            {
                if (!majorsByOffering.TryGetValue(link.UniversityOfferingId, out var names))
                {
                    names = [];
                    majorsByOffering[link.UniversityOfferingId] = names;
                }
                names.Add(link.Name);
            }
            return majorsByOffering;
        }

    }
}
