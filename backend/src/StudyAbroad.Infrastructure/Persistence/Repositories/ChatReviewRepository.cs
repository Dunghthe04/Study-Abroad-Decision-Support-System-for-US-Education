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

    public async Task<IReadOnlyList<ChatReviewCountRow>> CountsAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) =>
        await ChatReviewAggregator.Counts(db.ChatMessages.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);
}
