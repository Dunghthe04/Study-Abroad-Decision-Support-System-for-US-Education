using Microsoft.AspNetCore.SignalR;
using StudyAbroad.Application.Community;

namespace StudyAbroad.Api.Realtime;

public sealed class SignalRCommunityChatNotifier(IHubContext<LeadChatHub> hub) : ICommunityChatNotifier
{
    public Task NewMessageAsync(CommunityMessageDto message, CancellationToken ct = default) =>
        hub.Clients.Group(LeadChatHub.CommunityGroup).SendAsync("newCommunityMessage", message, ct);

    public Task MessageHiddenAsync(Guid messageId, CancellationToken ct = default) =>
        hub.Clients.Group(LeadChatHub.CommunityGroup).SendAsync("communityMessageHidden", new { id = messageId }, ct);

    public Task UserRestrictionChangedAsync(Guid userId, bool isRestricted, DateTime? restrictedUntil, string? reason, CancellationToken ct = default) =>
        hub.Clients.Group(LeadChatHub.UserGroup(userId)).SendAsync("communityRestrictionChanged", new
        {
            isRestricted,
            restrictedUntil,
            reason,
        }, ct);
}
