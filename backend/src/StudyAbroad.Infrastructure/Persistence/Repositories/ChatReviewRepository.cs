using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ChatReview;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; chỉ đọc nên không theo dõi thay đổi (AsNoTracking).</summary>
public class ChatReviewRepository(AppDbContext db) : IChatReviewRepository
{
    public async Task<(IReadOnlyList<ChatReviewRow> Rows, int Total)> QueuePageAsync(
        string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive, int skip, int take, CancellationToken ct = default)
    {
        var messages = db.ChatMessages.AsNoTracking();
        var sessions = db.ChatSessions.AsNoTracking();
        var total = await ChatReviewAggregator.QueueIds(messages, status, outcome, fromUtc, toUtcExclusive).CountAsync(ct);
        var rows = await ChatReviewAggregator.QueuePage(messages, sessions, status, outcome, fromUtc, toUtcExclusive, skip, take).ToListAsync(ct);
        return (rows, total);
    }

    public async Task<ChatReviewDetailRows?> FindDetailAsync(Guid messageId, int contextCount, CancellationToken ct = default)
    {
        var messages = db.ChatMessages.AsNoTracking();
        var reply = await ChatReviewAggregator.ReplyDetail(messages, db.ChatSessions.AsNoTracking(), messageId).FirstOrDefaultAsync(ct);
        if (reply is null)
            return null;

        var question = reply.QuestionMessageId is { } questionId
            ? await ChatReviewAggregator.QuestionDetail(messages, questionId).FirstOrDefaultAsync(ct)
            : null;
        // Ngữ cảnh là các tin trước câu hỏi (nếu không có tin hỏi thì trước chính tin trả lời)
        var context = await ChatReviewAggregator.ContextBefore(
                messages, reply.SessionId, question?.CreatedAt ?? reply.CreatedAt, question?.MessageId ?? reply.MessageId, contextCount)
            .ToListAsync(ct);
        return new ChatReviewDetailRows(reply, question, context);
    }

    public async Task<IReadOnlyList<ChatReviewCountRow>> CountsAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) =>
        await ChatReviewAggregator.Counts(db.ChatMessages.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);
}
