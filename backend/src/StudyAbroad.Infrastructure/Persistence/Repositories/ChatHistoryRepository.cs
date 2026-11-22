using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor.History;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lịch sử chat của chính người dùng, chỉ đọc nên không theo dõi thay đổi (AsNoTracking).</summary>
public class ChatHistoryRepository(AppDbContext db) : IChatHistoryRepository
{
    public async Task<IReadOnlyList<ChatSessionRow>> SessionsAsync(
        Guid userId, ChatHistoryCursor? after, int take, CancellationToken ct = default) =>
        await ChatHistoryQuery.Sessions(db.ChatSessions.AsNoTracking(), db.ChatMessages.AsNoTracking(), userId, after, take).ToListAsync(ct);

    public async Task<(bool Found, string? Title)> SessionTitleAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var match = await db.ChatSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => new { s.Title })
            .FirstOrDefaultAsync(ct);
        return match is null ? (false, null) : (true, match.Title);
    }

    public async Task<IReadOnlyList<ChatHistoryMessageRow>> MessagesAsync(
        Guid sessionId, ChatHistoryCursor? before, int take, CancellationToken ct = default) =>
        await ChatHistoryQuery.Messages(db.ChatMessages.AsNoTracking(), sessionId, before, take).ToListAsync(ct);
}
