using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>
/// Truy vấn các câu trả lời có thể bốc, viết trên IQueryable: EF dịch thành SQL, test chạy trên danh sách trong bộ nhớ.
/// Chỉ chọn mã và meta_json, không chọn cột nội dung chat.
/// </summary>
public static class ReviewCandidateQuery
{
    /// <summary>
    /// Tin trợ lý đã trả lời được (outcome answered), không có trạng thái xử lý (không thuộc hàng đợi), có tin hỏi đi kèm,
    /// trong khoảng [fromUtc, toUtcExclusive); null là không giới hạn đầu đó.
    /// </summary>
    public static IQueryable<ReviewCandidate> Candidates(IQueryable<ChatMessage> messages, DateTime? fromUtc, DateTime? toUtcExclusive)
    {
        var query = messages.Where(m =>
            m.Role == "assistant" && m.Outcome == ChatOutcomes.Answered && m.ReviewStatus == null && m.ReplyToMessageId != null);
        if (fromUtc is { } from)
            query = query.Where(m => m.CreatedAt >= from);
        if (toUtcExclusive is { } to)
            query = query.Where(m => m.CreatedAt < to);
        return query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new ReviewCandidate(m.Id, m.MetaJson));
    }
}
