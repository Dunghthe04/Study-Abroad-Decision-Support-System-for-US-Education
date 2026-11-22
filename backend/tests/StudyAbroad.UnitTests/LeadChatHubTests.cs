using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using StudyAbroad.Api.Realtime;
using StudyAbroad.Application.Community;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[Chat realtime] Kiểm tra bộ theo dõi hiện diện và hub SignalR chat yêu cầu liên hệ.</summary>
public class LeadChatHubTests
{
    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid CenterOwnerId = Guid.NewGuid();

    // ===== ChatPresenceTracker =====

    [Fact]
    public void Tracker_OnlineOnlyWhileAtLeastOneConnectionOpen()
    {
        var t = new ChatPresenceTracker();

        Assert.True(t.Connect("c1", StudentId));   // kết nối đầu tiên: vừa online
        Assert.False(t.Connect("c2", StudentId));  // mở thêm tab: không phải thay đổi
        Assert.True(t.IsOnline(StudentId));

        Assert.True(t.Disconnect("c1", out _, out var offlineAfterFirst));
        Assert.False(offlineAfterFirst);           // còn tab kia
        Assert.True(t.IsOnline(StudentId));

        Assert.True(t.Disconnect("c2", out var userId, out var offlineAfterLast));
        Assert.True(offlineAfterLast);
        Assert.Equal(StudentId, userId);
        Assert.False(t.IsOnline(StudentId));
    }

    [Fact]
    public void Tracker_DisconnectUnknownConnection_IsIgnored()
    {
        var t = new ChatPresenceTracker();
        Assert.False(t.Disconnect("khong-ton-tai", out _, out var offline));
        Assert.False(offline);
    }

    [Fact]
    public void Tracker_IsViewing_OnlyForThatLeadAndWhileJoined()
    {
        var t = new ChatPresenceTracker();
        t.Connect("c1", CenterOwnerId);

        Assert.False(t.IsViewing(CenterOwnerId, LeadId));
        t.Join("c1", LeadId, LeadChatSides.Center, [StudentId]);
        Assert.True(t.IsViewing(CenterOwnerId, LeadId));
        Assert.False(t.IsViewing(CenterOwnerId, Guid.NewGuid())); // lead khác
        Assert.False(t.IsViewing(StudentId, LeadId));              // người khác

        t.Leave("c1", LeadId);
        Assert.False(t.IsViewing(CenterOwnerId, LeadId));
    }

    [Fact]
    public void Tracker_WatchersOf_ReturnsOnlyRoomsThatCareAboutThatUser()
    {
        var t = new ChatPresenceTracker();
        t.Connect("center-tab", CenterOwnerId);
        t.Join("center-tab", LeadId, LeadChatSides.Center, [StudentId]);
        t.Connect("other-tab", Guid.NewGuid());
        t.Join("other-tab", Guid.NewGuid(), LeadChatSides.Center, [Guid.NewGuid()]);

        var watchers = t.WatchersOf(StudentId);

        var w = Assert.Single(watchers);
        Assert.Equal("center-tab", w.ConnectionId);
        Assert.Equal(LeadId, w.LeadId);
    }

    [Fact]
    public void Tracker_Community_JoinLeave_TracksUniqueUsers()
    {
        var t = new ChatPresenceTracker();
        t.Connect("a1", StudentId);
        t.Connect("a2", StudentId);
        t.Connect("b1", CenterOwnerId);

        Assert.True(t.JoinCommunity("a1"));
        Assert.False(t.JoinCommunity("a2"));
        Assert.Equal(StudentId, Assert.Single(t.CommunityUserIds()));

        Assert.True(t.JoinCommunity("b1"));
        Assert.Equal(2, t.CommunityUserIds().Count);

        Assert.True(t.LeaveCommunity("a1", out var id, out var last));
        Assert.Equal(StudentId, id);
        Assert.False(last);
        Assert.True(t.LeaveCommunity("a2", out _, out var lastTab));
        Assert.True(lastTab);
        Assert.Equal(CenterOwnerId, Assert.Single(t.CommunityUserIds()));
    }

    [Fact]
    public void Tracker_TryGetSide_FalseUntilJoined()
    {
        var t = new ChatPresenceTracker();
        t.Connect("c1", StudentId);
        Assert.False(t.TryGetSide("c1", LeadId, out _));

        t.Join("c1", LeadId, LeadChatSides.Student, []);
        Assert.True(t.TryGetSide("c1", LeadId, out var side));
        Assert.Equal(LeadChatSides.Student, side);
    }

    // ===== LeadChatHub =====

    [Fact]
    public async Task Hub_Connect_JoinsPersonalGroup_AndBroadcastsPresenceToWatchers()
    {
        var presence = new ChatPresenceTracker();
        // Trung tâm đang mở khung chat, theo dõi học sinh
        presence.Connect("center-tab", CenterOwnerId);
        presence.Join("center-tab", LeadId, LeadChatSides.Center, [StudentId]);

        var h = NewHub(presence, StudentId, "student-tab");
        await h.Hub.OnConnectedAsync();

        Assert.Contains((h.ConnectionId, LeadChatHub.UserGroup(StudentId)), h.Groups.Added);
        var sent = Assert.Single(h.Clients.SentToClients, x => x.ConnectionId == "center-tab");
        Assert.Equal("presence", sent.Method);
        Assert.Equal(true, Prop(sent.Arg, "online"));
    }

    [Fact]
    public async Task Hub_LastDisconnect_TellsWatchersPeerWentOffline()
    {
        var presence = new ChatPresenceTracker();
        presence.Connect("center-tab", CenterOwnerId);
        presence.Join("center-tab", LeadId, LeadChatSides.Center, [StudentId]);

        // alreadyConnected: học sinh đang kết nối bằng đúng một tab
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true);
        await h.Hub.OnDisconnectedAsync(null);

        var sent = Assert.Single(h.Clients.SentToClients, x => x.ConnectionId == "center-tab");
        Assert.Equal(false, Prop(sent.Arg, "online"));
    }

    [Fact]
    public async Task Hub_JoinLead_ReturnsSideAndPeerOnline_AndJoinsRoomGroup()
    {
        var presence = new ChatPresenceTracker();
        presence.Connect("center-tab", CenterOwnerId); // trung tâm đang online
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true);

        var result = await h.Hub.JoinLead(LeadId);

        Assert.Equal(LeadChatSides.Student, result.Side);
        Assert.True(result.PeerOnline);
        Assert.Contains((h.ConnectionId, LeadChatHub.RoomGroup(LeadId, LeadChatSides.Student)), h.Groups.Added);
        Assert.True(presence.IsViewing(StudentId, LeadId));
    }

    [Fact]
    public async Task Hub_JoinLead_WithoutAccess_ThrowsGenericHubException_AndJoinsNothing()
    {
        var presence = new ChatPresenceTracker();
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true, denyAccess: true);

        var ex = await Assert.ThrowsAsync<HubException>(() => h.Hub.JoinLead(LeadId));

        Assert.Equal("Không vào được cuộc trò chuyện này.", ex.Message); // không lộ lead có tồn tại hay không
        Assert.DoesNotContain(h.Groups.Added, g => g.Group.StartsWith("lead:"));
        Assert.False(presence.IsViewing(StudentId, LeadId));
    }

    [Fact]
    public async Task Hub_Typing_GoesToOppositeSideOnly_AndOnlyAfterJoining()
    {
        var presence = new ChatPresenceTracker();
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true);

        await h.Hub.Typing(LeadId, true); // chưa vào phòng: bỏ qua
        Assert.Empty(h.Clients.SentToGroups);

        await h.Hub.JoinLead(LeadId);
        await h.Hub.Typing(LeadId, true);

        var sent = Assert.Single(h.Clients.SentToGroups);
        Assert.Equal(LeadChatHub.RoomGroup(LeadId, LeadChatSides.Center), sent.Group); // gửi cho trung tâm, không gửi lại cho mình
        Assert.Equal("typing", sent.Method);
        Assert.Equal(true, Prop(sent.Arg, "isTyping"));
    }

    [Fact]
    public async Task Hub_MarkRead_IgnoredUntilJoined_ThenCallsService()
    {
        var presence = new ChatPresenceTracker();
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true);

        await h.Hub.MarkRead(LeadId);
        Assert.Empty(h.Service.MarkReadCalls);

        await h.Hub.JoinLead(LeadId);
        await h.Hub.MarkRead(LeadId);
        Assert.Equal([LeadId], h.Service.MarkReadCalls);
    }

    [Fact]
    public async Task Hub_LeaveLead_LeavesRoomGroupAndStopsViewing()
    {
        var presence = new ChatPresenceTracker();
        var h = NewHub(presence, StudentId, "student-tab", alreadyConnected: true);
        await h.Hub.JoinLead(LeadId);

        await h.Hub.LeaveLead(LeadId);

        Assert.Contains((h.ConnectionId, LeadChatHub.RoomGroup(LeadId, LeadChatSides.Student)), h.Groups.Removed);
        Assert.False(presence.IsViewing(StudentId, LeadId));
    }

    // ===== Dựng hub với các lớp giả =====

    private static object? Prop(object? o, string name) => o?.GetType().GetProperty(name)?.GetValue(o);

    private sealed record HubHarness(
        LeadChatHub Hub, string ConnectionId, FakeGroups Groups, FakeClients Clients, StubLeadService Service);

    private static HubHarness NewHub(
        ChatPresenceTracker presence, Guid userId, string connectionId, bool alreadyConnected = false, bool denyAccess = false)
    {
        if (alreadyConnected) presence.Connect(connectionId, userId);

        var service = new StubLeadService { Deny = denyAccess };
        var groups = new FakeGroups();
        var clients = new FakeClients();
        var hub = new LeadChatHub(service.Create(), presence, new StubCommunityService())
        {
            Context = new FakeContext(connectionId, userId),
            Groups = groups,
            Clients = clients,
        };
        return new HubHarness(hub, connectionId, groups, clients, service);
    }

    private sealed class StubCommunityService : ICommunityService
    {
        public Task<IReadOnlyList<CommunityMessageDto>> ListMessagesAsync(
            Guid viewerId, DateTime? before, Guid? beforeId, DateTime? after, Guid? afterId, int take, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<CommunityMessageDto> SendAsync(Guid userId, SendCommunityMessageRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task ReportAsync(Guid reporterId, Guid messageId, ReportCommunityMessageRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<CommunityPresenceDto>> DescribeUsersAsync(
            IEnumerable<Guid> userIds, IReadOnlyDictionary<Guid, DateTime>? userLastSeen = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CommunityPresenceDto>>([]);
        public Task<StudyAbroad.Application.Common.PagedResult<CommunityReportDto>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task HideFromReportAsync(Guid handlerId, Guid reportId, CancellationToken ct) => throw new NotSupportedException();
        public Task HideMessageAsync(Guid handlerId, Guid messageId, CancellationToken ct) => throw new NotSupportedException();
        public Task DismissReportAsync(Guid handlerId, Guid reportId, CancellationToken ct) => throw new NotSupportedException();
        public Task RestrictUserAsync(Guid handlerId, Guid userId, RestrictCommunityUserRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task UnrestrictUserAsync(Guid handlerId, Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommunityRestrictionDto> RestrictionStatusAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new CommunityRestrictionDto(false, null, null, null));
    }

    /// <summary>Giả ILeadService bằng DispatchProxy: chỉ hai hàm chat được dùng, gọi hàm khác là lỗi test.</summary>
    private class StubLeadService : DispatchProxy
    {
        public bool Deny { get; set; }
        public List<Guid> MarkReadCalls { get; } = [];
        private StubLeadService? _state;

        public ILeadService Create()
        {
            var proxy = Create<ILeadService, StubLeadService>();
            ((StubLeadService)(object)proxy)._state = this;
            return proxy;
        }

        public StubLeadService() { }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var state = _state ?? this;
            switch (targetMethod!.Name)
            {
                case nameof(ILeadService.GetChatAccessAsync):
                    if (state.Deny) return Task.FromException<LeadChatAccessDto>(new KeyNotFoundException("khong co quyen"));
                    var role = (string)args![2]!;
                    var side = role == UserRoles.Center ? LeadChatSides.Center : LeadChatSides.Student;
                    IReadOnlyList<Guid> peers = side == LeadChatSides.Student ? [CenterOwnerId] : [StudentId];
                    return Task.FromResult(new LeadChatAccessDto(side, peers));
                case nameof(ILeadService.MarkThreadReadAsync):
                    state.MarkReadCalls.Add((Guid)args![3]!);
                    return Task.CompletedTask;
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }

    private sealed class FakeContext : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;
        public FakeContext(string connectionId, Guid userId)
        {
            ConnectionId = connectionId;
            _user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, UserRoles.Student),
            ], "test"));
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class FakeGroups : IGroupManager
    {
        public List<(string ConnectionId, string Group)> Added { get; } = [];
        public List<(string ConnectionId, string Group)> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClients : IHubCallerClients
    {
        public List<(string Group, string Method, object? Arg)> SentToGroups { get; } = [];
        public List<(string ConnectionId, string Method, object? Arg)> SentToClients { get; } = [];

        private sealed class Proxy(Action<string, object?> onSend) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                onSend(method, args.Length > 0 ? args[0] : null);
                return Task.CompletedTask;
            }
        }

        public IClientProxy Group(string groupName) => new Proxy((m, a) => SentToGroups.Add((groupName, m, a)));
        public IClientProxy Client(string connectionId) => new Proxy((m, a) => SentToClients.Add((connectionId, m, a)));

        public IClientProxy Caller => throw new NotSupportedException();
        public IClientProxy Others => throw new NotSupportedException();
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy OthersInGroup(string groupName) => new Proxy((m, a) => SentToGroups.Add((groupName, m, a)));
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
