using StudyAbroad.Domain.Entities;
// Application.Advisor cũng có record ChatMessage (tin gửi advisor) và nó che tên này trong namespace con: gọi rõ thực thể của bảng chat_messages
using ChatMessageEntity = StudyAbroad.Domain.Entities.ChatMessage;

namespace StudyAbroad.Application.Advisor.History;

/// <summary>
/// Truy vấn lịch sử chat của chính người dùng. Viết trên IQueryable: EF dịch thành SQL, test chạy trên danh sách trong bộ nhớ.
/// Mọi truy vấn đều lọc theo chủ phiên ở phía database, không lọc sau khi tải về. Lọc và xếp trên thực thể trước rồi mới dựng
/// record kết quả ở bước cuối (EF không dịch được điều kiện đặt lên thuộc tính của record đã dựng).
/// </summary>
public static class ChatHistoryQuery
{
    /// <summary>
    /// Các phiên của userId có ít nhất một tin đã lưu (phiên không có tin, ví dụ tạo trước khi bật lưu tin, không có gì để xem lại),
    /// xếp theo lần trao đổi gần nhất giảm dần rồi mã giảm dần. after: chỉ lấy các phiên đứng SAU con trỏ trong thứ tự đó.
    /// Lần trao đổi gần nhất là UpdatedAt (ChatService ghi mỗi lượt), phiên chưa có thì lấy CreatedAt.
    /// </summary>
    public static IQueryable<ChatSessionRow> Sessions(
        IQueryable<ChatSession> sessions, IQueryable<ChatMessageEntity> messages, Guid userId, ChatHistoryCursor? after, int take)
    {
        var owned = sessions.Where(s => s.UserId == userId && messages.Any(m => m.ChatSessionId == s.Id));
        if (after is { } c)
            owned = owned.Where(s =>
                (s.UpdatedAt ?? s.CreatedAt) < c.At
                || ((s.UpdatedAt ?? s.CreatedAt) == c.At && s.Id.CompareTo(c.Id) < 0));
        return owned
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Take(take)
            .Select(s => new ChatSessionRow(
                s.Id,
                s.Title,
                s.CreatedAt,
                s.UpdatedAt ?? s.CreatedAt,
                messages.Count(m => m.ChatSessionId == s.Id)));
    }

    /// <summary>
    /// Các tin của một phiên, MỚI NHẤT trước (bên gọi đảo lại để hiện cũ trước mới sau), lấy take tin. before: chỉ lấy các tin cũ hơn con trỏ.
    /// Không kiểm chủ phiên ở đây: bên gọi phải kiểm trước (IChatHistoryRepository.SessionTitleAsync).
    /// </summary>
    public static IQueryable<ChatHistoryMessageRow> Messages(
        IQueryable<ChatMessageEntity> messages, Guid sessionId, ChatHistoryCursor? before, int take)
    {
        var rows = messages.Where(m => m.ChatSessionId == sessionId);
        if (before is { } c)
            rows = rows.Where(m => m.CreatedAt < c.At || (m.CreatedAt == c.At && m.Id.CompareTo(c.Id) < 0));
        return rows
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .Select(m => new ChatHistoryMessageRow(m.Id, m.Role, m.Content, m.CreatedAt, m.SourcesJson));
    }
}
