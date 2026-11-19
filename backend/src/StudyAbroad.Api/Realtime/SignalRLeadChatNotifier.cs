using Microsoft.AspNetCore.SignalR;
using StudyAbroad.Application.Leads;

namespace StudyAbroad.Api.Realtime;

/// <summary>[Chat realtime] Cài đặt <see cref="ILeadChatNotifier"/> bằng SignalR: đẩy sự kiện tới đúng phòng / đúng người dùng.</summary>
public sealed class SignalRLeadChatNotifier(IHubContext<LeadChatHub> hub) : ILeadChatNotifier
{
    public async Task NewMessageAsync(Guid leadId, LeadMessageDto studentView, LeadMessageDto centerView, CancellationToken ct = default)
    {
        await hub.Clients.Group(LeadChatHub.RoomGroup(leadId, LeadChatSides.Student)).SendAsync("newMessage", studentView, ct);
        await hub.Clients.Group(LeadChatHub.RoomGroup(leadId, LeadChatSides.Center)).SendAsync("newMessage", centerView, ct);
    }

    public Task MessagesReadAsync(Guid leadId, string senderSide, IReadOnlyList<Guid> messageIds, DateTime readAt, CancellationToken ct = default) =>
        hub.Clients.Group(LeadChatHub.RoomGroup(leadId, senderSide))
            .SendAsync("messagesRead", new { leadId, messageIds, readAt }, ct);
}
