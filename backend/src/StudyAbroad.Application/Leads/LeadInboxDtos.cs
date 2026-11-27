using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [Hộp thư tin nhắn] Một cuộc trò chuyện trong danh sách kiểu Messenger.
/// <paramref name="Title"/>: phía học sinh / phụ huynh là tên trung tâm; phía trung tâm là tên học sinh khi được chia sẻ
/// liên hệ, không thì tên ẩn danh (cùng luật với luồng tin nhắn).
/// <paramref name="StudentName"/>: tên học sinh của yêu cầu, chỉ trả cho phía học sinh / phụ huynh (phụ huynh có nhiều con
/// cần biết cuộc trò chuyện của con nào).
/// <paramref name="LastMessageFromMe"/>: tin cuối do phía mình gửi (phụ huynh và con cùng một phía).
/// <paramref name="UnreadCount"/>: tin còn hiện của phía đối diện chưa được xem (cùng luật với đánh dấu đã xem).
/// <paramref name="CanMessage"/>: yêu cầu còn nhắn tin được (Đã chấp nhận, chưa đóng / thu hồi / quá hạn).
/// </summary>
public record LeadConversationDto(
    Guid LeadId,
    string Title,
    string? StudentName,
    string LeadStatus,
    string? LastMessage,
    bool LastMessageHasImage,
    DateTime? LastMessageAt,
    bool LastMessageFromMe,
    int UnreadCount,
    bool CanMessage);

/// <summary>[Hộp thư tin nhắn] Các cuộc trò chuyện mới nhất trước, kèm tổng tin chưa đọc của mọi cuộc (kể cả cuộc không nằm trong danh sách).</summary>
public record LeadInboxDto(IReadOnlyList<LeadConversationDto> Items, int TotalUnread);

/// <summary>
/// Hàng thô repository trả về cho hộp thư; service tính tên hiển thị và còn nhắn được hay không.
/// <paramref name="StudentName"/> đã bỏ trống khi phía trung tâm không được xem tên (không chia sẻ liên hệ / dữ liệu đã ẩn).
/// </summary>
public sealed record LeadConversationRow(
    Lead Lead,
    string CenterName,
    string? StudentName,
    string? LastContent,
    string? LastAttachmentUrl,
    string? LastStatus,
    DateTime? LastAt,
    bool LastFromCenter,
    int UnreadCount);
