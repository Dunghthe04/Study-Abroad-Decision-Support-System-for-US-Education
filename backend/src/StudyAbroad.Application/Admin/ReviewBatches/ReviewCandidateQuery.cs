using StudyAbroad.Application.Admin.AiQuality;
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
    /// sessions: có thì bỏ các tin thuộc cuộc trò chuyện học sinh đã xóa (không bốc vào đợt duyệt để người duyệt đọc lại).
    /// </summary>
    public static IQueryable<ReviewCandidate> Candidates(
        IQueryable<ChatMessage> messages, DateTime? fromUtc, DateTime? toUtcExclusive, IQueryable<ChatSession>? sessions = null) =>
        Eligible(messages, fromUtc, toUtcExclusive, sessions)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new ReviewCandidate(m.Id, m.MetaJson));

    /// <summary>
    /// Cùng các câu như Candidates, kèm chủ phiên (ngày sinh, cờ phụ huynh vận hành) và thời điểm của lượt, để áp luật tuổi:
    /// người dưới 16 chỉ được bốc khi phụ huynh đã đồng ý (xem AiQualityContentGate). Cùng thứ tự với Candidates.
    /// Chủ phiên nối qua chat_sessions; tài khoản đã xóa thì không có dòng users (ngày sinh null).
    /// </summary>
    public static IQueryable<ReviewCandidateOwner> WithOwner(
        IQueryable<ChatMessage> messages, DateTime? fromUtc, DateTime? toUtcExclusive,
        IQueryable<ChatSession> sessions, IQueryable<User> users) =>
        from m in Eligible(messages, fromUtc, toUtcExclusive, sessions)
        join session in sessions on m.ChatSessionId equals session.Id
        join user in users on session.UserId equals user.Id into owners
        from owner in owners.DefaultIfEmpty()
        orderby m.CreatedAt, m.Id
        select new ReviewCandidateOwner(
            m.Id, m.MetaJson, m.CreatedAt, session.UserId,
            owner == null ? (DateOnly?)null : owner.DateOfBirth,
            owner != null && owner.Status == UserStatuses.Managed);

    /// <summary>
    /// Giữ các câu của người được phép cho admin đọc nội dung: từ 16 tuổi trở lên (hoặc chưa khai ngày sinh, hoặc tài khoản phụ huynh vận hành),
    /// hoặc dưới 16 mà phụ huynh đã đồng ý bản chính sách có mục đích đánh giá chất lượng. Cùng luật với mẫu hội thoại (AiQualityContentGate),
    /// tuổi tính tại thời điểm của lượt chat. Hàm thuần: consentVersionsOf trả các phiên bản đã đồng ý của một chủ phiên.
    /// </summary>
    public static IReadOnlyList<ReviewCandidate> AllowedToRead(
        IEnumerable<ReviewCandidateOwner> rows, Func<Guid, IReadOnlyList<string?>> consentVersionsOf,
        string minPolicyVersion = ConsentPurposes.QualityReviewMinPolicyVersion) =>
        rows
            .Where(r => AiQualityContentGate.CanReadContent(
                new AiQualityCandidate(r.MessageId, r.CreatedAt, r.OwnerDateOfBirth, consentVersionsOf(r.OwnerId), r.OwnerIsParentOperated),
                minPolicyVersion))
            .Select(r => new ReviewCandidate(r.MessageId, r.ReplyMetaJson))
            .ToList();

    private static IQueryable<ChatMessage> Eligible(
        IQueryable<ChatMessage> messages, DateTime? fromUtc, DateTime? toUtcExclusive, IQueryable<ChatSession>? sessions)
    {
        var query = messages.Where(m =>
            m.Role == "assistant" && m.Outcome == ChatOutcomes.Answered && m.ReviewStatus == null && m.ReplyToMessageId != null);
        if (sessions is not null)
            query = query.Where(m => sessions.Any(s => s.Id == m.ChatSessionId && s.DeletedAt == null));
        if (fromUtc is { } from)
            query = query.Where(m => m.CreatedAt >= from);
        if (toUtcExclusive is { } to)
            query = query.Where(m => m.CreatedAt < to);
        return query;
    }
}
