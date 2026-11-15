using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>
/// Truy vấn hàng đợi câu cần xem trên chat_messages. Viết trên IQueryable: EF dịch thành SQL (lọc, sắp, phân trang ở database),
/// test chạy trên danh sách trong bộ nhớ. Hàng đợi gồm các tin có review_status (trợ lý hoặc dòng lỗi hệ thống), không gồm tin của học sinh.
/// fromUtc, toUtcExclusive: null nghĩa là không giới hạn đầu đó (hàng đợi là việc tồn đọng nên mặc định không giới hạn ngày).
/// Lọc và phân trang làm trên chat_messages TRƯỚC, rồi mới nối với phiên (lấy chủ phiên) và tin hỏi: nối ít dòng, và dùng kiểu ẩn danh
/// vì EF không dịch được các điều kiện đi qua một kiểu tự định nghĩa trong phép nối.
/// </summary>
public static class ChatReviewAggregator
{
    // Các tin có trạng thái xử lý (không phải của học sinh), qua bộ lọc trạng thái, kết quả và khoảng thời gian
    private static IQueryable<ChatMessage> Reviewable(
        IQueryable<ChatMessage> messages, string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive)
    {
        var query = messages.Where(m => m.ReviewStatus != null && m.Role != "user");
        if (status is not null)
            query = query.Where(m => m.ReviewStatus == status);
        if (outcome is not null)
            query = query.Where(m => m.Outcome == outcome);
        if (fromUtc is { } from)
            query = query.Where(m => m.CreatedAt >= from);
        if (toUtcExclusive is { } to)
            query = query.Where(m => m.CreatedAt < to);
        return query;
    }

    /// <summary>
    /// Một trang hàng đợi, mới nhất trước, kèm chủ phiên và meta_json của tin trả lời và của tin hỏi (để dịch vụ lấy mã cố định).
    /// status và outcome null nghĩa là không lọc theo đó.
    /// </summary>
    public static IQueryable<ChatReviewRow> QueuePage(
        IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions,
        string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive, int skip, int take) =>
        Reviewable(messages, status, outcome, fromUtc, toUtcExclusive)
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip(skip)
            .Take(take)
            .Join(sessions, m => m.ChatSessionId, s => s.Id, (m, s) => new { Message = m, s.UserId })
            .GroupJoin(messages, x => x.Message.ReplyToMessageId, q => q.Id, (x, questions) => new { x.Message, x.UserId, questions })
            .SelectMany(x => x.questions.DefaultIfEmpty(), (x, question) => new { x.Message, x.UserId, question })
            // Sắp lại trên cột của tin nhắn, TRƯỚC khi dựng bản ghi: sắp sau khi dựng bản ghi bằng hàm khởi tạo thì EF có thể không dịch được,
            // và phép nối trái không bảo đảm giữ thứ tự của trang con bên trong
            .OrderByDescending(x => x.Message.CreatedAt)
            .ThenBy(x => x.Message.Id)
            .Select(x => new ChatReviewRow(
                x.Message.Id,
                x.Message.ChatSessionId,
                x.UserId,
                x.Message.CreatedAt,
                x.Message.Role,
                x.Message.Outcome ?? "",
                x.Message.ReviewStatus ?? "",
                x.Message.ReplyToMessageId,
                x.Message.MetaJson,
                x.question == null ? null : x.question.MetaJson));

    /// <summary>
    /// Tin của hàng đợi theo mã (có trạng thái xử lý, không phải của học sinh). Chỗ duy nhất chọn tin được phép đổi review_status:
    /// mã của tin đã trả lời được hoặc của học sinh cho kết quả rỗng nên không đổi được.
    /// </summary>
    public static IQueryable<ChatMessage> QueueMessage(IQueryable<ChatMessage> messages, Guid messageId) =>
        messages.Where(m => m.Id == messageId && m.ReviewStatus != null && m.Role != "user");

    /// <summary>
    /// Tin trả lời cần xem kèm nội dung và chủ phiên. CHỈ tin thuộc hàng đợi (có trạng thái xử lý, không phải của học sinh):
    /// mã tin khác (đã trả lời được, tin của học sinh) cho kết quả rỗng, nên không thể dùng nó để đọc tuỳ ý các cuộc chat.
    /// </summary>
    public static IQueryable<ChatReviewReplyRow> ReplyDetail(IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, Guid messageId) =>
        messages
            .Where(m => m.Id == messageId && m.ReviewStatus != null && m.Role != "user")
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

    /// <summary>Mã các tin của hàng đợi sau khi lọc, để đếm tổng số cho phân trang.</summary>
    public static IQueryable<Guid> QueueIds(
        IQueryable<ChatMessage> messages, string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive) =>
        Reviewable(messages, status, outcome, fromUtc, toUtcExclusive).Select(m => m.Id);

    /// <summary>Số câu theo (trạng thái xử lý, kết quả) trong khoảng.</summary>
    public static IQueryable<ChatReviewCountRow> Counts(IQueryable<ChatMessage> messages, DateTime? fromUtc, DateTime? toUtcExclusive) =>
        Reviewable(messages, null, null, fromUtc, toUtcExclusive)
            .GroupBy(m => new { m.ReviewStatus, m.Outcome })
            .Select(g => new ChatReviewCountRow(g.Key.ReviewStatus ?? "", g.Key.Outcome ?? "", g.Count()));
}
