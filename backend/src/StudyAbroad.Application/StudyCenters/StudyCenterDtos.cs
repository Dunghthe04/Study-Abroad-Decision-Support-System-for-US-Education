namespace StudyAbroad.Application.StudyCenters;

public record StudyCenterDto(
    Guid Id,
    string Code,
    string Name,
    string? Website,
    string? Address,
    string? City,
    IReadOnlyList<string> StudyLevels,
    IReadOnlyList<string> Services,
    DateOnly? SurveyedAt);

public record StudyCenterQuery(
    string? Search = null,
    string? City = null,
    string? Service = null,
    string? StudyLevel = null,
    int Page = 1,
    int PageSize = 20);
