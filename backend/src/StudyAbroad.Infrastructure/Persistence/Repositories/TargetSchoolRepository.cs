using StudyAbroad.Application.Costs;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ITargetSchoolRepository thao tác trực tiếp với cơ sở dữ liệu qua EF Core.
/// </summary>
public class TargetSchoolRepository(AppDbContext db) : ITargetSchoolRepository
{
    public Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TargetSchools.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.TargetSchools.AsNoTracking()
            .Where(ts => ts.Id == id)
            .Join(db.Universities.AsNoTracking(),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new { ts, u })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var meta = await LoadOfferingMetaAsync([row.u.Id], ct);
        return ToDto(row.ts, row.u, meta);
    }

    public async Task<List<TargetSchoolDto>> GetListByUserAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
    {
        var query = db.TargetSchools.AsNoTracking()
            .Where(ts => ts.UserId == userId);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(ts => ts.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            query = query.Where(ts => ts.Status == st);
        }

        var rows = await query
            .Join(db.Universities.AsNoTracking(),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new { ts, u })
            .ToListAsync(ct);

        var meta = await LoadOfferingMetaAsync(rows.Select(r => r.u.Id).Distinct(), ct);

        return rows
            .Select(r => ToDto(r.ts, r.u, meta))
            .OrderBy(ts => ts.Priority)
            .ThenBy(ts => ts.Deadline == null)
            .ThenBy(ts => ts.Deadline)
            .ThenBy(ts => ts.CreatedAt)
            .ToList();
    }

    public Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
        db.TargetSchools.FirstOrDefaultAsync(x => x.UserId == userId && x.UniversityId == universityId, ct);

    public Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
        db.TargetSchools.AnyAsync(x => x.UserId == userId && x.UniversityId == universityId, ct);

    public async Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        db.TargetSchools.Add(targetSchool);
        await db.SaveChangesAsync(ct);
        return targetSchool;
    }

    public async Task DeleteAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        db.TargetSchools.Remove(targetSchool);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        targetSchool.UpdatedAt = DateTime.UtcNow;
        db.TargetSchools.Update(targetSchool);
        await db.SaveChangesAsync(ct);
    }

    public Task<TargetSchool?> GetByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) =>
        db.TargetSchools
            .Join(db.Universities, ts => ts.UniversityId, u => u.Id, (ts, u) => new { ts, u })
            .Where(x => x.ts.UserId == userId && (x.u.Code == code || x.u.Code.ToLower() == code.ToLower()))
            .Select(x => x.ts)
            .FirstOrDefaultAsync(ct);

    public async Task<TargetSchoolDto?> GetDtoByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var row = await db.TargetSchools.AsNoTracking()
            .Where(ts => ts.UserId == userId)
            .Join(db.Universities.AsNoTracking().Where(u => u.Code == code || u.Code.ToLower() == code.ToLower()),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new { ts, u })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var meta = await LoadOfferingMetaAsync([row.u.Id], ct);
        return ToDto(row.ts, row.u, meta);
    }

    public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
        db.Universities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == universityId && x.IsActive, ct);

    public Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default) =>
        db.Universities.AsNoTracking().FirstOrDefaultAsync(x => (x.Code == code || x.Code.ToLower() == code.ToLower()) && x.IsActive, ct);

    public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) =>
        db.UniversityOfferings.AsNoTracking()
            .Where(x => x.UniversityId == universityId && x.DeadlinesJson != null)
            .OrderBy(x => x.StudyLevel == "undergraduate" ? 0 : 1)
            .ThenByDescending(x => x.AcademicYear)
            .Select(x => x.DeadlinesJson)
            .FirstOrDefaultAsync(ct);

    public async Task<Dictionary<Guid, string>> GetLatestRecommendedCategoriesAsync(Guid userId, CancellationToken ct = default)
    {
        var itemsJson = await db.Recommendations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.ItemsJson)
            .FirstOrDefaultAsync(ct);

        return ParseRecommendedCategories(itemsJson);
    }

    public async Task<string?> GetLatestRecommendedCategoryAsync(Guid userId, Guid universityId, CancellationToken ct = default)
    {
        var map = await GetLatestRecommendedCategoriesAsync(userId, ct);
        return map.TryGetValue(universityId, out var cat) ? cat : null;
    }

    public Task<string?> GetUserScholarshipNeedAsync(Guid userId, CancellationToken ct = default) =>
        db.StudentProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ScholarshipNeed)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> FindSchoolWithRoundAsync(
        Guid userId,
        string canonicalRound,
        Guid? excludeTargetId = null,
        CancellationToken ct = default)
    {
        var query = db.TargetSchools.AsNoTracking()
            .Where(ts => ts.UserId == userId
                && ts.Status != StudyAbroad.Domain.Constants.TargetSchoolStatuses.ParentSuggested
                && ts.ApplicationRound != null);

        if (excludeTargetId.HasValue)
            query = query.Where(ts => ts.Id != excludeTargetId.Value);

        var rows = await query
            .Join(db.Universities.AsNoTracking(),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new { ts.ApplicationRound, u.Name })
            .ToListAsync(ct);

        return rows
            .FirstOrDefault(r => string.Equals(
                TargetSchoolService.CanonicalRound(r.ApplicationRound!),
                canonicalRound,
                StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private sealed record OfferingMeta(decimal? EstimatedAnnualCostUsd, IReadOnlyList<string> AvailableRounds);

    private async Task<Dictionary<Guid, OfferingMeta>> LoadOfferingMetaAsync(IEnumerable<Guid> universityIds, CancellationToken ct)
    {
        var ids = universityIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var offerings = await db.UniversityOfferings.AsNoTracking()
            .Where(o => ids.Contains(o.UniversityId))
            .OrderBy(o => o.StudyLevel == "undergraduate" ? 0 : 1)
            .ThenByDescending(o => o.AcademicYear)
            .Select(o => new
            {
                o.UniversityId,
                o.TuitionUsd,
                o.LivingUsd,
                o.FeesUsd,
                o.OtherExpensesUsd,
                o.HealthInsuranceUsd,
                o.DeadlinesJson,
            })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, OfferingMeta>(ids.Length);
        foreach (var g in offerings.GroupBy(o => o.UniversityId))
        {
            var o = g.First();
            var totalCost = AnnualCost.Of(o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd, o.HealthInsuranceUsd);
            var rounds = TargetSchoolService.ExtractAvailableRounds(o.DeadlinesJson);
            result[g.Key] = new OfferingMeta(totalCost, rounds);
        }

        return result;
    }

    private static TargetSchoolDto ToDto(TargetSchool ts, University u, Dictionary<Guid, OfferingMeta> meta)
    {
        meta.TryGetValue(u.Id, out var m);
        return new TargetSchoolDto(
            ts.Id,
            ts.UserId,
            ts.UniversityId,
            u.Code,
            u.Name,
            u.City,
            u.State,
            u.Website,
            u.Control,
            u.AcceptanceRate,
            ts.Category,
            ts.Status,
            ts.Priority,
            ts.Notes,
            ts.ApplicationRound,
            ts.Deadline,
            ts.CreatedAt,
            ts.UpdatedAt,
            m?.EstimatedAnnualCostUsd,
            ts.IsCategoryUserSelected,
            m?.AvailableRounds ?? []);
    }

    private static Dictionary<Guid, string> ParseRecommendedCategories(string? itemsJson)
    {
        var map = new Dictionary<Guid, string>();
        if (string.IsNullOrWhiteSpace(itemsJson)) return map;

        try
        {
            var root = JsonNode.Parse(itemsJson) as JsonObject;
            var scored = root?["scored"] as JsonArray;
            if (scored is null) return map;

            foreach (var entry in scored)
            {
                if (entry is not JsonObject obj) continue;
                var idStr = obj["universityId"]?.GetValue<string>();
                if (!Guid.TryParse(idStr, out var uniId)) continue;

                var cat = MapGroupToCategory(obj["g2"]?.GetValue<string>() ?? obj["category"]?.GetValue<string>());
                if (cat is not null)
                    map[uniId] = cat;
            }
        }
        catch
        {
            // Bỏ qua JSON hỏng của lần gợi ý cũ
        }

        return map;
    }

    private static string? MapGroupToCategory(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "R" or "REACH" => "reach",
        "M" or "MATCH" => "match",
        "S" or "SAFETY" => "safety",
        _ => null,
    };
}
