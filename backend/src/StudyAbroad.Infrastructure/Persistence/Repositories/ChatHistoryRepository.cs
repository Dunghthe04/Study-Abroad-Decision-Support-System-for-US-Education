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
        var match = await ChatHistoryQuery.Active(db.ChatSessions.AsNoTracking(), sessionId, userId)
            .Select(s => new { s.Title })
            .FirstOrDefaultAsync(ct);
        return match is null ? (false, null) : (true, match.Title);
    }

    // Cập nhật thẳng ở database (một câu UPDATE có điều kiện chủ phiên và chưa xóa): không đọc phiên về rồi mới sửa,
    // nên hai yêu cầu cùng lúc không ghi đè nhau. Không đổi UpdatedAt: đổi tên hay xóa không phải một lần trao đổi
    public async Task<bool> RenameAsync(Guid sessionId, Guid userId, string title, CancellationToken ct = default) =>
        await ChatHistoryQuery.Active(db.ChatSessions, sessionId, userId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Title, title), ct) > 0;

    public async Task<bool> SoftDeleteAsync(Guid sessionId, Guid userId, DateTime deletedAtUtc, CancellationToken ct = default) =>
        await ChatHistoryQuery.Active(db.ChatSessions, sessionId, userId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.DeletedAt, deletedAtUtc), ct) > 0;

    public async Task<IReadOnlyList<ChatHistoryMessageRow>> MessagesAsync(
        Guid sessionId, ChatHistoryCursor? before, int take, CancellationToken ct = default) =>
        await ChatHistoryQuery.Messages(db.ChatMessages.AsNoTracking(), sessionId, before, take).ToListAsync(ct);
}
