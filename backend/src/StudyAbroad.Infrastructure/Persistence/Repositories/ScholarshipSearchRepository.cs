using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Scholarships;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ScholarshipSearchRepository(AppDbContext db) : IScholarshipSearchRepository
{
    public async Task<PagedResult<ScholarshipDto>> SearchAsync(ScholarshipSearchQuery query, CancellationToken ct = default)
    {
        // UC-29: chỉ học bổng đại học do trường cấp (đang bật, trường đang hiện, không có học bổng ngoài trường)
        var q = db.Scholarships.AsNoTracking()
            .Where(s => s.IsActive
                && s.StudyLevel == StudyLevels.Undergraduate
                && s.UniversityId != null
                && s.Kind != "external"
                && db.Universities.Any(u => u.Id == s.UniversityId && u.IsActive));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(s => EF.Functions.ILike(s.Name, pattern)
                || (s.Provider != null && EF.Functions.ILike(s.Provider, pattern))
                || db.Universities.Any(u => u.Id == s.UniversityId && EF.Functions.ILike(u.Name, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(query.CoverageType))
        {
            var coverage = query.CoverageType.Trim().ToLowerInvariant();
            q = q.Where(s => s.CoverageType == coverage);
        }
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            var kind = query.Kind.Trim().ToLowerInvariant();
            q = q.Where(s => s.Kind == kind);
        }
        if (query.VnEligible == true) q = q.Where(s => s.VnEligible == true);
        if (query.MinAmountUsd is not null || query.MaxAmountUsd is not null)
        {
            // UC-29: giữ học bổng toàn phần (full_tuition / full_ride); học bổng không rõ giá trị không hiện
            var lo = query.MinAmountUsd;
            var hi = query.MaxAmountUsd;
            q = q.Where(s =>
                s.AmountType == "full_tuition" || s.AmountType == "full_ride"
                || (s.AmountUsd != null && (lo == null || s.AmountUsd >= lo) && (hi == null || s.AmountUsd <= hi))
                || ((s.AmountMinUsd != null || s.AmountMaxUsd != null)
                    && (lo == null || (s.AmountMaxUsd ?? s.AmountMinUsd) >= lo)
                    && (hi == null || (s.AmountMinUsd ?? s.AmountMaxUsd) <= hi)));
        }
        // điều kiện null = không đòi hỏi, nên vẫn giữ
        if (query.MaxMinGpa4 is { } gpa) q = q.Where(s => s.MinGpa4 == null || s.MinGpa4 <= gpa);
        if (query.MaxMinIelts is { } ielts) q = q.Where(s => s.MinIelts == null || s.MinIelts <= ielts);
        if (query.MaxMinToefl is { } toefl) q = q.Where(s => s.MinToefl == null || s.MinToefl <= toefl);
        if (query.MaxMinDuolingo is { } duolingo) q = q.Where(s => s.MinDuolingo == null || s.MinDuolingo <= duolingo);
        if (query.DeadlineFrom is { } from) q = q.Where(s => s.Deadline == null || s.Deadline >= from);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(s => s.Deadline == null)
            .ThenBy(s => s.Deadline)
            .ThenBy(s => s.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new ScholarshipDto(
                s.Id,
                db.Universities.Where(u => u.Id == s.UniversityId).Select(u => u.Code).FirstOrDefault(),
                db.Universities.Where(u => u.Id == s.UniversityId).Select(u => u.Name).FirstOrDefault(),
                s.Name,
                s.Provider,
                s.StudyLevel,
                s.AmountUsd,
                s.CoverageType,
                s.MinGpa4,
                s.MinIelts,
                s.Deadline,
                s.EligibilityNotes,
                s.SourceUrl,
                s.RetrievedAt,
                s.SourceReliability,
                s.Kind,
                s.AmountType,
                s.AmountMinUsd,
                s.AmountMaxUsd,
                s.DurationYears,
                s.Renewable,
                s.VnEligible,
                s.ApplicationMethod,
                s.ApplyUrl,
                s.MinSat,
                s.MinAct,
                s.MinToefl,
                s.MinDuolingo,
                s.MeritBased,
                s.NeedBased,
                s.MajorRestriction))
            .ToListAsync(ct);

        return new PagedResult<ScholarshipDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScholarshipDto>> GetByUniversityCodeAsync(string universityCode, int maxCount, CancellationToken ct = default)
    {
        // Lấy học bổng IsActive của trường có Code khớp, sắp hạn nộp (null cuối), tối đa maxCount bản ghi
        var items = await db.Scholarships.AsNoTracking()
            .Where(s => s.IsActive
                && s.StudyLevel == StudyLevels.Undergraduate
                && s.Kind != "external"
                && db.Universities.Any(u => u.Id == s.UniversityId && u.IsActive && u.Code == universityCode))
            .OrderBy(s => s.Deadline == null)
            .ThenBy(s => s.Deadline)
            .ThenBy(s => s.Name)
            .Take(maxCount)
            .Select(s => new ScholarshipDto(
                s.Id,
                db.Universities.Where(u => u.Id == s.UniversityId).Select(u => u.Code).FirstOrDefault(),
                db.Universities.Where(u => u.Id == s.UniversityId).Select(u => u.Name).FirstOrDefault(),
                s.Name,
                s.Provider,
                s.StudyLevel,
                s.AmountUsd,
                s.CoverageType,
                s.MinGpa4,
                s.MinIelts,
                s.Deadline,
                s.EligibilityNotes,
                s.SourceUrl,
                s.RetrievedAt,
                s.SourceReliability,
                s.Kind,
                s.AmountType,
                s.AmountMinUsd,
                s.AmountMaxUsd,
                s.DurationYears,
                s.Renewable,
                s.VnEligible,
                s.ApplicationMethod,
                s.ApplyUrl,
                s.MinSat,
                s.MinAct,
                s.MinToefl,
                s.MinDuolingo,
                s.MeritBased,
                s.NeedBased,
                s.MajorRestriction))
            .ToListAsync(ct);

        return items;
    }
}
