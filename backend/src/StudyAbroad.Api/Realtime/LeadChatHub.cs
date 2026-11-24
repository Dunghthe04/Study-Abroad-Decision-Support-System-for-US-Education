using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Community;
using StudyAbroad.Application.Leads;

namespace StudyAbroad.Api.Realtime;

/// <summary>Kết quả vào phòng chat: người dùng ở phía nào và phía đối diện có đang online không.</summary>
public sealed record JoinLeadResult(string Side, bool PeerOnline);

/// <summary>
/// [Chat realtime] Hub SignalR cho chat giữa học sinh / phụ huynh và trung tâm trong một yêu cầu liên hệ.
/// Mỗi trình duyệt giữ một kết nối duy nhất (cả để nhận thông báo), rồi vào / rời phòng của từng lead khi mở / đóng khung chat.
/// Gửi tin vẫn đi qua REST (có kiểm tra nội dung, ảnh, trạng thái lead); hub chỉ đẩy kết quả tới phía đối diện và lo các
/// tín hiệu nhẹ: đang nhập, đã xem, đang online. Quyền vào phòng dùng đúng luật phân quyền của đọc tin nhắn (chống IDOR).
/// Xác thực bằng cookie HttpOnly như các API khác.
/// </summary>
[Authorize]
public sealed class LeadChatHub(ILeadService leadService, ChatPresenceTracker presence, ICommunityService community) : Hub
{
    public const string Path = "/api/v1/hubs/lead-chat";
    public const string CommunityGroup = "community";

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public static string RoomGroup(Guid leadId, string side) => $"lead:{leadId}:{side}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        // Nhóm riêng của người dùng để nhận thông báo, tách khỏi các phòng chat
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        if (presence.Connect(Context.ConnectionId, userId.Value))
            await PushPresenceAsync(userId.Value, online: true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (presence.LeaveCommunity(Context.ConnectionId, out var communityUserId, out var lastInCommunity) && lastInCommunity)
            await PushCommunityPresenceAsync(communityUserId, joined: false);

        if (presence.Disconnect(Context.ConnectionId, out var userId, out var wentOffline) && wentOffline)
            await PushPresenceAsync(userId, online: false);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<JoinCommunityResult> JoinCommunity()
    {
        var userId = Context.User?.GetUserId()
            ?? throw new HubException("Không vào được phòng cộng đồng.");

        var firstForUser = presence.JoinCommunity(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, CommunityGroup);

        var members = await community.DescribeUsersAsync(presence.CommunityUserIds(), presence.CommunityUserLastSeen(), Context.ConnectionAborted);
        if (firstForUser)
        {
            var me = members.FirstOrDefault(m => m.UserId == userId);
            if (me is not null)
                await Clients.OthersInGroup(CommunityGroup).SendAsync("communityPresence", new { joined = true, member = me });
        }

        return new JoinCommunityResult(members);
    }

    public async Task LeaveCommunity()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CommunityGroup);
        if (presence.LeaveCommunity(Context.ConnectionId, out var userId, out var lastForUser) && lastForUser)
            await PushCommunityPresenceAsync(userId, joined: false);
    }

    /// <summary>Vào phòng chat của một lead. Không có quyền thì báo lỗi chung, không lộ lead có tồn tại hay không.</summary>
    public async Task<JoinLeadResult> JoinLead(Guid leadId)
    {
        var access = await GetAccessAsync(leadId);

        presence.Join(Context.ConnectionId, leadId, access.Side, access.PeerUserIds);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(leadId, access.Side));

        return new JoinLeadResult(access.Side, presence.AnyOnline(access.PeerUserIds));
    }

    public async Task LeaveLead(Guid leadId)
    {
        if (presence.TryGetSide(Context.ConnectionId, leadId, out var side))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(leadId, side));

        presence.Leave(Context.ConnectionId, leadId);
    }

    /// <summary>Báo phía đối diện biết mình đang nhập (hoặc đã ngừng). Chỉ hợp lệ khi đã vào phòng.</summary>
    public async Task Typing(Guid leadId, bool isTyping)
    {
        if (!presence.TryGetSide(Context.ConnectionId, leadId, out var side)) return;

        await Clients.Group(RoomGroup(leadId, LeadChatSides.Opposite(side)))
            .SendAsync("typing", new { leadId, isTyping });
    }

    /// <summary>Người dùng đang xem cuộc trò chuyện: đánh dấu đã xem các tin của phía đối diện.</summary>
    public async Task MarkRead(Guid leadId)
    {
        if (!presence.TryGetSide(Context.ConnectionId, leadId, out _)) return;

        var user = Context.User!;
        try
        {
            await leadService.MarkThreadReadAsync(
                user.GetUserId()!.Value, user.GetEmail(), user.GetRole() ?? string.Empty, leadId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new HubException("Không đánh dấu đã xem được.");
        }
    }

    private async Task<LeadChatAccessDto> GetAccessAsync(Guid leadId)
    {
        var user = Context.User!;
        try
        {
            return await leadService.GetChatAccessAsync(
                user.GetUserId()!.Value, user.GetEmail(), user.GetRole() ?? string.Empty, leadId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mọi lý do từ chối (không tìm thấy, sai vai trò, chưa đủ tuổi...) trả cùng một thông báo
            throw new HubException("Không vào được cuộc trò chuyện này.");
        }
    }

    /// <summary>Báo cho những ai đang mở khung chat có người này ở phía đối diện rằng họ vừa online / offline.</summary>
    private async Task PushPresenceAsync(Guid userId, bool online)
    {
        foreach (var watcher in presence.WatchersOf(userId))
        {
            // Phía đối diện có thể còn người khác đang online (ví dụ cả học sinh lẫn phụ huynh), nên tính lại cho cả nhóm
            var peerOnline = online || presence.AnyOnline(watcher.Peers);
            await Clients.Client(watcher.ConnectionId).SendAsync("presence", new { leadId = watcher.LeadId, online = peerOnline });
        }
    }

    private async Task PushCommunityPresenceAsync(Guid userId, bool joined)
    {
        var members = await community.DescribeUsersAsync([userId], ct: Context.ConnectionAborted);
        var member = members.FirstOrDefault() ?? new CommunityPresenceDto(userId, "Thành viên", "");
        await Clients.Group(CommunityGroup).SendAsync("communityPresence", new { joined, member });
    }
}
