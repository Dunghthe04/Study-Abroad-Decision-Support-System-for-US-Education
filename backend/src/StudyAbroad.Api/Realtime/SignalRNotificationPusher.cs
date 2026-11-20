using Microsoft.AspNetCore.SignalR;
using StudyAbroad.Application.Notifications;

namespace StudyAbroad.Api.Realtime;

/// <summary>[Chat realtime] Cài đặt <see cref="INotificationPusher"/> bằng SignalR: gửi sự kiện "notification" vào nhóm riêng của người nhận.</summary>
public sealed class SignalRNotificationPusher(IHubContext<LeadChatHub> hub) : INotificationPusher
{
    public Task PushAsync(Guid userId, string title, string? body, string? linkUrl, CancellationToken ct = default) =>
        hub.Clients.Group(LeadChatHub.UserGroup(userId)).SendAsync("notification", new { title, body, linkUrl }, ct);
}
