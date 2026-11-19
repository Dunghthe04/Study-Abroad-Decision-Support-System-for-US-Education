using StudyAbroad.Application.Leads;

namespace StudyAbroad.Api.Realtime;

/// <summary>
/// [Chat realtime] Theo dõi ai đang kết nối và ai đang mở khung chat nào. Chỉ giữ trong bộ nhớ của một tiến trình API:
/// chạy nhiều bản API song song thì cần thêm backplane (Redis) và nơi lưu hiện diện dùng chung.
/// </summary>
public sealed class ChatPresenceTracker : ILeadChatPresence
{
    private sealed record Room(string Side, IReadOnlyList<Guid> PeerUserIds);

    private sealed class Connection(Guid userId)
    {
        public Guid UserId { get; } = userId;
        public Dictionary<Guid, Room> Rooms { get; } = [];
    }

    /// <summary>Một khung chat đang mở mà người xem quan tâm tới trạng thái online của <see cref="Peers"/>.</summary>
    public sealed record Watcher(string ConnectionId, Guid LeadId, IReadOnlyList<Guid> Peers);

    private readonly object _gate = new();
    private readonly Dictionary<string, Connection> _connections = [];
    private readonly Dictionary<Guid, int> _connectionsPerUser = [];

    /// <summary>Ghi nhận kết nối mới. Trả true nếu đây là kết nối đầu tiên của người dùng (vừa chuyển sang online).</summary>
    public bool Connect(string connectionId, Guid userId)
    {
        lock (_gate)
        {
            _connections[connectionId] = new Connection(userId);
            _connectionsPerUser[userId] = _connectionsPerUser.GetValueOrDefault(userId) + 1;
            return _connectionsPerUser[userId] == 1;
        }
    }

    /// <summary>Gỡ kết nối. <paramref name="wentOffline"/> là true khi đó là kết nối cuối cùng của người dùng.</summary>
    public bool Disconnect(string connectionId, out Guid userId, out bool wentOffline)
    {
        lock (_gate)
        {
            userId = default;
            wentOffline = false;
            if (!_connections.Remove(connectionId, out var connection)) return false;

            userId = connection.UserId;
            var remaining = _connectionsPerUser.GetValueOrDefault(userId) - 1;
            if (remaining <= 0)
            {
                _connectionsPerUser.Remove(userId);
                wentOffline = true;
            }
            else
            {
                _connectionsPerUser[userId] = remaining;
            }
            return true;
        }
    }

    public void Join(string connectionId, Guid leadId, string side, IReadOnlyList<Guid> peerUserIds)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
                connection.Rooms[leadId] = new Room(side, peerUserIds);
        }
    }

    public void Leave(string connectionId, Guid leadId)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
                connection.Rooms.Remove(leadId);
        }
    }

    /// <summary>Phía mà kết nối này đã vào phòng của lead. Chưa vào phòng (hoặc không có quyền) thì trả false.</summary>
    public bool TryGetSide(string connectionId, Guid leadId, out string side)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var connection) && connection.Rooms.TryGetValue(leadId, out var room))
            {
                side = room.Side;
                return true;
            }
            side = string.Empty;
            return false;
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate) return _connectionsPerUser.ContainsKey(userId);
    }

    public bool AnyOnline(IEnumerable<Guid> userIds)
    {
        lock (_gate) return userIds.Any(_connectionsPerUser.ContainsKey);
    }

    /// <summary>Người dùng có kết nối nào đang mở đúng cuộc trò chuyện của lead này không.</summary>
    public bool IsViewing(Guid userId, Guid leadId)
    {
        lock (_gate)
        {
            return _connections.Values.Any(c => c.UserId == userId && c.Rooms.ContainsKey(leadId));
        }
    }

    /// <summary>Các khung chat đang mở mà người xem theo dõi <paramref name="userId"/> (để báo khi người đó online / offline).</summary>
    public IReadOnlyList<Watcher> WatchersOf(Guid userId)
    {
        lock (_gate)
        {
            return _connections
                .SelectMany(c => c.Value.Rooms.Select(r => new Watcher(c.Key, r.Key, r.Value.PeerUserIds)))
                .Where(w => w.Peers.Contains(userId))
                .ToList();
        }
    }
}
