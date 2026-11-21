namespace StudyAbroad.Application.Leads;

/// <summary>
/// [Chat realtime] Phía nào của cuộc trò chuyện: học sinh / phụ huynh hay trung tâm.
/// </summary>
public static class LeadChatSides
{
    public const string Student = "student";
    public const string Center = "center";

    public static string Opposite(string side) => side == Center ? Student : Center;
}

/// <summary>
/// [Chat realtime] Người dùng được vào phòng chat của một yêu cầu liên hệ ở phía nào, và những ai ở phía đối diện
/// (để biết họ có đang online hay không).
/// </summary>
public sealed record LeadChatAccessDto(string Side, IReadOnlyList<Guid> PeerUserIds);

/// <summary>
/// [Chat realtime] Đẩy sự kiện tới trình duyệt đang kết nối. Cài đặt nằm ở tầng Api (SignalR). Mọi lỗi đẩy tin
/// đều không được làm hỏng thao tác chính: tin đã lưu thì vẫn lấy được bằng REST.
/// </summary>
public interface ILeadChatNotifier
{
    /// <summary>
    /// Tin mới. Hai bản DTO khác nhau ở tên người gửi: học sinh luôn thấy tên thật của mình, còn trung tâm chỉ thấy khi
    /// yêu cầu có chia sẻ nhóm thông tin liên hệ (UC-42), nên mỗi phía nhận đúng bản của mình.
    /// </summary>
    Task NewMessageAsync(Guid leadId, LeadMessageDto studentView, LeadMessageDto centerView, CancellationToken ct = default);

    /// <summary>Các tin vừa được xem. Gửi cho phía đã gửi những tin đó.</summary>
    Task MessagesReadAsync(Guid leadId, string senderSide, IReadOnlyList<Guid> messageIds, DateTime readAt, CancellationToken ct = default);
}

/// <summary>
/// [Chat realtime] Ai đang mở khung chat nào, để khỏi tạo thông báo tin mới cho người đang xem đúng cuộc trò chuyện đó.
/// </summary>
public interface ILeadChatPresence
{
    bool IsViewing(Guid userId, Guid leadId);
}
