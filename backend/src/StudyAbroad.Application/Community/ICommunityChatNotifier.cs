namespace StudyAbroad.Application.Community;

public interface ICommunityChatNotifier
{
    Task NewMessageAsync(CommunityMessageDto message, CancellationToken ct = default);
    Task MessageHiddenAsync(Guid messageId, CancellationToken ct = default);
    Task UserRestrictionChangedAsync(Guid userId, bool isRestricted, DateTime? restrictedUntil, string? reason, CancellationToken ct = default);
}
