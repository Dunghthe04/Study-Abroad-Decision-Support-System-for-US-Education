using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
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
        "application_fee_usd", "tuition_in_state_usd", "act_policy", "act_submit_pct",
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

    public async Task<PagedResult<UniversityListItemDto>> SearchAsync(UniversitySearchQuery query, CancellationToken ct = default)
    {
        var q = db.Universities.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.Name, pattern) ||
                             EF.Functions.ILike(u.Code, pattern) ||
                             (u.City != null && EF.Functions.ILike(u.City, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim().ToUpperInvariant();
            q = q.Where(u => u.State == state);
        }

        if (!string.IsNullOrWhiteSpace(query.Control))
        {
            var control = query.Control.Trim().ToLowerInvariant();
            q = q.Where(u => u.Control != null && u.Control.ToLower() == control);
        }

        if (query.IsIvy.HasValue)
        {
            q = q.Where(u => u.IsIvy == query.IsIvy.Value);
        }

        // ponytail: some source rows have AcceptanceRate > 1 (Williams 1.15, Amherst 1.13, Illinois Wesleyan 1.19);
        // treated as missing here until the source data is corrected.
        if (query.MinAcceptanceRate.HasValue)
        {
            q = q.Where(u => u.AcceptanceRate != null && u.AcceptanceRate <= 1 && u.AcceptanceRate >= query.MinAcceptanceRate.Value);
        }

        if (query.MaxAcceptanceRate.HasValue)
        {
            q = q.Where(u => u.AcceptanceRate != null && u.AcceptanceRate <= 1 && u.AcceptanceRate <= query.MaxAcceptanceRate.Value);
        }

        var generalListIds = await db.RankingLists.AsNoTracking()
            .Where(l => l.ListGroup == RecommendationRepository.GeneralRankingGroup)
            .Select(l => l.Id)
            .ToListAsync(ct);

        if (query.MaxRank is > 0)
        {
            var maxRank = query.MaxRank.Value;
            q = q.Where(u => db.UniversityRankings.Any(r =>
                r.UniversityId == u.Id && generalListIds.Contains(r.ListId) && r.Rank <= maxRank));
        }

        var studyLevel = query.StudyLevel;

        if (query.MinTuitionUsd.HasValue || query.MaxTuitionUsd.HasValue)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.TuitionUsd != null &&
                (!query.MinTuitionUsd.HasValue || o.TuitionUsd >= query.MinTuitionUsd.Value) &&
                (!query.MaxTuitionUsd.HasValue || o.TuitionUsd <= query.MaxTuitionUsd.Value)));
        }

        if (query.MinTotalCostUsd.HasValue || query.MaxTotalCostUsd.HasValue)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.TuitionUsd != null &&
                (!query.MinTotalCostUsd.HasValue || (o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)) >= query.MinTotalCostUsd.Value) &&
                (!query.MaxTotalCostUsd.HasValue || (o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)) <= query.MaxTotalCostUsd.Value)));
        }

        if (query.MaxMinIelts.HasValue)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.MinIelts != null && o.MinIelts <= query.MaxMinIelts.Value));
        }

        if (query.MaxMinToefl.HasValue)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.MinToefl != null && o.MinToefl <= query.MaxMinToefl.Value));
        }

        if (query.MaxMinDuolingo.HasValue)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.MinDuolingo != null && o.MinDuolingo <= query.MaxMinDuolingo.Value));
        }

        if (query.TestOptional == true)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                (o.SatPolicy == "optional" || o.SatPolicy == "not_accepted" || o.ActPolicy == "optional" || o.ActPolicy == "not_accepted") &&
                (o.SatPolicy == null || o.SatPolicy != "required") &&
                (o.ActPolicy == null || o.ActPolicy != "required")));
        }

        if (query.HasIntlAid == true)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.IntlAidPolicy != null && o.IntlAidPolicy != "none"));
        }

        var campusPrefix = query.CampusSetting?.Trim().ToLowerInvariant() switch
        {
            "city" => "City:",
            "suburb" => "Suburb:",
            "town" => "Town:",
            "rural" => "Rural:",
            _ => null,
        };
        if (campusPrefix != null)
        {
            q = q.Where(u => db.UniversityOfferings.Any(o =>
                o.UniversityId == u.Id &&
                (studyLevel == null || o.StudyLevel == studyLevel) &&
                o.CampusSetting != null && EF.Functions.ILike(o.CampusSetting, campusPrefix + "%")));
        }

        if (!string.IsNullOrWhiteSpace(query.Major))
        {
            var majorPattern = $"%{query.Major.Trim()}%";
            q = q.Where(u => db.UniversityOfferings
                .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                .Any(o => db.UniversityOfferingMajors
                    .Where(link => link.UniversityOfferingId == o.Id)
                    .Join(db.Majors, link => link.MajorId, m => m.Id, (link, m) => m.Name)
                    .Any(name => EF.Functions.ILike(name, majorPattern))));
        }

        var total = await q.CountAsync(ct);

        var selectedRankListId = string.IsNullOrWhiteSpace(query.RankListId) ? null : query.RankListId.Trim();
        if (selectedRankListId != null && !await db.RankingLists.AsNoTracking().AnyAsync(l => l.Id == selectedRankListId, ct))
        {
            selectedRankListId = null;
        }

        var sortBy = query.SortBy?.ToLowerInvariant();
        IQueryable<University> sorted = sortBy switch
        {
            "rank" => selectedRankListId != null
                ? q
                    .OrderBy(u => db.UniversityRankings
                        .Where(r => r.UniversityId == u.Id && r.ListId == selectedRankListId)
                        .Min(r => (int?)r.Rank) == null)
                    .ThenBy(u => db.UniversityRankings
                        .Where(r => r.UniversityId == u.Id && r.ListId == selectedRankListId)
                        .Min(r => (int?)r.Rank))
                    .ThenBy(u => u.Name)
                : q
                    .OrderBy(u => db.UniversityRankings
                        .Where(r => r.UniversityId == u.Id && generalListIds.Contains(r.ListId))
                        .Min(r => (int?)r.Rank) == null)
                    .ThenBy(u => db.UniversityRankings
                        .Where(r => r.UniversityId == u.Id && generalListIds.Contains(r.ListId))
                        .Min(r => (int?)r.Rank))
                    .ThenBy(u => u.Name),
            "tuition_asc" => q
                .OrderBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Min(o => o.TuitionUsd) == null)
                .ThenBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Min(o => o.TuitionUsd))
                .ThenBy(u => u.Name),
            "tuition_desc" => q
                .OrderBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Max(o => o.TuitionUsd) == null)
                .ThenByDescending(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Max(o => o.TuitionUsd))
                .ThenBy(u => u.Name),
            // Tổng chi phí một năm = học phí + ăn ở + phí + chi khác (cùng cách tính với chat: không có học phí thì không tính)
            "total_cost_asc" => q
                .OrderBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Min(o => o.TuitionUsd == null ? (decimal?)null : o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)) == null)
                .ThenBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Min(o => o.TuitionUsd == null ? (decimal?)null : o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)))
                .ThenBy(u => u.Name),
            "total_cost_desc" => q
                .OrderBy(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Max(o => o.TuitionUsd == null ? (decimal?)null : o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)) == null)
                .ThenByDescending(u => db.UniversityOfferings
                    .Where(o => o.UniversityId == u.Id && (studyLevel == null || o.StudyLevel == studyLevel))
                    .Max(o => o.TuitionUsd == null ? (decimal?)null : o.TuitionUsd + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)))
                .ThenBy(u => u.Name),
            "acceptance_asc" => q
                .OrderBy(u => u.AcceptanceRate == null || u.AcceptanceRate > 1)
                .ThenBy(u => u.AcceptanceRate)
                .ThenBy(u => u.Name),
            "acceptance_desc" => q
                .OrderBy(u => u.AcceptanceRate == null || u.AcceptanceRate > 1)
                .ThenByDescending(u => u.AcceptanceRate)
                .ThenBy(u => u.Name),
            _ => q.OrderBy(u => u.Name),
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 12 : query.PageSize;

        var uList = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (uList.Count == 0)
        {
            return new PagedResult<UniversityListItemDto>([], page, pageSize, total);
        }

        var universityIds = uList.Select(u => u.Id).ToList();
        var stateCodes = uList.Where(u => u.State != null).Select(u => u.State!).Distinct().ToList();
        var stateNames = await db.UsStates.AsNoTracking()
            .Where(s => stateCodes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, s => s.Name, ct);

        var logos = await db.Media.AsNoTracking()
            .Where(m => m.EntityType == "University" && universityIds.Contains(m.EntityId) && m.Type == "logo")
            .GroupBy(m => m.EntityId)
            .Select(g => new { UniversityId = g.Key, Url = g.OrderBy(m => m.DisplayOrder).Select(m => m.Url).FirstOrDefault() })
            .ToDictionaryAsync(x => x.UniversityId, x => x.Url, ct);

        var covers = await db.Media.AsNoTracking()
            .Where(m => m.EntityType == "University" && universityIds.Contains(m.EntityId) && m.Type == "cover")
            .GroupBy(m => m.EntityId)
            .Select(g => new { UniversityId = g.Key, Url = g.OrderBy(m => m.DisplayOrder).Select(m => m.Url).FirstOrDefault() })
            .ToDictionaryAsync(x => x.UniversityId, x => x.Url, ct);

        var primaryOfferings = await db.UniversityOfferings.AsNoTracking()
            .Where(o => universityIds.Contains(o.UniversityId) && (studyLevel == null || o.StudyLevel == studyLevel))
            .OrderByDescending(o => o.AcademicYear)
            .ToListAsync(ct);

        var offeringByUni = primaryOfferings
            .GroupBy(o => o.UniversityId)
            .ToDictionary(g => g.Key, g => g.First());

        var offeringIds = offeringByUni.Values.Select(o => o.Id).ToList();
        var majorsByOffering = await db.UniversityOfferingMajors.AsNoTracking()
            .Where(link => offeringIds.Contains(link.UniversityOfferingId))
            .Join(db.Majors.AsNoTracking(), link => link.MajorId, m => m.Id, (link, m) => new { link.UniversityOfferingId, MajorName = m.Name })
            .GroupBy(x => x.UniversityOfferingId)
            .ToDictionaryAsync(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.MajorName).Distinct().OrderBy(n => n).Take(5).ToList(),
                ct);

        var activeRankListId = sortBy == "rank" ? selectedRankListId : null;
        var badgeListIds = activeRankListId != null && !generalListIds.Contains(activeRankListId)
            ? generalListIds.Append(activeRankListId).ToList()
            : generalListIds;

        var allRankings = await db.UniversityRankings.AsNoTracking()
            .Where(r => universityIds.Contains(r.UniversityId) && badgeListIds.Contains(r.ListId))
            .Join(db.RankingLists.AsNoTracking(), r => r.ListId, l => l.Id, (r, l) => new { r.UniversityId, r.ListId, r.Rank, ListName = l.Name })
            .OrderBy(x => x.Rank)
            .ToListAsync(ct);

        var bestRankByUni = allRankings
            .GroupBy(x => x.UniversityId)
            .ToDictionary(
                g => g.Key,
                g => (activeRankListId != null ? g.FirstOrDefault(x => x.ListId == activeRankListId) : null) ?? g.First());

        var items = uList.Select(u =>
        {
            offeringByUni.TryGetValue(u.Id, out var offering);
            var offeringId = offering?.Id;
            var majors = (offeringId.HasValue && majorsByOffering.TryGetValue(offeringId.Value, out var mList))
                ? mList
                : (IReadOnlyList<string>)[];

            bestRankByUni.TryGetValue(u.Id, out var bestRank);
            logos.TryGetValue(u.Id, out var logoUrl);
            covers.TryGetValue(u.Id, out var coverUrl);

            string? stateName = null;
            if (u.State != null)
            {
                stateNames.TryGetValue(u.State, out stateName);
            }

            decimal? totalCost = null;
            if (offering?.TuitionUsd != null)
            {
                // Cùng công thức với chat và bộ gợi ý: học phí + ăn ở + phí + chi khác
                totalCost = offering.TuitionUsd.Value + (offering.LivingUsd ?? 0) + (offering.FeesUsd ?? 0) + (offering.OtherExpensesUsd ?? 0);
            }

            return new UniversityListItemDto(
                u.Id,
                u.Code,
                u.Name,
                u.City,
                u.State,
                stateName,
                u.Control,
                u.AcceptanceRate > 1 ? null : u.AcceptanceRate,
                u.IsIvy,
                logoUrl,
                coverUrl,
                offering?.TuitionUsd,
                totalCost,
                offering?.MinIelts,
                offering?.MinToefl,
                offering?.MinDuolingo,
                bestRank?.Rank,
                bestRank?.ListName,
                offering?.SatPolicy,
                offering?.ActPolicy,
                offering?.IntlAidPolicy,
                majors);
        }).ToList();

        return new PagedResult<UniversityListItemDto>(items, page, pageSize, total);
    }

    public async Task<UniversityFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default)
    {
        var activeStateCodes = await db.Universities.AsNoTracking()
            .Where(u => u.IsActive && u.State != null)
            .Select(u => u.State!)
            .Distinct()
            .ToListAsync(ct);

        var states = await db.UsStates.AsNoTracking()
            .Where(s => activeStateCodes.Contains(s.Code))
            .OrderBy(s => s.Name)
            .Select(s => new UniversityFilterOptionItem(s.Code, $"{s.Name} ({s.Code})"))
            .ToListAsync(ct);

        var controls = new List<UniversityFilterOptionItem>
        {
            new("public", "Công lập (Public)"),
            new("private", "Tư thục (Private)")
        };

        var majors = await db.Majors.AsNoTracking()
            .Where(m => db.UniversityOfferingMajors.Any(link =>
                link.MajorId == m.Id &&
                db.UniversityOfferings.Any(o => o.Id == link.UniversityOfferingId &&
                    db.Universities.Any(u => u.Id == o.UniversityId && u.IsActive))))
            .OrderBy(m => m.NameVi ?? m.Name)
            .Take(100)
            .Select(m => new UniversityFilterOptionItem(m.Name, m.NameVi ?? m.Name))
            .ToListAsync(ct);

        var costs = await db.UniversityOfferings.AsNoTracking()
            .Where(o => db.Universities.Any(u => u.Id == o.UniversityId && u.IsActive) && o.TuitionUsd != null)
            .Select(o => new
            {
                Tuition = o.TuitionUsd!.Value,
                Total = o.TuitionUsd!.Value + (o.LivingUsd ?? 0) + (o.FeesUsd ?? 0) + (o.OtherExpensesUsd ?? 0)
            })
            .ToListAsync(ct);

        var minTuition = costs.Count > 0 ? costs.Min(c => c.Tuition) : 0m;
        var maxTuition = costs.Count > 0 ? costs.Max(c => c.Tuition) : 100000m;
        var minTotal = costs.Count > 0 ? costs.Min(c => c.Total) : 0m;
        var maxTotal = costs.Count > 0 ? costs.Max(c => c.Total) : 120000m;

        var activeListIds = await db.UniversityRankings.AsNoTracking()
            .Where(r => db.Universities.Any(u => u.Id == r.UniversityId && u.IsActive))
            .Select(r => r.ListId)
            .Distinct()
            .ToListAsync(ct);

        var rawRankingLists = await db.RankingLists.AsNoTracking()
            .Where(l => activeListIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name, l.ListGroup })
            .ToListAsync(ct);

        string[] groupOrder = [RecommendationRepository.GeneralRankingGroup, "Nhóm con", "Giá trị", "Theo ngành", "Khu vực"];
        var rankingLists = rawRankingLists
            .OrderBy(l => Array.IndexOf(groupOrder, l.ListGroup) is var idx and >= 0 ? idx : groupOrder.Length)
            .ThenBy(l => l.Id == "USN27-NU" ? 0 : l.Id == "USN27-LAC" ? 1 : 2)
            .ThenBy(l => l.Name)
            .Select(l => new UniversityFilterOptionItem(l.Id, l.Name))
            .ToList();

        return new UniversityFilterOptionsDto(
            states,
            controls,
            majors,
            minTuition,
            maxTuition,
            minTotal,
            maxTotal,
            rankingLists);
    }
}

