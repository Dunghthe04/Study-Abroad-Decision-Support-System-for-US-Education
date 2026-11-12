using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>
/// Truy vấn lấy NỘI DUNG của một câu trong đợt duyệt, viết trên IQueryable (EF dịch thành SQL, test chạy trên bộ nhớ).
/// Tin trả lời của đợt là tin đã trả lời được, KHÔNG có trạng thái xử lý, nên không dùng được ChatReviewAggregator.ReplyDetail (chỉ cho tin của hàng đợi).
/// Câu hỏi và ngữ cảnh dùng lại truy vấn của hàng đợi.
/// </summary>
public static class ReviewItemQuery
{
    /// <summary>
    /// Tin trả lời của trợ lý đã trả lời được, kèm nội dung và chủ phiên (để lấy ngữ cảnh). Chỉ nhận tin trợ lý đã trả lời được:
    /// mã của tin học sinh, tin thuộc hàng đợi hay dòng báo lỗi cho kết quả rỗng, nên không thể dùng nó để đọc tuỳ ý các cuộc chat.
    /// </summary>
    public static IQueryable<ChatReviewReplyRow> AnsweredReply(IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, Guid messageId) =>
        messages
            .Where(m => m.Id == messageId && m.Role == "assistant" && m.Outcome == ChatOutcomes.Answered)
            .Join(sessions, m => m.ChatSessionId, s => s.Id, (m, s) => new { Message = m, s.UserId })
            .Select(x => new ChatReviewReplyRow(
                x.Message.Id,
                x.Message.ChatSessionId,
                x.UserId,
                x.Message.CreatedAt,
                x.Message.Role,
                x.Message.Outcome ?? "",
                x.Message.ReviewStatus ?? "",
                x.Message.Content,
                x.Message.MetaJson,
                x.Message.ReplyToMessageId));
}
