namespace StudyAbroad.Application.Community;

public interface ICommunityChatNotifier
{
    Task NewMessageAsync(CommunityMessageDto message, CancellationToken ct = default);
    Task MessageHiddenAsync(Guid messageId, CancellationToken ct = default);
}
