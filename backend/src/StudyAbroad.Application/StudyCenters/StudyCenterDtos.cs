namespace StudyAbroad.Application.StudyCenters;
public record CenterReferralQuery(string? Service = null, string? StudyLevel = null, string? City = null);

public record StudyCenterDto(
    Guid Id,
    string Code,
    string Name,
    string? Website,
    string? Address,
    string? City,
    IReadOnlyList<string> StudyLevels,
    IReadOnlyList<string> Services,
    DateOnly? SurveyedAt,
    string? Phone = null,
    string? Email = null,
    string? Description = null,
    string? VerificationStatus = "unverified",
    bool IsActive = true);

/// <summary>[UC-71] Moderator / Admin thêm trung tâm tư vấn vào danh bạ.</summary>
public record CreateStudyCenterRequest(
    string Code,
    string Name,
    string? Website = null,
    string? Address = null,
    string? City = null,
    List<string>? StudyLevels = null,
    List<string>? Services = null,
    string? Phone = null,
    string? Email = null,
    string? Description = null,
    DateOnly? SurveyedAt = null);

public record StudyCenterQuery(
    string? Search = null,
    string? City = null,
    string? Service = null,
    string? StudyLevel = null,
    int Page = 1,
    int PageSize = 20);
