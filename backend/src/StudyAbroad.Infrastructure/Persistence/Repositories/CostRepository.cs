using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Costs;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CostRepository(AppDbContext db) : ICostRepository
{
    public async Task<IReadOnlyList<SchoolCostRow>> GetSchoolCostsAsync(IReadOnlyList<string> codes, string studyLevel, CancellationToken ct = default)
    {
        var upper = codes.Select(c => c.ToUpper()).ToList();
        // Trường không có dữ liệu bậc học này vẫn trả về dòng toàn null để giao diện ghi "chưa có"
        return await db.Universities.AsNoTracking()
            .Where(u => u.IsActive && upper.Contains(u.Code.ToUpper()))
            .Select(u => new SchoolCostRow(
                u.Code, u.Name, u.State, studyLevel,
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.AcademicYear).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.TuitionUsd).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.LivingUsd).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.FeesUsd).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.OtherExpensesUsd).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.HealthInsuranceUsd).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.SourceUrl).FirstOrDefault(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id && o.StudyLevel == studyLevel).Select(o => o.RetrievedAt).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default) =>
        db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => (string?)s.ValueJson).FirstOrDefaultAsync(ct);

    public async Task<BudgetInput?> GetProfileBudgetAsync(Guid userId, CancellationToken ct = default)
    {
        var p = await db.StudentProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.BudgetTier, x.BudgetMinUsd, x.BudgetMaxUsd }).FirstOrDefaultAsync(ct);
        if (p is null) return null;
        if (p.BudgetMaxUsd is { } max) return new BudgetInput("profile", max, false);
        // Mức "trên 70.000 USD" lưu max null: có ngân sách nhưng không có trần
        if (p.BudgetTier == "tier_above_70k" || p.BudgetMinUsd is not null) return new BudgetInput("profile", null, true);
        return null;
    }

    public async Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default) =>
        await db.UsStates.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Code, s.Name, s.Region,
                Latest = db.StateLivingCosts.Where(c => c.StateCode == s.Code).OrderByDescending(c => c.Year).FirstOrDefault(),
            })
            .Select(x => new StateLivingCostDto(
                x.Code, x.Name, x.Region,
                x.Latest == null ? null : x.Latest.IndexAllItems,
                x.Latest == null ? null : x.Latest.IndexHousingRents,
                x.Latest == null ? null : x.Latest.Year,
                x.Latest == null ? null : x.Latest.SourceUrl,
                x.Latest == null ? null : x.Latest.RetrievedAt))
            .ToListAsync(ct);
}
