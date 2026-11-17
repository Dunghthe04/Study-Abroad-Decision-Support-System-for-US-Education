namespace StudyAbroad.Application.Admin.Scholarships;

public record AdminScholarshipQuery(
    Guid? UniversityId = null,
    string? Search = null,
    string? StudyLevel = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20);

public record AdminScholarshipDto(
    Guid Id,
    Guid? UniversityId,
    string? UniversityName,
    string Name,
    string? Provider,
    string StudyLevel,
    decimal? AmountUsd,
    string? CoverageType,
    decimal? MinGpa4,
    decimal? MinIelts,
    DateOnly? Deadline,
    string? EligibilityNotes,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? SourceReliability,
    bool IsActive,
    DateTime? UpdatedAt,
    string? LastEditedBy);

/// <summary>Dùng cho cả thêm mới và sửa; gửi đủ mọi trường.</summary>
public record SaveScholarshipRequest(
    Guid? UniversityId,
    string? Name,
    string? Provider,
    string? StudyLevel,
    decimal? AmountUsd,
    string? CoverageType,
    decimal? MinGpa4,
    decimal? MinIelts,
    DateOnly? Deadline,
    string? EligibilityNotes,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? SourceReliability);

public record SetScholarshipVisibilityRequest(bool IsActive, string? Reason = null);
