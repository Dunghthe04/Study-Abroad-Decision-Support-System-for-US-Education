using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using static StudyAbroad.Application.Admin.AdminEditHelpers;

namespace StudyAbroad.Application.Admin.Universities;

public interface IAdminUniversityService
{
    /// <summary>[UC-65] Danh sách trường cho admin/moderator — tìm, lọc, phân trang.</summary>
    Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default);
    Task<AdminUniversityDetailDto?> GetAsync(Guid universityId, CancellationToken ct = default);
    /// <summary>null nếu trường không tồn tại.</summary>
    Task<IReadOnlyList<AdminUniversityOfferingDto>?> GetOfferingsAsync(Guid universityId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminMajorOptionDto>> ListMajorsAsync(CancellationToken ct = default);
    Task<AdminUpdateResult<AdminUniversityDetailDto>> CreateUniversityAsync(
        CreateUniversityRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-66] Sửa thông tin trường (kèm ghi audit).</summary>
    Task<AdminUpdateResult<AdminUniversityDetailDto>> UpdateUniversityAsync(
        Guid universityId, UpdateUniversityRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-65] Thêm một bậc học trống cho trường; số liệu nhập tiếp qua UpdateOfferingAsync.</summary>
    Task<AdminUpdateResult<AdminUniversityOfferingDto>> CreateOfferingAsync(
        Guid universityId, CreateOfferingRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-66] Sửa một bậc học của trường, gồm cả danh sách ngành (kèm ghi audit).</summary>
    Task<AdminUpdateResult<AdminUniversityOfferingDto>> UpdateOfferingAsync(
        Guid universityId, Guid offeringId, UpdateOfferingRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-67] Ẩn trường khỏi tìm kiếm và gợi ý, hoặc hiện lại (kèm ghi audit).</summary>
    /// <summary>Ẩn bắt buộc có lý do; lý do được ghi vào audit.</summary>
    Task<AdminUpdateResult<AdminUniversityDetailDto>> SetVisibilityAsync(
        Guid universityId, bool isActive, string? reason, Guid actorUserId, CancellationToken ct = default);
}

public class AdminUniversityService(IAdminUniversityRepository repository) : IAdminUniversityService
{
    private const int MaxPageSize = 100;
    private static readonly string[] Controls = ["public", "private"];
    private static readonly string[] SatPolicies = ["required", "optional", "not_accepted"];
    private static readonly string[] Importances = ["very_important", "important", "considered", "not_considered"];
    private static readonly string[] GpaScaleTypes = ["weighted", "unweighted", "both"];
    private static readonly string[] AidPolicies = ["need_and_merit", "merit_only", "need_only", "none"];
    private const string DuplicateLevelMessage = "Trường đã có bậc học này.";
    private static readonly Regex StateCode = new("^[A-Z]{2}$");
    private static readonly Dictionary<string, string> AllowedDeadlineRounds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ED"] = "ED",
        ["ED2"] = "ED2",
        ["EA"] = "EA",
        ["REA"] = "REA",
        ["SCEA"] = "SCEA",
        ["RD"] = "RD",
        ["Rolling"] = "Rolling",
    };
    private static readonly HashSet<string> EarlyDeadlineRounds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ED", "ED2", "EA", "REA", "SCEA",
    };

    public Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };
        return repository.SearchAsync(normalized, ct);
    }

    public async Task<AdminUniversityDetailDto?> GetAsync(Guid universityId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return null;
        return await ToDetailDtoAsync(university, ct);
    }

    public async Task<IReadOnlyList<AdminUniversityOfferingDto>?> GetOfferingsAsync(Guid universityId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return null;

        var offerings = await repository.ListOfferingsAsync(universityId, ct);
        var offeringIds = offerings.Select(o => o.Id).ToList();
        var majorRows = await repository.ListMajorRowsAsync(offeringIds, ct);

        var result = new List<AdminUniversityOfferingDto>();
        foreach (var offering in offerings)
        {
            var rowsOfOffering = majorRows.Where(r => r.UniversityOfferingId == offering.Id).ToList();
            var lastEditedBy = await repository.GetUserDisplayNameAsync(offering.LastEditedByUserId, ct);
            result.Add(ToOfferingDto(offering, rowsOfOffering, lastEditedBy));
        }
        return result;
    }

    public async Task<IReadOnlyList<AdminMajorOptionDto>> ListMajorsAsync(CancellationToken ct = default)
    {
        var majors = await repository.ListMajorsAsync(ct);
        return majors.Select(m => new AdminMajorOptionDto(m.Id, m.Name, m.CipCode)).ToList();
    }

    public async Task<AdminUpdateResult<AdminUniversityDetailDto>> CreateUniversityAsync(
        CreateUniversityRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // UC-61 E3: không tự chuyển chữ thường thành chữ hoa; chữ thường phải báo lỗi
        var code = Clean(request.Code);
        var name = Clean(request.Name);
        var city = Clean(request.City);
        var state = Clean(request.State)?.ToUpperInvariant();
        var website = Clean(request.Website);
        var control = Clean(request.Control)?.ToLowerInvariant();
        var logoUrl = Clean(request.LogoUrl);
        var coverUrl = Clean(request.CoverUrl);

        var errors = new Dictionary<string, string>();
        if (code is null)
            errors["code"] = "Mã trường không được để trống.";
        else if (code.Length > 32)
            errors["code"] = "Mã trường tối đa 32 ký tự.";
        else if (!Regex.IsMatch(code, "^[A-Z0-9.-]+$"))
            errors["code"] = "Mã trường chỉ được chứa chữ hoa, số, dấu gạch ngang và dấu chấm.";
        else if (await repository.ExistsCodeAsync(code, ct))
            errors["code"] = "Mã trường đã tồn tại trong hệ thống.";

        ValidateUniversityFields(name, city, state, website, control, request.AcceptanceRate, request.InternationalStudentCount, logoUrl, coverUrl, errors);
        if (errors.Count == 0 && !request.ConfirmDuplicateName && await repository.ExistsNameAsync(name!, ct: ct))
            errors["duplicateName"] = $"Đã có trường tên \"{name}\". Kiểm tra lại, hoặc xác nhận để vẫn tạo.";
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityDetailDto>.Invalid(errors);

        var university = new University
        {
            Code = code!,
            Name = name!,
            City = city,
            State = state,
            Website = website,
            Control = control,
            AcceptanceRate = request.AcceptanceRate,
            InternationalStudentCount = request.InternationalStudentCount,
            IsIvy = request.IsIvy,
            IsActive = true,
            LastEditedByUserId = actorUserId,
        };

        repository.AddUniversity(university);
        if (!string.IsNullOrWhiteSpace(logoUrl) || !string.IsNullOrWhiteSpace(coverUrl))
        {
            await repository.SaveUniversityMediaAsync(university.Id, logoUrl, coverUrl, ct);
        }
        repository.AddAudit(AuditEntry(actorUserId, "university.create", "university", university.Id, new
            {
                code = university.Code, name = university.Name, city, state, website, control,
                acceptanceRate = request.AcceptanceRate, internationalStudentCount = request.InternationalStudentCount,
                isIvy = request.IsIvy, logoUrl, coverUrl,
            }));
        await repository.SaveChangesAsync(ct);
        return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));
    }

    public async Task<AdminUpdateResult<AdminUniversityDetailDto>> UpdateUniversityAsync(
        Guid universityId, UpdateUniversityRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return AdminUpdateResult<AdminUniversityDetailDto>.Missing();

        var name = Clean(request.Name);
        var city = Clean(request.City);
        var state = Clean(request.State)?.ToUpperInvariant();
        var website = Clean(request.Website);
        var control = Clean(request.Control)?.ToLowerInvariant();
        var logoUrl = Clean(request.LogoUrl);
        var coverUrl = Clean(request.CoverUrl);

        var errors = new Dictionary<string, string>();
        ValidateUniversityFields(name, city, state, website, control, request.AcceptanceRate, request.InternationalStudentCount, logoUrl, coverUrl, errors);
        if (errors.Count == 0 && !request.ConfirmDuplicateName && name != university.Name
            && await repository.ExistsNameAsync(name!, university.Id, ct))
            errors["duplicateName"] = $"Đã có trường tên \"{name}\". Kiểm tra lại, hoặc bấm Lưu lần nữa để xác nhận đổi tên.";
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityDetailDto>.Invalid(errors);

        var currentMedia = await repository.GetUniversityMediaAsync(university.Id, ct);
        var currentLogo = currentMedia.FirstOrDefault(m => m.Type == "logo")?.Url;
        var currentCover = currentMedia.FirstOrDefault(m => m.Type == "cover")?.Url;

        var changes = new Dictionary<string, object?[]>();
        AddChange(changes, "name", university.Name, name);
        AddChange(changes, "city", university.City, city);
        AddChange(changes, "state", university.State, state);
        AddChange(changes, "website", university.Website, website);
        AddChange(changes, "control", university.Control, control);
        AddChange(changes, "acceptanceRate", university.AcceptanceRate, request.AcceptanceRate);
        AddChange(changes, "internationalStudentCount", university.InternationalStudentCount, request.InternationalStudentCount);
        AddChange(changes, "isIvy", university.IsIvy, request.IsIvy);
        AddChange(changes, "logoUrl", currentLogo, logoUrl);
        AddChange(changes, "coverUrl", currentCover, coverUrl);

        if (changes.Count == 0) return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));

        university.Name = name!;
        university.City = city;
        university.State = state;
        university.Website = website;
        university.Control = control;
        university.AcceptanceRate = request.AcceptanceRate;
        university.InternationalStudentCount = request.InternationalStudentCount;
        university.IsIvy = request.IsIvy;
        university.LastEditedByUserId = actorUserId;

        await repository.SaveUniversityMediaAsync(university.Id, logoUrl, coverUrl, ct);
        repository.AddAudit(AuditEntry(actorUserId, "university.update", "university", university.Id, new { code = university.Code, changes }));
        await repository.SaveChangesAsync(ct);
        return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));
    }

    public async Task<AdminUpdateResult<AdminUniversityOfferingDto>> CreateOfferingAsync(
        Guid universityId, CreateOfferingRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        if (await repository.GetUniversityAsync(universityId, ct) is null)
            return AdminUpdateResult<AdminUniversityOfferingDto>.Missing();

        var studyLevel = Clean(request.StudyLevel)?.ToLowerInvariant();
        var errors = new Dictionary<string, string>();
        // UC-61: chỉ cho phép thêm bậc đại học (undergraduate)
        if (studyLevel != StudyLevels.Undergraduate)
            errors["studyLevel"] = "Chỉ hỗ trợ thêm bậc học đại học (undergraduate).";
        else if ((await repository.ListOfferingsAsync(universityId, ct)).Any(o => o.StudyLevel == studyLevel))
            errors["studyLevel"] = DuplicateLevelMessage;
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityOfferingDto>.Invalid(errors);

        var offering = new UniversityOffering
        {
            UniversityId = universityId,
            StudyLevel = studyLevel!,
            LastEditedByUserId = actorUserId,
        };
        repository.AddOffering(offering);
        repository.AddAudit(AuditEntry(actorUserId, "university_offering.create", "university_offering", offering.Id,
            new { universityId, studyLevel }));
        if (!await repository.TrySaveAsync(ct))
            return AdminUpdateResult<AdminUniversityOfferingDto>.Invalid(
                new Dictionary<string, string> { ["studyLevel"] = DuplicateLevelMessage });

        var lastEditedBy = await repository.GetUserDisplayNameAsync(actorUserId, ct);
        return AdminUpdateResult<AdminUniversityOfferingDto>.Ok(ToOfferingDto(offering, [], lastEditedBy));
    }

    public async Task<AdminUpdateResult<AdminUniversityOfferingDto>> UpdateOfferingAsync(
        Guid universityId, Guid offeringId, UpdateOfferingRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var offering = await repository.GetOfferingAsync(universityId, offeringId, ct);
        if (offering is null) return AdminUpdateResult<AdminUniversityOfferingDto>.Missing();

        var academicYear = Clean(request.AcademicYear);
        var satPolicy = Clean(request.SatPolicy)?.ToLowerInvariant();
        var actPolicy = Clean(request.ActPolicy)?.ToLowerInvariant();
        var sourceUrl = Clean(request.SourceUrl);
        var sourceReliability = Clean(request.SourceReliability)?.ToLowerInvariant();
        var notes = Clean(request.Notes);
        var requestedMajors = request.Majors;
        var gpaScaleType = Clean(request.GpaScaleType);
        var c7GpaImportance = Clean(request.C7GpaImportance);
        var c7TestImportance = Clean(request.C7TestImportance);
        var c7EcImportance = Clean(request.C7EcImportance);
        var intlAidPolicy = Clean(request.IntlAidPolicy);
        var campusSetting = Clean(request.CampusSetting);
        var housingTypes = Clean(request.HousingTypes);
        var activities = Clean(request.Activities);
        var rawDeadlinesJson = Clean(request.DeadlinesJson);

        var errors = ValidateOffering(request, academicYear, satPolicy, actPolicy, notes,
            c7GpaImportance, c7TestImportance, c7EcImportance, gpaScaleType, intlAidPolicy, campusSetting);
        var normalizedDeadlinesJson = ValidateAndNormalizeDeadlines(rawDeadlinesJson, errors);
        ValidateSource(sourceUrl, request.RetrievedAt, sourceReliability, errors);
        var knownMajors = await repository.ListMajorsAsync(ct);
        var knownMajorIds = knownMajors.Select(m => m.Id).ToHashSet();
        var seenMajorIds = new HashSet<Guid>();
        foreach (var major in requestedMajors ?? [])
        {
            if (!knownMajorIds.Contains(major.MajorId))
                errors["majors"] = "Có ngành không nằm trong danh mục ngành.";
            else if (!seenMajorIds.Add(major.MajorId))
                errors["majors"] = "Một ngành bị chọn hai lần.";
        }
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityOfferingDto>.Invalid(errors);

        var changes = new Dictionary<string, object?[]>();
        AddChange(changes, "academicYear", offering.AcademicYear, academicYear);
        AddChange(changes, "tuitionUsd", offering.TuitionUsd, request.TuitionUsd);
        AddChange(changes, "livingUsd", offering.LivingUsd, request.LivingUsd);
        AddChange(changes, "feesUsd", offering.FeesUsd, request.FeesUsd);
        AddChange(changes, "otherExpensesUsd", offering.OtherExpensesUsd, request.OtherExpensesUsd);
        AddChange(changes, "healthInsuranceUsd", offering.HealthInsuranceUsd, request.HealthInsuranceUsd);
        AddChange(changes, "applicationFeeUsd", offering.ApplicationFeeUsd, request.ApplicationFeeUsd);
        AddChange(changes, "tuitionInStateUsd", offering.TuitionInStateUsd, request.TuitionInStateUsd);
        AddChange(changes, "minGpa4", offering.MinGpa4, request.MinGpa4);
        AddChange(changes, "minIelts", offering.MinIelts, request.MinIelts);
        AddChange(changes, "minToefl", offering.MinToefl, request.MinToefl);
        AddChange(changes, "minDuolingo", offering.MinDuolingo, request.MinDuolingo);
        AddChange(changes, "avgGpa4", offering.AvgGpa4, request.AvgGpa4);
        AddChange(changes, "sat25", offering.Sat25, request.Sat25);
        AddChange(changes, "sat75", offering.Sat75, request.Sat75);
        AddChange(changes, "act25", offering.Act25, request.Act25);
        AddChange(changes, "act75", offering.Act75, request.Act75);
        AddChange(changes, "satPolicy", offering.SatPolicy, satPolicy);
        AddChange(changes, "actPolicy", offering.ActPolicy, actPolicy);
        AddChange(changes, "actSubmitPct", offering.ActSubmitPct, request.ActSubmitPct);
        AddChange(changes, "sourceUrl", offering.SourceUrl, sourceUrl);
        AddChange(changes, "retrievedAt", offering.RetrievedAt, request.RetrievedAt);
        AddChange(changes, "sourceReliability", offering.SourceReliability, sourceReliability);
        AddChange(changes, "notes", offering.Notes, notes);
        AddChange(changes, "applicantsTotal", offering.ApplicantsTotal, request.ApplicantsTotal);
        AddChange(changes, "admittedTotal", offering.AdmittedTotal, request.AdmittedTotal);
        AddChange(changes, "intlApplicants", offering.IntlApplicants, request.IntlApplicants);
        AddChange(changes, "intlAdmitted", offering.IntlAdmitted, request.IntlAdmitted);
        AddChange(changes, "intlAcceptanceRate", offering.IntlAcceptanceRate, request.IntlAcceptanceRate);
        AddChange(changes, "satSubmitPct", offering.SatSubmitPct, request.SatSubmitPct);
        AddChange(changes, "gpaSubmitPct", offering.GpaSubmitPct, request.GpaSubmitPct);
        AddChange(changes, "gpaPct40", offering.GpaPct40, request.GpaPct40);
        AddChange(changes, "gpaPct375To399", offering.GpaPct375To399, request.GpaPct375To399);
        AddChange(changes, "gpaPct350To374", offering.GpaPct350To374, request.GpaPct350To374);
        AddChange(changes, "gpaPct325To349", offering.GpaPct325To349, request.GpaPct325To349);
        AddChange(changes, "gpaPct300To324", offering.GpaPct300To324, request.GpaPct300To324);
        AddChange(changes, "gpaPct250To299", offering.GpaPct250To299, request.GpaPct250To299);
        AddChange(changes, "gpaPctBelow250", offering.GpaPctBelow250, request.GpaPctBelow250);
        AddChange(changes, "avgGpaWeighted", offering.AvgGpaWeighted, request.AvgGpaWeighted);
        AddChange(changes, "gpaScaleType", offering.GpaScaleType, gpaScaleType);
        AddChange(changes, "c7GpaImportance", offering.C7GpaImportance, c7GpaImportance);
        AddChange(changes, "c7TestImportance", offering.C7TestImportance, c7TestImportance);
        AddChange(changes, "c7EcImportance", offering.C7EcImportance, c7EcImportance);
        AddChange(changes, "retentionRate1Yr", offering.RetentionRate1Yr, request.RetentionRate1Yr);
        AddChange(changes, "gradRate6Yr", offering.GradRate6Yr, request.GradRate6Yr);
        AddChange(changes, "studentFacultyRatio", offering.StudentFacultyRatio, request.StudentFacultyRatio);
        AddChange(changes, "intlAidPolicy", offering.IntlAidPolicy, intlAidPolicy);
        AddChange(changes, "intlAidAvgUsd", offering.IntlAidAvgUsd, request.IntlAidAvgUsd);
        AddChange(changes, "intlAidRecipients", offering.IntlAidRecipients, request.IntlAidRecipients);
        AddChange(changes, "campusSetting", offering.CampusSetting, campusSetting);
        AddChange(changes, "fyOnCampusPct", offering.FyOnCampusPct, request.FyOnCampusPct);
        AddChange(changes, "ugOnCampusPct", offering.UgOnCampusPct, request.UgOnCampusPct);
        AddChange(changes, "housingTypes", offering.HousingTypes, housingTypes);
        AddChange(changes, "intlHousing", offering.IntlHousing, request.IntlHousing);
        AddChange(changes, "housingOnlyUsd", offering.HousingOnlyUsd, request.HousingOnlyUsd);
        AddChange(changes, "housingDepositUsd", offering.HousingDepositUsd, request.HousingDepositUsd);
        AddChange(changes, "activities", offering.Activities, activities);
        AddChange(changes, "deadlinesJson", offering.DeadlinesJson, normalizedDeadlinesJson);

        offering.AcademicYear = academicYear;
        offering.TuitionUsd = request.TuitionUsd;
        offering.LivingUsd = request.LivingUsd;
        offering.FeesUsd = request.FeesUsd;
        offering.OtherExpensesUsd = request.OtherExpensesUsd;
        offering.HealthInsuranceUsd = request.HealthInsuranceUsd;
        offering.ApplicationFeeUsd = request.ApplicationFeeUsd;
        offering.TuitionInStateUsd = request.TuitionInStateUsd;
        offering.MinGpa4 = request.MinGpa4;
        offering.MinIelts = request.MinIelts;
        offering.MinToefl = request.MinToefl;
        offering.MinDuolingo = request.MinDuolingo;
        offering.AvgGpa4 = request.AvgGpa4;
        offering.Sat25 = request.Sat25;
        offering.Sat75 = request.Sat75;
        offering.Act25 = request.Act25;
        offering.Act75 = request.Act75;
        offering.SatPolicy = satPolicy;
        offering.ActPolicy = actPolicy;
        offering.ActSubmitPct = request.ActSubmitPct;
        offering.SourceUrl = sourceUrl;
        offering.RetrievedAt = request.RetrievedAt;
        offering.SourceReliability = sourceReliability;
        offering.Notes = notes;
        offering.ApplicantsTotal = request.ApplicantsTotal;
        offering.AdmittedTotal = request.AdmittedTotal;
        offering.IntlApplicants = request.IntlApplicants;
        offering.IntlAdmitted = request.IntlAdmitted;
        offering.IntlAcceptanceRate = request.IntlAcceptanceRate;
        offering.SatSubmitPct = request.SatSubmitPct;
        offering.GpaSubmitPct = request.GpaSubmitPct;
        offering.GpaPct40 = request.GpaPct40;
        offering.GpaPct375To399 = request.GpaPct375To399;
        offering.GpaPct350To374 = request.GpaPct350To374;
        offering.GpaPct325To349 = request.GpaPct325To349;
        offering.GpaPct300To324 = request.GpaPct300To324;
        offering.GpaPct250To299 = request.GpaPct250To299;
        offering.GpaPctBelow250 = request.GpaPctBelow250;
        offering.AvgGpaWeighted = request.AvgGpaWeighted;
        offering.GpaScaleType = gpaScaleType;
        offering.C7GpaImportance = c7GpaImportance;
        offering.C7TestImportance = c7TestImportance;
        offering.C7EcImportance = c7EcImportance;
        offering.RetentionRate1Yr = request.RetentionRate1Yr;
        offering.GradRate6Yr = request.GradRate6Yr;
        offering.StudentFacultyRatio = request.StudentFacultyRatio;
        offering.IntlAidPolicy = intlAidPolicy;
        offering.IntlAidAvgUsd = request.IntlAidAvgUsd;
        offering.IntlAidRecipients = request.IntlAidRecipients;
        offering.CampusSetting = campusSetting;
        offering.FyOnCampusPct = request.FyOnCampusPct;
        offering.UgOnCampusPct = request.UgOnCampusPct;
        offering.HousingTypes = housingTypes;
        offering.IntlHousing = request.IntlHousing;
        offering.HousingOnlyUsd = request.HousingOnlyUsd;
        offering.HousingDepositUsd = request.HousingDepositUsd;
        offering.Activities = activities;
        offering.DeadlinesJson = normalizedDeadlinesJson;

        if (requestedMajors is not null)
        {
            var majorChanges = await ApplyMajorChangesAsync(offering.Id, requestedMajors, knownMajors, ct);
            if (majorChanges.Count > 0)
                changes["majors"] = [.. majorChanges];
        }

        if (changes.Count > 0)
        {
            offering.UpdatedAt = DateTime.UtcNow;
            offering.LastEditedByUserId = actorUserId;
            repository.AddAudit(AuditEntry(actorUserId, "university_offering.update", "university_offering", offering.Id,
                new { universityId, studyLevel = offering.StudyLevel, changes }));
            await repository.SaveChangesAsync(ct);
        }

        var majorRows = await repository.ListMajorRowsAsync([offering.Id], ct);
        var lastEditedBy = await repository.GetUserDisplayNameAsync(offering.LastEditedByUserId, ct);
        return AdminUpdateResult<AdminUniversityOfferingDto>.Ok(ToOfferingDto(offering, majorRows, lastEditedBy));
    }

    public async Task<AdminUpdateResult<AdminUniversityDetailDto>> SetVisibilityAsync(
        Guid universityId, bool isActive, string? reason, Guid actorUserId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return AdminUpdateResult<AdminUniversityDetailDto>.Missing();

        // Bấm lại cùng trạng thái là thao tác hợp lệ, không cần lý do và không ghi thêm audit.
        if (university.IsActive == isActive)
            return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));

        reason = Clean(reason);
        var errors = ValidateVisibilityReason(isActive, reason);
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityDetailDto>.Invalid(errors);

        university.IsActive = isActive;
        university.LastEditedByUserId = actorUserId;
        var action = isActive ? "university.show" : "university.hide";
        repository.AddAudit(AuditEntry(actorUserId, action, "university", university.Id,
            new { code = university.Code, from = !isActive, to = isActive, reason }));
        await repository.SaveChangesAsync(ct);
        return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));
    }

    private static void ValidateUniversityFields(
        string? name, string? city, string? state, string? website, string? control,
        decimal? acceptanceRate, int? internationalStudentCount, string? logoUrl, string? coverUrl,
        Dictionary<string, string> errors)
    {
        if (name is null)
            errors["name"] = "Tên trường không được để trống.";
        else if (name.Length > 256)
            errors["name"] = "Tên trường tối đa 256 ký tự.";
        if (city is not null && city.Length > 128)
            errors["city"] = "Thành phố tối đa 128 ký tự.";
        if (state is not null && !StateCode.IsMatch(state))
            errors["state"] = "Mã bang gồm 2 chữ cái, ví dụ CA.";
        if (website is not null && (website.Length > 512 || !IsHttpUrl(website)))
            errors["website"] = "Website phải là đường dẫn http(s), tối đa 512 ký tự.";
        if (logoUrl is not null && (logoUrl.Length > 1000 || !IsHttpUrl(logoUrl)))
            errors["logoUrl"] = "Logo phải là đường dẫn http(s), tối đa 1000 ký tự.";
        if (coverUrl is not null && (coverUrl.Length > 1000 || !IsHttpUrl(coverUrl)))
            errors["coverUrl"] = "Ảnh bìa phải là đường dẫn http(s), tối đa 1000 ký tự.";
        if (control is not null && !Controls.Contains(control))
            errors["control"] = "Loại trường chỉ nhận public hoặc private.";
        if (acceptanceRate is < 0 or > 1)
            errors["acceptanceRate"] = "Tỷ lệ nhận phải trong khoảng 0–1.";
        if (internationalStudentCount is < 0)
            errors["internationalStudentCount"] = "Số du học sinh không được âm.";
    }

    private static Dictionary<string, string> ValidateOffering(
        UpdateOfferingRequest request, string? academicYear, string? satPolicy, string? actPolicy, string? notes,
        string? c7GpaImportance, string? c7TestImportance, string? c7EcImportance, string? gpaScaleType,
        string? intlAidPolicy, string? campusSetting)
    {
        var errors = new Dictionary<string, string>();

        if (academicYear is not null && academicYear.Length > 16)
            errors["academicYear"] = "Năm học tối đa 16 ký tự, ví dụ 2026-27.";
        if (request.TuitionUsd is <= 0) errors["tuitionUsd"] = "Học phí phải lớn hơn 0.";
        if (request.LivingUsd is < 0) errors["livingUsd"] = "Chi phí sinh hoạt không được âm.";
        if (request.FeesUsd is < 0) errors["feesUsd"] = "Phí bắt buộc không được âm.";
        if (request.OtherExpensesUsd is < 0) errors["otherExpensesUsd"] = "Chi phí khác không được âm.";
        if (request.HealthInsuranceUsd is < 0) errors["healthInsuranceUsd"] = "Bảo hiểm y tế không được âm.";
        if (request.ApplicationFeeUsd is < 0) errors["applicationFeeUsd"] = "Phí nộp hồ sơ không được âm.";
        if (request.TuitionInStateUsd is < 0) errors["tuitionInStateUsd"] = "Học phí in-state không được âm.";
        if (request.ActSubmitPct is < 0 or > 1) errors["actSubmitPct"] = "Tỉ lệ nộp ACT phải trong khoảng 0–1.";

        if (request.MinGpa4 is < 0 or > 4) errors["minGpa4"] = "GPA tối thiểu phải trong thang 0–4.";
        if (request.AvgGpa4 is < 0 or > 4) errors["avgGpa4"] = "GPA trung bình phải trong thang 0–4.";
        if (request.MinIelts is < 0 or > 9) errors["minIelts"] = "IELTS phải trong khoảng 0–9.";
        if (request.MinToefl is < 0 or > 120) errors["minToefl"] = "TOEFL iBT phải trong khoảng 0–120.";
        if (request.MinDuolingo is < 10 or > 160) errors["minDuolingo"] = "Duolingo phải trong khoảng 10–160.";

        // SAT: công thức gợi ý cần đủ cả 2 mốc, nên nhập đủ cả cặp hoặc để trống cả cặp.
        var hasSat25 = request.Sat25 is not null;
        var hasSat75 = request.Sat75 is not null;
        if (hasSat25 != hasSat75)
            errors["sat25"] = "Nhập đủ cả SAT 25% và 75%, hoặc để trống cả hai.";
        else if (hasSat25 && (request.Sat25 < 400 || request.Sat75 > 1600 || request.Sat25 > request.Sat75))
            errors["sat25"] = "SAT phải trong khoảng 400–1600 và mốc 25% không lớn hơn mốc 75%.";

        if (request.Act25 is < 1 or > 36) errors["act25"] = "ACT phải trong khoảng 1–36.";
        if (request.Act75 is < 1 or > 36) errors["act75"] = "ACT phải trong khoảng 1–36.";
        if (request.Act25 is not null && request.Act75 is not null && request.Act25 > request.Act75)
            errors["act25"] = "ACT mốc 25% không được lớn hơn mốc 75%.";

        if (satPolicy is not null && !SatPolicies.Contains(satPolicy))
            errors["satPolicy"] = "Chính sách SAT chỉ nhận required, optional hoặc not_accepted.";
        if (actPolicy is not null && !SatPolicies.Contains(actPolicy))
            errors["actPolicy"] = "Chính sách ACT chỉ nhận required, optional hoặc not_accepted.";
        if (notes is not null && notes.Length > 2000)
            errors["notes"] = "Ghi chú tối đa 2000 ký tự.";

        // Tỉ lệ lưu dạng 0–1 (vd 0.651 = 65,1%)
        void Fraction(string key, string label, decimal? v)
        {
            if (v is < 0 or > 1) errors[key] = $"{label} phải trong khoảng 0–1.";
        }
        void NonNegative(string key, string label, decimal? v)
        {
            if (v is < 0) errors[key] = $"{label} không được âm.";
        }
        Fraction("intlAcceptanceRate", "Tỉ lệ nhận du học sinh", request.IntlAcceptanceRate);
        Fraction("satSubmitPct", "Tỉ lệ nộp SAT", request.SatSubmitPct);
        Fraction("gpaSubmitPct", "Tỉ lệ nộp GPA", request.GpaSubmitPct);
        Fraction("gpaPct40", "Tỉ lệ GPA 4.0", request.GpaPct40);
        Fraction("gpaPct375To399", "Tỉ lệ GPA 3.75–3.99", request.GpaPct375To399);
        Fraction("gpaPct350To374", "Tỉ lệ GPA 3.50–3.74", request.GpaPct350To374);
        Fraction("gpaPct325To349", "Tỉ lệ GPA 3.25–3.49", request.GpaPct325To349);
        Fraction("gpaPct300To324", "Tỉ lệ GPA 3.00–3.24", request.GpaPct300To324);
        Fraction("gpaPct250To299", "Tỉ lệ GPA 2.50–2.99", request.GpaPct250To299);
        Fraction("gpaPctBelow250", "Tỉ lệ GPA dưới 2.50", request.GpaPctBelow250);
        Fraction("retentionRate1Yr", "Tỉ lệ giữ chân năm 1", request.RetentionRate1Yr);
        Fraction("gradRate6Yr", "Tỉ lệ tốt nghiệp 6 năm", request.GradRate6Yr);
        Fraction("fyOnCampusPct", "Tỉ lệ năm nhất ở KTX", request.FyOnCampusPct);
        Fraction("ugOnCampusPct", "Tỉ lệ sinh viên đại học ở KTX", request.UgOnCampusPct);
        if (request.AvgGpaWeighted is < 0 or > 5) errors["avgGpaWeighted"] = "GPA có trọng số phải trong khoảng 0–5.";
        NonNegative("applicantsTotal", "Số hồ sơ nộp", request.ApplicantsTotal);
        NonNegative("admittedTotal", "Số hồ sơ đỗ", request.AdmittedTotal);
        NonNegative("intlApplicants", "Số hồ sơ du học sinh", request.IntlApplicants);
        NonNegative("intlAdmitted", "Số du học sinh đỗ", request.IntlAdmitted);
        NonNegative("intlAidAvgUsd", "Mức hỗ trợ trung bình", request.IntlAidAvgUsd);
        NonNegative("intlAidRecipients", "Số du học sinh được hỗ trợ", request.IntlAidRecipients);
        NonNegative("housingOnlyUsd", "Tiền phòng KTX", request.HousingOnlyUsd);
        NonNegative("housingDepositUsd", "Tiền cọc KTX", request.HousingDepositUsd);
        if (request.StudentFacultyRatio is < 1) errors["studentFacultyRatio"] = "Tỉ lệ SV/GV phải từ 1 trở lên.";
        if (request.AdmittedTotal > request.ApplicantsTotal) errors["admittedTotal"] = "Số hồ sơ đỗ không được lớn hơn số hồ sơ nộp.";
        if (request.IntlAdmitted > request.IntlApplicants) errors["intlAdmitted"] = "Số du học sinh đỗ không được lớn hơn số hồ sơ du học sinh.";

        foreach (var (key, label, value) in new[]
        {
            ("c7GpaImportance", "Mức coi trọng GPA", c7GpaImportance),
            ("c7TestImportance", "Mức coi trọng điểm thi", c7TestImportance),
            ("c7EcImportance", "Mức coi trọng ngoại khóa", c7EcImportance),
        })
        {
            if (value is not null && !Importances.Contains(value))
                errors[key] = $"{label} chỉ nhận very_important, important, considered hoặc not_considered.";
        }
        if (gpaScaleType is not null && !GpaScaleTypes.Contains(gpaScaleType))
            errors["gpaScaleType"] = "Loại thang GPA chỉ nhận weighted, unweighted hoặc both.";
        if (intlAidPolicy is not null && !AidPolicies.Contains(intlAidPolicy))
            errors["intlAidPolicy"] = "Chính sách hỗ trợ chỉ nhận need_and_merit, merit_only, need_only hoặc none.";
        if (campusSetting is not null && campusSetting.Length > 64)
            errors["campusSetting"] = "Bối cảnh khuôn viên tối đa 64 ký tự.";
        if (request.HousingTypes is { Length: > 2000 }) errors["housingTypes"] = "Loại nhà ở tối đa 2000 ký tự.";
        if (request.Activities is { Length: > 4000 }) errors["activities"] = "Hoạt động tối đa 4000 ký tự.";

        return errors;
    }

    /// <summary>
    /// UC-62 E3: kiểm tra JSON vòng nộp hồ sơ (ED, ED2, EA, REA, SCEA, RD, Rolling),
    /// định dạng ngày yyyy-MM-dd, không trùng loại vòng, và hạn Early không muộn hơn RD.
    /// </summary>
    private static string? ValidateAndNormalizeDeadlines(string? rawJson, Dictionary<string, string> errors)
    {
        if (rawJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors["deadlinesJson"] = "Vòng nộp hồ sơ không đúng định dạng.";
                return null;
            }

            var parsed = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!AllowedDeadlineRounds.TryGetValue(prop.Name.Trim(), out var canonical))
                {
                    errors["deadlinesJson"] = $"Loại vòng nộp \"{prop.Name}\" không hợp lệ (chỉ nhận ED, ED2, EA, REA, SCEA, RD, Rolling).";
                    return null;
                }
                if (parsed.ContainsKey(canonical))
                {
                    errors["deadlinesJson"] = $"Trùng loại vòng nộp \"{canonical}\".";
                    return null;
                }
                if (prop.Value.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParseExact(prop.Value.GetString()?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    errors["deadlinesJson"] = $"Vòng {canonical} sai định dạng ngày (cần yyyy-MM-dd).";
                    return null;
                }
                parsed[canonical] = date;
            }

            if (parsed.Count == 0) return null;

            if (parsed.TryGetValue("RD", out var rdDate))
            {
                foreach (var (round, date) in parsed)
                {
                    if (EarlyDeadlineRounds.Contains(round) && date > rdDate)
                    {
                        errors["deadlinesJson"] = $"Hạn vòng {round} không được muộn hơn Regular Decision (RD).";
                        return null;
                    }
                }
            }

            var normalized = parsed.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            return JsonSerializer.Serialize(normalized);
        }
        catch (JsonException)
        {
            errors["deadlinesJson"] = "Vòng nộp hồ sơ không đúng định dạng JSON.";
            return null;
        }
    }

    /// <summary>Đưa danh sách ngành của offering về đúng danh sách gửi lên; trả về các thay đổi để ghi audit.</summary>
    private async Task<List<object?>> ApplyMajorChangesAsync(
        Guid offeringId, IReadOnlyList<UpdateOfferingMajor> requestedMajors, IReadOnlyList<Major> knownMajors, CancellationToken ct)
    {
        var changes = new List<object?>();
        var currentLinks = await repository.GetMajorLinksAsync(offeringId, ct);
        var requestedById = requestedMajors.ToDictionary(m => m.MajorId);
        var nameById = knownMajors.ToDictionary(m => m.Id, m => m.Name);

        foreach (var link in currentLinks)
        {
            if (!requestedById.TryGetValue(link.MajorId, out var requested))
            {
                repository.RemoveMajorLink(link);
                changes.Add(new { major = nameById.GetValueOrDefault(link.MajorId), action = "remove" });
            }
            else if (link.IsStem != requested.IsStem)
            {
                link.IsStem = requested.IsStem;
                changes.Add(new { major = nameById.GetValueOrDefault(link.MajorId), action = "stem", to = requested.IsStem });
            }
        }

        var currentMajorIds = currentLinks.Select(l => l.MajorId).ToHashSet();
        foreach (var requested in requestedMajors)
        {
            if (currentMajorIds.Contains(requested.MajorId)) continue;

            // Ngành do moderator thêm tay coi là đã kiểm tra.
            repository.AddMajorLink(new UniversityOfferingMajor
            {
                UniversityOfferingId = offeringId,
                MajorId = requested.MajorId,
                IsStem = requested.IsStem,
                IsVerified = true,
            });
            changes.Add(new { major = nameById.GetValueOrDefault(requested.MajorId), action = "add", stem = requested.IsStem });
        }
        return changes;
    }

    private async Task<AdminUniversityDetailDto> ToDetailDtoAsync(University u, CancellationToken ct)
    {
        var lastEditedBy = await repository.GetUserDisplayNameAsync(u.LastEditedByUserId, ct);
        var medias = await repository.GetUniversityMediaAsync(u.Id, ct);
        var logoUrl = medias.FirstOrDefault(m => m.Type == "logo")?.Url;
        var coverUrl = medias.FirstOrDefault(m => m.Type == "cover")?.Url;
        var galleryUrls = medias.Where(m => m.Type == "gallery").OrderBy(m => m.DisplayOrder).Select(m => m.Url).ToList();

        return new AdminUniversityDetailDto(
            u.Id, u.Code, u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate,
            u.InternationalStudentCount, u.IsIvy, u.IsActive, u.UpdatedAt, lastEditedBy,
            logoUrl, coverUrl, galleryUrls);
    }

    private static AdminUniversityOfferingDto ToOfferingDto(
        UniversityOffering o, IReadOnlyList<AdminOfferingMajorRow> majorRows, string? lastEditedBy)
    {
        var items = majorRows
            .OrderByDescending(r => r.IsStem)
            .ThenBy(r => r.Name)
            .Select(r => new AdminOfferingMajorDto(r.MajorId, r.Name, r.CipCode, r.IsStem, r.IsVerified))
            .ToList();
        var majors = items.Select(i => i.Name).ToList();
        var stemMajors = items.Where(i => i.IsStem).Select(i => i.Name).ToList();

        return new AdminUniversityOfferingDto(
            o.Id, o.StudyLevel, o.AcademicYear, o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd,
            o.MinGpa4, o.MinIelts, o.MinToefl, o.MinDuolingo, o.AvgGpa4, o.Sat25, o.Sat75, o.Act25, o.Act75,
            o.SatPolicy, majors, stemMajors, items, o.DeadlinesJson, o.Notes, o.SourceUrl, o.RetrievedAt,
            o.SourceReliability, o.UpdatedAt, lastEditedBy, o.HealthInsuranceUsd,
            o.ApplicationFeeUsd, o.TuitionInStateUsd, o.ActPolicy, o.ActSubmitPct,
            o.ApplicantsTotal, o.AdmittedTotal, o.IntlApplicants, o.IntlAdmitted, o.IntlAcceptanceRate, o.SatSubmitPct, o.GpaSubmitPct, o.GpaPct40, o.GpaPct375To399, o.GpaPct350To374, o.GpaPct325To349, o.GpaPct300To324, o.GpaPct250To299, o.GpaPctBelow250, o.AvgGpaWeighted, o.GpaScaleType, o.C7GpaImportance, o.C7TestImportance, o.C7EcImportance, o.RetentionRate1Yr, o.GradRate6Yr, o.StudentFacultyRatio, o.IntlAidPolicy, o.IntlAidAvgUsd, o.IntlAidRecipients, o.CampusSetting, o.FyOnCampusPct, o.UgOnCampusPct, o.HousingTypes, o.IntlHousing, o.HousingOnlyUsd, o.HousingDepositUsd, o.Activities);
    }
}
