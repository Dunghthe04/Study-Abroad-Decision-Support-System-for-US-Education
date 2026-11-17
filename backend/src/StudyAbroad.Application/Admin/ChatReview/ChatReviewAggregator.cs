using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>Truy vấn tin hỏi và ngữ cảnh trên chat_messages, dùng cho chi tiết một câu của đợt duyệt mẫu.</summary>
public static class ChatReviewAggregator
{
    /// <summary>Tin hỏi của học sinh (nội dung và meta_json).</summary>
    public static IQueryable<ChatReviewQuestionRow> QuestionDetail(IQueryable<ChatMessage> messages, Guid questionMessageId) =>
        messages
            .Where(m => m.Id == questionMessageId)
            .Select(m => new ChatReviewQuestionRow(m.Id, m.CreatedAt, m.Content, m.MetaJson));

    /// <summary>
    /// Các tin trước thời điểm beforeUtc trong cùng phiên, chỉ của học sinh và trợ lý (bỏ dòng báo lỗi hệ thống), gần nhất trước, tối đa take tin.
    /// Dịch vụ đảo lại theo thứ tự thời gian. excludeMessageId loại chính tin hỏi để không lặp.
    /// </summary>
    public static IQueryable<ChatReviewContextRow> ContextBefore(
        IQueryable<ChatMessage> messages, Guid sessionId, DateTime beforeUtc, Guid excludeMessageId, int take) =>
        messages
            .Where(m => m.ChatSessionId == sessionId && m.CreatedAt < beforeUtc && m.Id != excludeMessageId
                        && (m.Role == "user" || m.Role == "assistant"))
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(take)
            .Select(m => new ChatReviewContextRow(m.Id, m.Role, m.CreatedAt, m.Content));
}
