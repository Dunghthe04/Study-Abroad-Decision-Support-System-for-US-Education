using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Một lượt chat thô lấy từ database: tin trả lời (hoặc dòng lỗi hệ thống) cùng meta_json của tin hỏi nó.
/// UsedFallback: lượt có lần gọi AI nào dùng đường dự phòng (ai_calls.fallback_used).
/// </summary>
public record AiQualityTurnRow(
    DateTime CreatedAt, string? Outcome, string? ReplyMetaJson, string? QuestionMetaJson, bool UsedFallback);

/// <summary>
/// Truy vấn lấy các lượt chat trong khoảng thời gian cho báo cáo chất lượng AI (UC-90). Viết trên IQueryable như AiUsageAggregator:
/// EF dịch thành SQL (một câu, không gọi lại từng dòng), còn test chạy trên danh sách trong bộ nhớ.
/// </summary>
public static class AiQualityQuery
{
    /// <summary>
    /// Mỗi lượt là một dòng của hệ thống (Role khác "user"): tin trả lời của trợ lý, hoặc dòng lỗi hệ thống khi advisor sập hẳn.
    /// Phải gồm dòng lỗi hệ thống, nếu không các lượt hỏng nặng nhất sẽ biến mất khỏi tỉ lệ lỗi. Khoảng là [fromUtc, toUtcExclusive).
    /// Tin hỏi nối qua ReplyToMessageId; lượt không có tin hỏi thì QuestionMetaJson là null.
    /// </summary>
    public static IQueryable<AiQualityTurnRow> Turns(
        IQueryable<ChatMessage> messages, IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        from reply in messages
        where reply.Role != "user" && reply.CreatedAt >= fromUtc && reply.CreatedAt < toUtcExclusive
        join question in messages on reply.ReplyToMessageId equals (Guid?)question.Id into asked
        from question in asked.DefaultIfEmpty()
        orderby reply.CreatedAt
        select new AiQualityTurnRow(
            reply.CreatedAt,
            reply.Outcome,
            reply.MetaJson,
            question == null ? null : question.MetaJson,
            calls.Any(c => c.ChatMessageId == reply.Id && c.FallbackUsed));
}
