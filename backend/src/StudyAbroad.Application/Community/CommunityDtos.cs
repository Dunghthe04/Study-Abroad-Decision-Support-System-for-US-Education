namespace StudyAbroad.Application.Community;

public sealed record CommunityMessageDto(
    Guid Id,
    Guid? UserId,
    string AuthorName,
    string AuthorRole,
    string Content,
    DateTime CreatedAt,
    bool IsMine);

public sealed record CommunityPresenceDto(
    Guid UserId,
    string FullName,
    string Role,
    DateTime? LastSeenAt = null);

public sealed record JoinCommunityResult(IReadOnlyList<CommunityPresenceDto> Members);

public sealed record SendCommunityMessageRequest(string Content);

public sealed record ReportCommunityMessageRequest(string Reason, string? Detail);

public sealed record CommunityReportDto(
    Guid Id,
    Guid MessageId,
    string MessageContent,
    DateTime MessageCreatedAt,
    Guid? AuthorUserId,
    string AuthorName,
    Guid? ReporterUserId,
    string ReporterName,
    string Reason,
    string? Detail,
    string Status,
    DateTime CreatedAt,
    bool AuthorIsRestricted = false,
    DateTime? AuthorRestrictedUntil = null);

public sealed record RestrictCommunityUserRequest(int Days, string Reason);

public sealed record CommunityRestrictionDto(
    bool IsRestricted,
    DateTime? RestrictedUntil,
    string? Reason,
    string? Message);
