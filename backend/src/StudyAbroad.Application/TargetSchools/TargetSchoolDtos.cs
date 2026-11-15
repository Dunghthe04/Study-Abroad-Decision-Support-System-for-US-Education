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
    string? Notes = null
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
    DateTime? UpdatedAt
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
public record UpdateApplicationRoundResult(TargetSchoolDto? TargetSchool, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && TargetSchool is not null;
    public static UpdateApplicationRoundResult Ok(TargetSchoolDto targetSchool) => new(targetSchool);
    public static UpdateApplicationRoundResult Fail(string message) => new(null, message);
}

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
    DateTime? NextDeadline
);
