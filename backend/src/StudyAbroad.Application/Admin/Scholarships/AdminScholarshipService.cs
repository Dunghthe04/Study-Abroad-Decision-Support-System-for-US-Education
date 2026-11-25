using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using static StudyAbroad.Application.Admin.AdminEditHelpers;

namespace StudyAbroad.Application.Admin.Scholarships;

/// <summary>[UC-109] Moderator thêm, sửa, ẩn/hiện học bổng; Admin chỉ xem.</summary>
public interface IAdminScholarshipService
{
    Task<PagedResult<AdminScholarshipDto>> SearchAsync(AdminScholarshipQuery query, CancellationToken ct = default);
    Task<AdminScholarshipDto?> GetAsync(Guid scholarshipId, CancellationToken ct = default);
    Task<AdminUpdateResult<AdminScholarshipDto>> CreateAsync(SaveScholarshipRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<AdminUpdateResult<AdminScholarshipDto>> UpdateAsync(
        Guid scholarshipId, SaveScholarshipRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<AdminUpdateResult<AdminScholarshipDto>> SetVisibilityAsync(
        Guid scholarshipId, bool isActive, string? reason, Guid actorUserId, CancellationToken ct = default);
}

public class AdminScholarshipService(IAdminScholarshipRepository repository) : IAdminScholarshipService
{
    private const int MaxPageSize = 100;
    private const decimal MaxAmountUsd = 9_999_999_999.99m;
    private static readonly string[] CoverageTypes = ["full", "partial", "tuition", "stipend"];
    // UC-103: học bổng do trường cấp, không nhận loại external
    private static readonly string[] Kinds = ["merit", "need", "merit_and_need", "athletic", "program", "other"];
    private static readonly string[] AmountTypes = ["fixed", "range", "percent_tuition", "full_tuition", "full_ride", "varies"];
    private static readonly string[] ApplicationMethods = ["automatic", "separate_application", "nomination", "unknown"];

    public Task<PagedResult<AdminScholarshipDto>> SearchAsync(AdminScholarshipQuery query, CancellationToken ct = default) =>
        repository.SearchAsync(query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        }, ct);

    public async Task<AdminScholarshipDto?> GetAsync(Guid scholarshipId, CancellationToken ct = default)
    {
        var scholarship = await repository.GetAsync(scholarshipId, ct);
        return scholarship is null ? null : await ToDtoAsync(scholarship, ct);
    }

    public async Task<AdminUpdateResult<AdminScholarshipDto>> CreateAsync(
        SaveScholarshipRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var (values, errors) = await ValidateAsync(request, ct);
        if (errors.Count > 0) return AdminUpdateResult<AdminScholarshipDto>.Invalid(errors);

        var scholarship = new Scholarship { IsActive = true, LastEditedByUserId = actorUserId };
        Apply(scholarship, values);
        repository.Add(scholarship);
        repository.AddAudit(AuditEntry(actorUserId, "scholarship.create", "scholarship", scholarship.Id,
            new { universityId = scholarship.UniversityId, name = scholarship.Name, changes = Diff(null, values) }));
        await repository.SaveChangesAsync(ct);
        return AdminUpdateResult<AdminScholarshipDto>.Ok(await ToDtoAsync(scholarship, ct));
    }

    public async Task<AdminUpdateResult<AdminScholarshipDto>> UpdateAsync(
        Guid scholarshipId, SaveScholarshipRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var scholarship = await repository.GetAsync(scholarshipId, ct);
        if (scholarship is null) return AdminUpdateResult<AdminScholarshipDto>.Missing();

        var (v, errors) = await ValidateAsync(request, ct);
        if (errors.Count > 0) return AdminUpdateResult<AdminScholarshipDto>.Invalid(errors);

        var changes = Diff(scholarship, v);

        if (changes.Count > 0)
        {
            Apply(scholarship, v);
            scholarship.LastEditedByUserId = actorUserId;
            scholarship.UpdatedAt = DateTime.UtcNow;
            repository.AddAudit(AuditEntry(actorUserId, "scholarship.update", "scholarship", scholarship.Id,
                new { universityId = scholarship.UniversityId, name = scholarship.Name, changes }));
            await repository.SaveChangesAsync(ct);
        }
        return AdminUpdateResult<AdminScholarshipDto>.Ok(await ToDtoAsync(scholarship, ct));
    }

    public async Task<AdminUpdateResult<AdminScholarshipDto>> SetVisibilityAsync(
        Guid scholarshipId, bool isActive, string? reason, Guid actorUserId, CancellationToken ct = default)
    {
        var scholarship = await repository.GetAsync(scholarshipId, ct);
        if (scholarship is null) return AdminUpdateResult<AdminScholarshipDto>.Missing();

        if (scholarship.IsActive == isActive)
            return AdminUpdateResult<AdminScholarshipDto>.Ok(await ToDtoAsync(scholarship, ct));

        reason = Clean(reason);
        var errors = ValidateVisibilityReason(isActive, reason);
        if (errors.Count > 0) return AdminUpdateResult<AdminScholarshipDto>.Invalid(errors);

        scholarship.IsActive = isActive;
        scholarship.LastEditedByUserId = actorUserId;
        scholarship.UpdatedAt = DateTime.UtcNow;
        repository.AddAudit(AuditEntry(actorUserId, isActive ? "scholarship.show" : "scholarship.hide", "scholarship", scholarship.Id,
            new { name = scholarship.Name, from = !isActive, to = isActive, reason }));
        await repository.SaveChangesAsync(ct);
        return AdminUpdateResult<AdminScholarshipDto>.Ok(await ToDtoAsync(scholarship, ct));
    }

    private async Task<(SaveScholarshipRequest Values, Dictionary<string, string> Errors)> ValidateAsync(
        SaveScholarshipRequest request, CancellationToken ct)
    {
        var v = request with
        {
            Name = Clean(request.Name),
            Provider = Clean(request.Provider),
            StudyLevel = Clean(request.StudyLevel)?.ToLowerInvariant(),
            CoverageType = Clean(request.CoverageType)?.ToLowerInvariant(),
            EligibilityNotes = Clean(request.EligibilityNotes),
            SourceUrl = Clean(request.SourceUrl),
            SourceReliability = Clean(request.SourceReliability)?.ToLowerInvariant(),
            Kind = Clean(request.Kind)?.ToLowerInvariant(),
            AmountType = Clean(request.AmountType)?.ToLowerInvariant(),
            RenewalCriteria = Clean(request.RenewalCriteria),
            MajorRestriction = Clean(request.MajorRestriction),
            ApplicationMethod = Clean(request.ApplicationMethod)?.ToLowerInvariant(),
            ApplyUrl = Clean(request.ApplyUrl),
        };
        var errors = new Dictionary<string, string>();

        if (v.Name is null)
            errors["name"] = "Tên học bổng không được để trống.";
        else if (v.Name.Length > 256)
            errors["name"] = "Tên học bổng tối đa 256 ký tự.";

        if (v.Provider is { Length: > 256 })
            errors["provider"] = "Đơn vị cấp tối đa 256 ký tự.";
        // UC-103: học bổng bắt buộc gắn với một trường và chỉ dành cho bậc undergraduate
        if (v.UniversityId is null)
            errors["universityId"] = "Học bổng bắt buộc phải gắn với một trường.";
        else if (await repository.GetUniversityNameAsync(v.UniversityId.Value, ct) is null)
            errors["universityId"] = "Trường không tồn tại.";

        if (v.StudyLevel != StudyLevels.Undergraduate)
            errors["studyLevel"] = "Chỉ hỗ trợ học bổng bậc đại học (undergraduate).";
        if (v.AmountUsd is < 0 or > MaxAmountUsd)
            errors["amountUsd"] = "Giá trị học bổng phải từ 0 đến 9.999.999.999,99 USD.";
        if (v.CoverageType is not null && !CoverageTypes.Contains(v.CoverageType))
            errors["coverageType"] = "Loại học bổng chỉ nhận full, partial, tuition hoặc stipend.";
        if (v.MinGpa4 is < 0 or > 4)
            errors["minGpa4"] = "GPA tối thiểu phải trong thang 0–4.";
        if (v.MinIelts is < 0 or > 9)
            errors["minIelts"] = "IELTS phải trong khoảng 0–9.";
        if (v.EligibilityNotes is { Length: > 2000 })
            errors["eligibilityNotes"] = "Điều kiện tối đa 2000 ký tự.";

        // [USAS-2067] Cột bộ dữ liệu học bổng; null = chưa rõ.
        if (v.Kind is not null && !Kinds.Contains(v.Kind))
            errors["kind"] = "Loại học bổng chỉ nhận merit, need, merit_and_need, athletic, program hoặc other.";
        if (v.AmountType is not null && !AmountTypes.Contains(v.AmountType))
            errors["amountType"] = "Kiểu giá trị chỉ nhận fixed, range, percent_tuition, full_tuition, full_ride hoặc varies.";
        if (v.AmountMinUsd is < 0 or > MaxAmountUsd)
            errors["amountMinUsd"] = "Giá trị tối thiểu phải từ 0 đến 9.999.999.999,99 USD.";
        if (v.AmountMaxUsd is < 0 or > MaxAmountUsd)
            errors["amountMaxUsd"] = "Giá trị tối đa phải từ 0 đến 9.999.999.999,99 USD.";
        else if (v.AmountMinUsd is { } min && v.AmountMaxUsd is { } max && min > max)
            errors["amountMaxUsd"] = "Giá trị tối đa không được nhỏ hơn giá trị tối thiểu.";
        if (v.DurationYears is < 1 or > 8)
            errors["durationYears"] = "Thời hạn phải trong khoảng 1–8 năm.";
        if (v.RenewalCriteria is { Length: > 1000 })
            errors["renewalCriteria"] = "Điều kiện gia hạn tối đa 1000 ký tự.";
        if (v.MinSat is < 400 or > 1600)
            errors["minSat"] = "SAT tối thiểu phải trong khoảng 400–1600.";
        if (v.MinAct is < 1 or > 36)
            errors["minAct"] = "ACT tối thiểu phải trong khoảng 1–36.";
        if (v.MinToefl is < 0 or > 120)
            errors["minToefl"] = "TOEFL tối thiểu phải trong khoảng 0–120.";
        if (v.MinDuolingo is < 10 or > 160)
            errors["minDuolingo"] = "Duolingo tối thiểu phải trong khoảng 10–160.";
        if (v.MajorRestriction is { Length: > 500 })
            errors["majorRestriction"] = "Giới hạn ngành tối đa 500 ký tự.";
        if (v.ApplicationMethod is not null && !ApplicationMethods.Contains(v.ApplicationMethod))
            errors["applicationMethod"] = "Cách nộp chỉ nhận automatic, separate_application, nomination hoặc unknown.";
        if (v.ApplyUrl is not null && (v.ApplyUrl.Length > 512 || !IsHttpUrl(v.ApplyUrl)))
            errors["applyUrl"] = "Link nộp phải là đường dẫn http(s), tối đa 512 ký tự.";

        ValidateSource(v.SourceUrl, v.RetrievedAt, v.SourceReliability, errors);
        return (v, errors);
    }

    private static Dictionary<string, object?[]> Diff(Scholarship? s, SaveScholarshipRequest v)
    {
        var changes = new Dictionary<string, object?[]>();
        AddChange(changes, "universityId", s?.UniversityId, v.UniversityId);
        AddChange(changes, "name", s?.Name, v.Name);
        AddChange(changes, "provider", s?.Provider, v.Provider);
        AddChange(changes, "studyLevel", s?.StudyLevel, v.StudyLevel);
        AddChange(changes, "amountUsd", s?.AmountUsd, v.AmountUsd);
        AddChange(changes, "coverageType", s?.CoverageType, v.CoverageType);
        AddChange(changes, "minGpa4", s?.MinGpa4, v.MinGpa4);
        AddChange(changes, "minIelts", s?.MinIelts, v.MinIelts);
        AddChange(changes, "deadline", s?.Deadline, v.Deadline);
        AddChange(changes, "eligibilityNotes", s?.EligibilityNotes, v.EligibilityNotes);
        AddChange(changes, "sourceUrl", s?.SourceUrl, v.SourceUrl);
        AddChange(changes, "retrievedAt", s?.RetrievedAt, v.RetrievedAt);
        AddChange(changes, "sourceReliability", s?.SourceReliability, v.SourceReliability);
        AddChange(changes, "kind", s?.Kind, v.Kind);
        AddChange(changes, "amountType", s?.AmountType, v.AmountType);
        AddChange(changes, "amountMinUsd", s?.AmountMinUsd, v.AmountMinUsd);
        AddChange(changes, "amountMaxUsd", s?.AmountMaxUsd, v.AmountMaxUsd);
        AddChange(changes, "durationYears", s?.DurationYears, v.DurationYears);
        AddChange(changes, "renewable", s?.Renewable, v.Renewable);
        AddChange(changes, "renewalCriteria", s?.RenewalCriteria, v.RenewalCriteria);
        AddChange(changes, "intlEligible", s?.IntlEligible, v.IntlEligible);
        AddChange(changes, "vnEligible", s?.VnEligible, v.VnEligible);
        AddChange(changes, "needBased", s?.NeedBased, v.NeedBased);
        AddChange(changes, "meritBased", s?.MeritBased, v.MeritBased);
        AddChange(changes, "minSat", s?.MinSat, v.MinSat);
        AddChange(changes, "minAct", s?.MinAct, v.MinAct);
        AddChange(changes, "minToefl", s?.MinToefl, v.MinToefl);
        AddChange(changes, "minDuolingo", s?.MinDuolingo, v.MinDuolingo);
        AddChange(changes, "majorRestriction", s?.MajorRestriction, v.MajorRestriction);
        AddChange(changes, "applicationMethod", s?.ApplicationMethod, v.ApplicationMethod);
        AddChange(changes, "applyUrl", s?.ApplyUrl, v.ApplyUrl);
        return changes;
    }

    private static void Apply(Scholarship s, SaveScholarshipRequest v)
    {
        s.UniversityId = v.UniversityId;
        s.Name = v.Name!;
        s.Provider = v.Provider;
        s.StudyLevel = v.StudyLevel!;
        s.AmountUsd = v.AmountUsd;
        s.CoverageType = v.CoverageType;
        s.MinGpa4 = v.MinGpa4;
        s.MinIelts = v.MinIelts;
        s.Deadline = v.Deadline;
        s.EligibilityNotes = v.EligibilityNotes;
        s.SourceUrl = v.SourceUrl;
        s.RetrievedAt = v.RetrievedAt;
        s.SourceReliability = v.SourceReliability;
        s.Kind = v.Kind;
        s.AmountType = v.AmountType;
        s.AmountMinUsd = v.AmountMinUsd;
        s.AmountMaxUsd = v.AmountMaxUsd;
        s.DurationYears = v.DurationYears;
        s.Renewable = v.Renewable;
        s.RenewalCriteria = v.RenewalCriteria;
        s.IntlEligible = v.IntlEligible;
        s.VnEligible = v.VnEligible;
        s.NeedBased = v.NeedBased;
        s.MeritBased = v.MeritBased;
        s.MinSat = v.MinSat;
        s.MinAct = v.MinAct;
        s.MinToefl = v.MinToefl;
        s.MinDuolingo = v.MinDuolingo;
        s.MajorRestriction = v.MajorRestriction;
        s.ApplicationMethod = v.ApplicationMethod;
        s.ApplyUrl = v.ApplyUrl;
    }

    private async Task<AdminScholarshipDto> ToDtoAsync(Scholarship s, CancellationToken ct) => new(
        s.Id, s.UniversityId, await repository.GetUniversityNameAsync(s.UniversityId, ct), s.Name, s.Provider, s.StudyLevel,
        s.AmountUsd, s.CoverageType, s.MinGpa4, s.MinIelts, s.Deadline, s.EligibilityNotes, s.SourceUrl, s.RetrievedAt,
        s.SourceReliability, s.IsActive, s.UpdatedAt, await repository.GetUserDisplayNameAsync(s.LastEditedByUserId, ct),
        s.Kind, s.AmountType, s.AmountMinUsd, s.AmountMaxUsd, s.DurationYears, s.Renewable, s.RenewalCriteria,
        s.IntlEligible, s.VnEligible, s.NeedBased, s.MeritBased, s.MinSat, s.MinAct, s.MinToefl,
        s.MajorRestriction, s.ApplicationMethod, s.ApplyUrl, s.MinDuolingo);
}
