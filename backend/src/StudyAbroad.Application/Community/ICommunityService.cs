using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Community;

public interface ICommunityService
{
    Task<IReadOnlyList<CommunityMessageDto>> ListMessagesAsync(
        Guid viewerId,
        DateTime? before, Guid? beforeId,
        DateTime? after, Guid? afterId,
        int take, CancellationToken ct);
    Task<CommunityMessageDto> SendAsync(Guid userId, SendCommunityMessageRequest request, CancellationToken ct);
    Task ReportAsync(Guid reporterId, Guid messageId, ReportCommunityMessageRequest request, CancellationToken ct);
    Task<IReadOnlyList<CommunityPresenceDto>> DescribeUsersAsync(
        IEnumerable<Guid> userIds,
        IReadOnlyDictionary<Guid, DateTime>? userLastSeen = null,
        CancellationToken ct = default);
    Task<PagedResult<CommunityReportDto>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task HideFromReportAsync(Guid handlerId, Guid reportId, CancellationToken ct);
    Task HideMessageAsync(Guid handlerId, Guid messageId, CancellationToken ct);
    Task DismissReportAsync(Guid handlerId, Guid reportId, CancellationToken ct);
    Task RestrictUserAsync(Guid handlerId, Guid userId, RestrictCommunityUserRequest request, CancellationToken ct);
    Task UnrestrictUserAsync(Guid handlerId, Guid userId, CancellationToken ct);
    Task<CommunityRestrictionDto> RestrictionStatusAsync(Guid userId, CancellationToken ct);
}
