using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Universities;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class UniversityDetailRepository(AppDbContext db) : IUniversityDetailRepository
{
    private static readonly string[] PublicFields =
    [
        "name", "city", "state", "website", "control", "acceptance_rate", "international_student_count", "is_ivy",
        "academic_year", "tuition_usd", "living_usd", "fees_usd", "other_expenses_usd",
        "min_gpa4", "min_ielts", "min_toefl", "min_duolingo", "avg_gpa4", "sat25", "sat75", "act25", "act75",
        "applicants_total", "admitted_total", "intl_applicants", "intl_admitted", "intl_acceptance_rate", "sat_policy", "deadlines_json",
        "c7_gpa_importance", "c7_test_importance", "c7_ec_importance", "sat_submit_pct", "gpa_submit_pct",
        "gpa_pct_4_0", "gpa_pct_375_399", "gpa_pct_350_374", "gpa_pct_325_349", "gpa_pct_300_324", "gpa_pct_250_299", "gpa_pct_below_250",
        "intl_aid_policy", "intl_aid_avg_usd", "intl_aid_recipients", "retention_rate_1yr", "grad_rate_6yr"
    ];

    public async Task<UniversityDetailData?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var university = await db.Universities.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Code == code && u.IsActive, ct);
        if (university is null) return null;

        var stateName = await db.UsStates.AsNoTracking().Where(s => s.Code == university.State)
            .Select(s => s.Name).FirstOrDefaultAsync(ct);
        var offerings = await db.UniversityOfferings.AsNoTracking().Where(o => o.UniversityId == university.Id)
            .OrderBy(o => o.StudyLevel).ThenByDescending(o => o.AcademicYear).ThenBy(o => o.Id).ToListAsync(ct);
        var offeringIds = offerings.Select(o => o.Id).ToArray();
        var majors = await QueryMajors(offeringIds).ToListAsync(ct);
        var media = await db.Media.AsNoTracking()
            .Where(m => m.EntityType == "University" && m.EntityId == university.Id &&
                (m.Type == "logo" || m.Type == "cover" || m.Type == "gallery"))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Take(50).ToListAsync(ct);
        var rankings = await QueryRankings(university.Id).ToListAsync(ct);
        var evidence = await db.FieldEvidences.AsNoTracking()
            .Where(e => e.UniversityId == university.Id && PublicFields.Contains(e.FieldName))
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.FieldName).ThenBy(e => e.Id)
            .Take(256)
            .Select(e => new FieldEvidence
            {
                Id = e.Id, FieldName = e.FieldName,
                FieldValue = e.FieldValue != null && e.FieldValue.Length <= 500 ? e.FieldValue : null,
                SourceUrl = e.SourceUrl, SourceSection = e.SourceSection, Status = e.Status, CreatedAt = e.CreatedAt,
                ExtractedQuote = e.ExtractedQuote != null && e.ExtractedQuote.Length <= 16384 ? e.ExtractedQuote : null
            }).ToListAsync(ct);
        return new UniversityDetailData(university, stateName, offerings, majors, media, rankings, evidence);
    }

    private IQueryable<UniversityMajorRow> QueryMajors(Guid[] offeringIds) =>
        db.UniversityOfferingMajors.AsNoTracking()
            .Where(m => offeringIds.Contains(m.UniversityOfferingId))
            .Join(db.Majors.AsNoTracking(), link => link.MajorId, major => major.Id,
                (link, major) => new { Link = link, Major = major })
            .OrderBy(row => row.Major.Name)
            .Select(row => new UniversityMajorRow(row.Link.UniversityOfferingId, row.Major.Name,
                row.Major.CipCode, row.Link.IsStem, row.Link.IsVerified));

    private IQueryable<UniversityRankingDto> QueryRankings(Guid universityId) =>
        db.UniversityRankings.AsNoTracking().Where(r => r.UniversityId == universityId)
            .Join(db.RankingLists.AsNoTracking(), rank => rank.ListId, list => list.Id,
                (rank, list) => new { Rank = rank, List = list })
            .OrderByDescending(row => row.List.EditionYear).ThenBy(row => row.List.Name)
            .Select(row => new UniversityRankingDto(row.List.Publisher, row.List.Name, row.Rank.Rank,
                row.Rank.IsTie, row.Rank.RankDisplay, row.List.Edition, row.List.EditionYear, row.List.Url));
}
