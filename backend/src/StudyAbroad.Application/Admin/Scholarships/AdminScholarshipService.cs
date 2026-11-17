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

        reason = Clean(reason);
        var errors = ValidateVisibilityReason(isActive, reason);
        if (errors.Count > 0) return AdminUpdateResult<AdminScholarshipDto>.Invalid(errors);

        if (scholarship.IsActive != isActive)
        {
            scholarship.IsActive = isActive;
            scholarship.LastEditedByUserId = actorUserId;
            scholarship.UpdatedAt = DateTime.UtcNow;
            repository.AddAudit(AuditEntry(actorUserId, isActive ? "scholarship.show" : "scholarship.hide", "scholarship", scholarship.Id,
                new { name = scholarship.Name, from = !isActive, to = isActive, reason }));
            await repository.SaveChangesAsync(ct);
        }
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
        };
        var errors = new Dictionary<string, string>();

        if (v.Name is null)
            errors["name"] = "Tên học bổng không được để trống.";
        else if (v.Name.Length > 256)
            errors["name"] = "Tên học bổng tối đa 256 ký tự.";

        if (v.Provider is { Length: > 256 })
            errors["provider"] = "Đơn vị cấp tối đa 256 ký tự.";
        if (v.UniversityId is { } universityId && await repository.GetUniversityNameAsync(universityId, ct) is null)
            errors["universityId"] = "Trường không tồn tại.";
        else if (v.UniversityId is null && v.Provider is null)
            errors["provider"] = "Cần chọn trường hoặc nhập đơn vị cấp học bổng.";

        if (!StudyLevels.IsValid(v.StudyLevel))
            errors["studyLevel"] = "Bậc học không hợp lệ.";
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
    }

    private async Task<AdminScholarshipDto> ToDtoAsync(Scholarship s, CancellationToken ct) => new(
        s.Id, s.UniversityId, await repository.GetUniversityNameAsync(s.UniversityId, ct), s.Name, s.Provider, s.StudyLevel,
        s.AmountUsd, s.CoverageType, s.MinGpa4, s.MinIelts, s.Deadline, s.EligibilityNotes, s.SourceUrl, s.RetrievedAt,
        s.SourceReliability, s.IsActive, s.UpdatedAt, await repository.GetUserDisplayNameAsync(s.LastEditedByUserId, ct));
}
