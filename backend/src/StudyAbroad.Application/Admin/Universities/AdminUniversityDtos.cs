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
    DateOnly? LatestRetrievedAt);

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
    DateTime? UpdatedAt);

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
    DateTime? UpdatedAt);

public record AdminMajorOptionDto(Guid Id, string Name, string? CipCode);

/// <summary>Một dòng ngành của offering, đọc kèm tên và mã CIP.</summary>
public record AdminOfferingMajorRow(Guid UniversityOfferingId, Guid MajorId, string Name, string? CipCode, bool IsStem, bool IsVerified);

public record UpdateUniversityRequest(
    string? Name,
    string? City,
    string? State,
    string? Website,
    string? Control,
    decimal? AcceptanceRate,
    int? InternationalStudentCount,
    bool IsIvy);

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
    string? Notes,
    IReadOnlyList<UpdateOfferingMajor>? Majors);

public record SetUniversityVisibilityRequest(bool IsActive);

/// <summary>Kết quả của một thao tác sửa: có dữ liệu, không tìm thấy, hoặc lỗi theo từng trường dữ liệu.</summary>
public record AdminUpdateResult<T>(T? Value, bool NotFound, IReadOnlyDictionary<string, string> Errors)
{
    public bool Success => !NotFound && Errors.Count == 0;
    public static AdminUpdateResult<T> Ok(T value) => new(value, false, new Dictionary<string, string>());
    public static AdminUpdateResult<T> Missing() => new(default, true, new Dictionary<string, string>());
    public static AdminUpdateResult<T> Invalid(IReadOnlyDictionary<string, string> errors) => new(default, false, errors);
}
