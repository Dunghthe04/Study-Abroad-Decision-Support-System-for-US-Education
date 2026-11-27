namespace StudyAbroad.Application.Admin.Universities;

public record AdminUniversityQuery(
    string? Search = null,
    string? State = null,
    string? Control = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20);

public record AdminUniversityListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? City,
    string? State,
    string? Control,
    decimal? AcceptanceRate,
    bool IsActive,
    IReadOnlyList<string> StudyLevels,
    DateOnly? LatestRetrievedAt,
    string? LogoUrl = null,
    int ScholarshipCount = 0,
    IReadOnlyList<string>? Aliases = null);

public record AdminUniversityDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? City,
    string? State,
    string? Website,
    string? Control,
    decimal? AcceptanceRate,
    int? InternationalStudentCount,
    bool IsIvy,
    bool IsActive,
    DateTime? UpdatedAt,
    string? LastEditedBy,
    string? LogoUrl = null,
    string? CoverUrl = null,
    IReadOnlyList<string>? GalleryUrls = null,
    IReadOnlyList<string>? Aliases = null);

public record AdminOfferingMajorDto(Guid MajorId, string Name, string? CipCode, bool IsStem, bool IsVerified);

public record AdminUniversityOfferingDto(
    Guid Id,
    string StudyLevel,
    string? AcademicYear,
    decimal? TuitionUsd,
    decimal? LivingUsd,
    decimal? FeesUsd,
    decimal? OtherExpensesUsd,
    decimal? MinGpa4,
    decimal? MinIelts,
    decimal? MinToefl,
    decimal? MinDuolingo,
    decimal? AvgGpa4,
    int? Sat25,
    int? Sat75,
    int? Act25,
    int? Act75,
    string? SatPolicy,
    IReadOnlyList<string> Majors,
    IReadOnlyList<string> StemMajors,
    IReadOnlyList<AdminOfferingMajorDto> MajorItems,
    string? DeadlinesJson,
    string? Notes,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? SourceReliability,
    DateTime? UpdatedAt,
    string? LastEditedBy,
    decimal? HealthInsuranceUsd = null,
    decimal? ApplicationFeeUsd = null,
    decimal? TuitionInStateUsd = null,
    string? ActPolicy = null,
    decimal? ActSubmitPct = null,
    int? ApplicantsTotal = null,
    int? AdmittedTotal = null,
    int? IntlApplicants = null,
    int? IntlAdmitted = null,
    decimal? IntlAcceptanceRate = null,
    decimal? SatSubmitPct = null,
    decimal? GpaSubmitPct = null,
    decimal? GpaPct40 = null,
    decimal? GpaPct375To399 = null,
    decimal? GpaPct350To374 = null,
    decimal? GpaPct325To349 = null,
    decimal? GpaPct300To324 = null,
    decimal? GpaPct250To299 = null,
    decimal? GpaPctBelow250 = null,
    decimal? AvgGpaWeighted = null,
    string? GpaScaleType = null,
    string? C7GpaImportance = null,
    string? C7TestImportance = null,
    string? C7EcImportance = null,
    decimal? RetentionRate1Yr = null,
    decimal? GradRate6Yr = null,
    int? StudentFacultyRatio = null,
    string? IntlAidPolicy = null,
    decimal? IntlAidAvgUsd = null,
    int? IntlAidRecipients = null,
    string? CampusSetting = null,
    decimal? FyOnCampusPct = null,
    decimal? UgOnCampusPct = null,
    string? HousingTypes = null,
    bool? IntlHousing = null,
    decimal? HousingOnlyUsd = null,
    decimal? HousingDepositUsd = null,
    string? Activities = null);

public record AdminMajorOptionDto(Guid Id, string Name, string? CipCode);

/// <summary>Một dòng ngành của offering, đọc kèm tên và mã CIP.</summary>
public record AdminOfferingMajorRow(Guid UniversityOfferingId, Guid MajorId, string Name, string? CipCode, bool IsStem, bool IsVerified);

public record CreateUniversityRequest(
    string Code,
    string Name,
    string? City = null,
    string? State = null,
    string? Website = null,
    string? Control = null,
    decimal? AcceptanceRate = null,
    int? InternationalStudentCount = null,
    bool IsIvy = false,
    string? LogoUrl = null,
    string? CoverUrl = null,
    bool ConfirmDuplicateName = false,
    // Tên viết tắt (MIT, UCLA). null = giữ nguyên khi sửa
    IReadOnlyList<string>? Aliases = null);

public record UpdateUniversityRequest(
    string? Name,
    string? City,
    string? State,
    string? Website,
    string? Control,
    decimal? AcceptanceRate,
    int? InternationalStudentCount,
    bool IsIvy,
    string? LogoUrl = null,
    string? CoverUrl = null,
    bool ConfirmDuplicateName = false,
    // Tên viết tắt (MIT, UCLA). null = giữ nguyên khi sửa
    IReadOnlyList<string>? Aliases = null);

public record CreateOfferingRequest(string? StudyLevel);

public record UpdateOfferingMajor(Guid MajorId, bool IsStem);

public record UpdateOfferingRequest(
    string? AcademicYear,
    decimal? TuitionUsd,
    decimal? LivingUsd,
    decimal? FeesUsd,
    decimal? OtherExpensesUsd,
    decimal? MinGpa4,
    decimal? MinIelts,
    decimal? MinToefl,
    decimal? MinDuolingo,
    decimal? AvgGpa4,
    int? Sat25,
    int? Sat75,
    int? Act25,
    int? Act75,
    string? SatPolicy,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? SourceReliability,
    string? Notes,
    IReadOnlyList<UpdateOfferingMajor>? Majors,
    decimal? HealthInsuranceUsd = null,
    decimal? ApplicationFeeUsd = null,
    decimal? TuitionInStateUsd = null,
    string? ActPolicy = null,
    decimal? ActSubmitPct = null,
    int? ApplicantsTotal = null,
    int? AdmittedTotal = null,
    int? IntlApplicants = null,
    int? IntlAdmitted = null,
    decimal? IntlAcceptanceRate = null,
    decimal? SatSubmitPct = null,
    decimal? GpaSubmitPct = null,
    decimal? GpaPct40 = null,
    decimal? GpaPct375To399 = null,
    decimal? GpaPct350To374 = null,
    decimal? GpaPct325To349 = null,
    decimal? GpaPct300To324 = null,
    decimal? GpaPct250To299 = null,
    decimal? GpaPctBelow250 = null,
    decimal? AvgGpaWeighted = null,
    string? GpaScaleType = null,
    string? C7GpaImportance = null,
    string? C7TestImportance = null,
    string? C7EcImportance = null,
    decimal? RetentionRate1Yr = null,
    decimal? GradRate6Yr = null,
    int? StudentFacultyRatio = null,
    string? IntlAidPolicy = null,
    decimal? IntlAidAvgUsd = null,
    int? IntlAidRecipients = null,
    string? CampusSetting = null,
    decimal? FyOnCampusPct = null,
    decimal? UgOnCampusPct = null,
    string? HousingTypes = null,
    bool? IntlHousing = null,
    decimal? HousingOnlyUsd = null,
    decimal? HousingDepositUsd = null,
    string? Activities = null,
    string? DeadlinesJson = null);

public record SetUniversityVisibilityRequest(bool IsActive, string? Reason = null);

/// <summary>Kết quả của một thao tác sửa: có dữ liệu, không tìm thấy, hoặc lỗi theo từng trường dữ liệu.</summary>
public record AdminUpdateResult<T>(T? Value, bool NotFound, IReadOnlyDictionary<string, string> Errors)
{
    public bool Success => !NotFound && Errors.Count == 0;
    public static AdminUpdateResult<T> Ok(T value) => new(value, false, new Dictionary<string, string>());
    public static AdminUpdateResult<T> Missing() => new(default, true, new Dictionary<string, string>());
    public static AdminUpdateResult<T> Invalid(IReadOnlyDictionary<string, string> errors) => new(default, false, errors);
}
