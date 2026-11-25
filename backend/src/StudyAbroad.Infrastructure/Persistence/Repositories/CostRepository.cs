using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Costs;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

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
        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Role).FirstOrDefaultAsync(ct);

        // Phụ huynh: ưu tiên ngân sách phụ huynh lưu trong liên kết con; nếu chưa có thì lấy hồ sơ riêng hoặc của con
        if (role == UserRoles.Parent)
        {
            var links = await db.ParentStudentLinks.AsNoTracking()
                .Where(l => l.ParentUserId == userId && l.Status == ParentLinkStatuses.Active)
                .OrderByDescending(l => l.UpdatedAt)
                .Select(l => new { l.StudentUserId, l.ParentPreferenceJson })
                .ToListAsync(ct);

            foreach (var link in links)
            {
                var parentUsd = ParentPreferenceService.Read(link.ParentPreferenceJson)?.BudgetUsd;
                if (parentUsd is > 0) return new BudgetInput("parent", parentUsd.Value, false);
            }

            var ownParent = await ReadStudentProfileBudgetAsync(userId, "parent", ct);
            if (ownParent is not null) return ownParent;

            foreach (var link in links)
            {
                var childBudget = await ReadStudentProfileBudgetAsync(link.StudentUserId, "student", ct);
                if (childBudget is not null) return childBudget;
            }
            return null;
        }

        // Học sinh (hoặc con managed khi phụ huynh đang làm thay): ưu tiên ngân sách trong hồ sơ học sinh
        var studentBudget = await ReadStudentProfileBudgetAsync(userId, "student", ct);
        if (studentBudget is not null) return studentBudget;

        // Nếu học sinh chưa khai ngân sách nhưng phụ huynh đã liên kết có khai, dùng kèm nhãn "parent"
        var parentPrefs = await db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.StudentUserId == userId && l.Status == ParentLinkStatuses.Active && l.ParentPreferenceJson != null)
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => l.ParentPreferenceJson)
            .ToListAsync(ct);

        foreach (var json in parentPrefs)
        {
            var parentUsd = ParentPreferenceService.Read(json)?.BudgetUsd;
            if (parentUsd is > 0) return new BudgetInput("parent", parentUsd.Value, false);
        }
        return null;
    }

    private async Task<BudgetInput?> ReadStudentProfileBudgetAsync(Guid userId, string source, CancellationToken ct)
    {
        var p = await db.StudentProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.BudgetTier, x.BudgetMinUsd, x.BudgetMaxUsd }).FirstOrDefaultAsync(ct);
        if (p is null) return null;
        if (p.BudgetMaxUsd is { } max) return new BudgetInput(source, max, false);
        // Mức "trên 70.000 USD" lưu max null: có ngân sách nhưng không có trần
        if (p.BudgetTier == "tier_above_70k" || p.BudgetMinUsd is not null) return new BudgetInput(source, null, true);
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
