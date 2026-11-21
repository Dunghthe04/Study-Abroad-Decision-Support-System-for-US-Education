using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// Đọc lượt chat cho danh sách mẫu hội thoại (UC-90), chỉ đọc nên không theo dõi thay đổi (AsNoTracking).
/// Bước một (CandidatesAsync) KHÔNG đọc cột nội dung chat nào; bước hai (RowsAsync) chỉ đọc nội dung của các lượt được hỏi.
/// </summary>
public class AiQualitySampleRepository(AppDbContext db) : IAiQualitySampleRepository
{
    /// <summary>Một dòng của bước một: chỉ mã, thời điểm và thông tin chủ phiên, KHÔNG có cột nội dung nào.</summary>
    public record CandidateRow(Guid Id, DateTime CreatedAt, Guid OwnerId, DateOnly? Birth, bool Managed);

    /// <summary>Một dòng của bước hai: nội dung thô của một lượt, tin hỏi của nó và họ tên chủ phiên.</summary>
    public record ReplyRow(
        Guid Id, DateTime CreatedAt, string? Outcome, string? MetaJson, string Content, Guid ChatSessionId,
        Guid? QuestionId, DateTime? QuestionAt, string? QuestionContent, string? OwnerName);

    /// <summary>
    /// Mỗi lượt là dòng của hệ thống (Role khác "user"), như báo cáo. Chủ phiên nối qua chat_sessions; tài khoản đã xóa thì không có dòng users.
    /// Mới nhất trước, bằng nhau thì theo mã để các trang không lặp hay sót. Không đọc cột nội dung chat nào.
    /// </summary>
    public static IQueryable<CandidateRow> CandidateRows(AppDbContext db, DateTime fromUtc, DateTime toUtcExclusive, string outcome) =>
        from reply in db.ChatMessages.AsNoTracking()
        where reply.Role != "user" && reply.Outcome == outcome && reply.CreatedAt >= fromUtc && reply.CreatedAt < toUtcExclusive
        join session in db.ChatSessions.AsNoTracking() on reply.ChatSessionId equals session.Id
        join user in db.Users.AsNoTracking() on session.UserId equals user.Id into owners
        from owner in owners.DefaultIfEmpty()
        orderby reply.CreatedAt descending, reply.Id
        select new CandidateRow(
            reply.Id,
            reply.CreatedAt,
            session.UserId,
            owner == null ? (DateOnly?)null : owner.DateOfBirth,
            owner != null && owner.Status == UserStatuses.Managed);

    /// <summary>Nội dung thô của đúng các lượt được hỏi, kèm tin hỏi (nối qua reply_to_message_id) và họ tên chủ phiên.</summary>
    public static IQueryable<ReplyRow> ReplyRows(AppDbContext db, IReadOnlyCollection<Guid> messageIds) =>
        from reply in db.ChatMessages.AsNoTracking()
        where messageIds.Contains(reply.Id)
        join asked in db.ChatMessages.AsNoTracking() on reply.ReplyToMessageId equals (Guid?)asked.Id into questions
        from question in questions.DefaultIfEmpty()
        join session in db.ChatSessions.AsNoTracking() on reply.ChatSessionId equals session.Id
        join user in db.Users.AsNoTracking() on session.UserId equals user.Id into owners
        from owner in owners.DefaultIfEmpty()
        select new ReplyRow(
            reply.Id,
            reply.CreatedAt,
            reply.Outcome,
            reply.MetaJson,
            reply.Content,
            reply.ChatSessionId,
            question == null ? (Guid?)null : question.Id,
            question == null ? (DateTime?)null : question.CreatedAt,
            question == null ? null : question.Content,
            owner == null ? null : owner.FullName);

    public async Task<IReadOnlyList<AiQualityCandidate>> CandidatesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, string outcome, CancellationToken ct = default)
    {
        var rows = await CandidateRows(db, fromUtc, toUtcExclusive, outcome).ToListAsync(ct);

        if (rows.Count == 0)
            return [];

        // Sự đồng ý của phụ huynh: consents.user_id là CON (người được xử lý dữ liệu), granted_by_user_id là phụ huynh.
        // Phụ huynh rút đồng ý thì dòng đó đổi thành granted = false nên chỉ lấy dòng còn hiệu lực
        var ownerIds = rows.Select(r => r.OwnerId).Distinct().ToList();
        var consents = (await db.Consents.AsNoTracking()
                .Where(c => ownerIds.Contains(c.UserId) && c.Purpose == ConsentPurposes.ChildDataProcessing && c.Granted)
                .Select(c => new { c.UserId, c.PolicyVersion })
                .ToListAsync(ct))
            .ToLookup(c => c.UserId, c => c.PolicyVersion);

        return rows
            .Select(r => new AiQualityCandidate(r.Id, r.CreatedAt, r.Birth, consents[r.OwnerId].ToList(), r.Managed))
            .ToList();
    }

    public async Task<IReadOnlyList<AiQualitySampleRow>> RowsAsync(
        IReadOnlyCollection<Guid> messageIds, int contextMessages, CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
            return [];

        var replies = await ReplyRows(db, messageIds).ToListAsync(ct);

        var result = new List<AiQualitySampleRow>();
        foreach (var r in replies)
        {
            // Ngữ cảnh là các tin ngay trước CÂU HỎI (không phải trước câu trả lời); cũ trước, mới sau
            var context = contextMessages <= 0
                ? []
                : (await ChatReviewAggregator.ContextBefore(
                        db.ChatMessages.AsNoTracking(), r.ChatSessionId, r.QuestionAt ?? r.CreatedAt, r.QuestionId ?? r.Id, contextMessages)
                    .ToListAsync(ct))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.MessageId)
                .Select(c => new AiQualitySampleMessage(c.Role, c.Content))
                .ToList();
            result.Add(new AiQualitySampleRow(r.Id, r.CreatedAt, r.Outcome, r.MetaJson, r.QuestionContent, r.Content, context, r.OwnerName));
        }
        return result;
    }
}
