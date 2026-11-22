using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Community;

public interface ICommunityRepository
{
    Task<IReadOnlyList<ForumPost>> ListVisibleChatAsync(
        DateTime? before, Guid? beforeId,
        DateTime? after, Guid? afterId,
        int take, CancellationToken ct);
    Task<ForumPost?> GetChatPostAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ForumPost>> GetChatPostsAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task AddPostAsync(ForumPost post, CancellationToken ct);
    Task<bool> HasOpenReportAsync(Guid postId, Guid reporterUserId, CancellationToken ct);
    Task AddReportAsync(ForumReport report, CancellationToken ct);
    Task<ForumReport?> GetReportAsync(Guid id, CancellationToken ct);
    Task CloseOpenReportsForPostAsync(Guid postId, Guid handlerId, CancellationToken ct);
    Task<PagedResult<ForumReport>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, User>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<User?> GetUserAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
