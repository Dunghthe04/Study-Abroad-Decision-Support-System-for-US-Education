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
            var universityIds = rows.Select(row => row.university.Id).ToList();
            var rankings = await LoadRankingsAsync(universityIds, ct);
            var images = await LoadImagesAsync(universityIds, ct);

            var schools = new List<RecommendSchoolInput>();
            foreach (var row in rows)
            {
                var majors = majorsByOffering.GetValueOrDefault(row.offering.Id) ?? [];
                schools.Add(new RecommendSchoolInput(
                    row.university, row.offering, majors, rankings.GetValueOrDefault(row.university.Id),
                    images.GetValueOrDefault(row.university.Id)));
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
            var rankings = await LoadRankingsAsync([row.university.Id], ct);
            var images = await LoadImagesAsync([row.university.Id], ct);
            return new RecommendSchoolInput(
                row.university, row.offering, majors, rankings.GetValueOrDefault(row.university.Id),
                images.GetValueOrDefault(row.university.Id));
        }

        // Logo và ảnh bìa của các trường (bảng media). Một truy vấn cho cả danh sách, không truy vấn từng trường.
        // Một trường có nhiều dòng cùng loại thì lấy dòng đứng đầu theo display_order.
        private async Task<Dictionary<Guid, SchoolImages>> LoadImagesAsync(List<Guid> universityIds, CancellationToken ct)
        {
            var media = await db.Media
                .Where(m => m.EntityType == "University" && universityIds.Contains(m.EntityId)
                            && (m.Type == "logo" || m.Type == "cover"))
                .Select(m => new { m.EntityId, m.Type, m.Url, m.DisplayOrder })
                .ToListAsync(ct);

            return media
                .GroupBy(m => m.EntityId)
                .ToDictionary(
                    g => g.Key,
                    g => new SchoolImages(
                        g.Where(m => m.Type == "logo").OrderBy(m => m.DisplayOrder).Select(m => m.Url).FirstOrDefault(),
                        g.Where(m => m.Type == "cover").OrderBy(m => m.DisplayOrder).Select(m => m.Url).FirstOrDefault()));
        }

        // Nhóm bảng xếp hạng dùng cho chất lượng: các bảng chung (National Universities, Liberal Arts Colleges).
        // Bảng theo ngành, theo khu vực, "giá trị" không dùng vì chỉ có vài trường hoặc đo thứ khác.
        public const string GeneralRankingGroup = "Tổng quát";

        // Hạng của từng trường trong các bảng chung. Tổng số trường của bảng lấy bằng hạng lớn nhất của bảng đó.
        private async Task<Dictionary<Guid, IReadOnlyList<RankPosition>>> LoadRankingsAsync(
            List<Guid> universityIds, CancellationToken ct)
        {
            var listIds = await db.RankingLists
                .Where(l => l.ListGroup == GeneralRankingGroup)
                .Select(l => l.Id)
                .ToListAsync(ct);

            var totals = await db.UniversityRankings
                .Where(r => listIds.Contains(r.ListId))
                .GroupBy(r => r.ListId)
                .Select(g => new { ListId = g.Key, Total = g.Max(r => r.Rank) })
                .ToDictionaryAsync(x => x.ListId, x => x.Total, ct);

            var ranks = await db.UniversityRankings
                .Where(r => listIds.Contains(r.ListId) && universityIds.Contains(r.UniversityId))
                .ToListAsync(ct);

            return ranks
                .GroupBy(r => r.UniversityId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<RankPosition>)g.Select(r => new RankPosition(r.Rank, totals[r.ListId])).ToList());
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
