using System.Text.Json.Serialization;

namespace StudyAbroad.Application.TargetSchools;

/// <summary>
/// [UC-30] Yêu cầu thêm một trường đại học vào danh sách mục tiêu của học sinh.
/// Cho phép truyền UniversityId (Guid) hoặc UniversityCode (chuỗi mã trường).
/// </summary>
public record AddTargetSchoolRequest(
    Guid? UniversityId = null,
    string? UniversityCode = null,
    string? Category = null,
    string? ApplicationRound = null,
    DateTime? Deadline = null,
    string? Notes = null,
    bool? IsUserCategory = null
);

/// <summary>
/// Kết quả thực hiện thêm trường mục tiêu.
/// </summary>
public record AddTargetSchoolResult(TargetSchoolDto? TargetSchool, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && TargetSchool is not null;
    public static AddTargetSchoolResult Ok(TargetSchoolDto targetSchool) => new(targetSchool);
    public static AddTargetSchoolResult Fail(string message) => new(null, message);
}

/// <summary>
/// DTO biểu diễn một trường mục tiêu trong danh sách theo dõi của học sinh.
/// </summary>
public record TargetSchoolDto(
    Guid Id,
    Guid UserId,
    Guid UniversityId,
    string UniversityCode,
    string UniversityName,
    string? City,
    string? State,
    string? Website,
    string? Control,
    decimal? AcceptanceRate,
    string? Category,
    string Status,
    int Priority,
    string? Notes,
    string? ApplicationRound,
    DateTime? Deadline,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    decimal? EstimatedAnnualCostUsd = null,
    bool IsCategoryUserSelected = false,
    IReadOnlyList<string>? AvailableRounds = null
);

/// <summary>
/// [UC-32] Yêu cầu chọn hoặc thay đổi vòng nộp hồ sơ cho một trường mục tiêu.
/// </summary>
public record UpdateApplicationRoundRequest(
    string ApplicationRound,
    DateTime? Deadline = null,
    bool AutoResolveDeadline = true
);

/// <summary>
/// Kết quả thực hiện chọn vòng nộp hồ sơ.
/// </summary>
public record UpdateApplicationRoundResult(TargetSchoolDto? TargetSchool, string? ErrorMessage = null, bool NotFound = false)
{
    public bool Success => ErrorMessage is null && TargetSchool is not null;
    public static UpdateApplicationRoundResult Ok(TargetSchoolDto targetSchool) => new(targetSchool);
    public static UpdateApplicationRoundResult Fail(string message) => new(null, message);
    public static UpdateApplicationRoundResult NotFoundError(string message) => new(null, message, NotFound: true);
}

/// <summary>
/// [UC-33] Yêu cầu đổi nhóm phân loại (reach / match / safety) của trường mục tiêu đã lưu.
/// </summary>
public record UpdateTargetCategoryRequest(string Category);

/// <summary>
/// Kết quả đổi nhóm phân loại trường mục tiêu.
/// </summary>
public record UpdateTargetCategoryResult(TargetSchoolDto? TargetSchool, string? ErrorMessage = null, bool NotFound = false)
{
    public bool Success => ErrorMessage is null && TargetSchool is not null;
    public static UpdateTargetCategoryResult Ok(TargetSchoolDto targetSchool) => new(targetSchool);
    public static UpdateTargetCategoryResult Fail(string message) => new(null, message);
    public static UpdateTargetCategoryResult NotFoundError(string message) => new(null, message, NotFound: true);
}

/// <summary>
/// Thông tin mặc định khi mở hộp thoại thêm trường mục tiêu: nhóm gợi ý gần nhất và các vòng nộp trường hỗ trợ.
/// </summary>
public record TargetSchoolDefaultsDto(
    string? RecommendedCategory,
    IReadOnlyList<string> AvailableRounds,
    bool IsScholarshipMandatory = false,
    string? ExistingEdSchoolName = null,
    string? ExistingEd2SchoolName = null
);

/// <summary>
/// [UC-33] Tổng hợp danh sách trường mục tiêu kèm số lượng theo từng nhóm phân loại và hạn nộp gần nhất.
/// </summary>
public record TargetSchoolSummaryDto(
    List<TargetSchoolDto> Items,
    int TotalCount,
    int ReachCount,
    int MatchCount,
    int SafetyCount,
    int UndecidedCategoryCount,
    DateTime? NextDeadline,
    bool IsScholarshipMandatory = false
);
