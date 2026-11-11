using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.AiUsage;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Gộp ở database (GROUP BY), chỉ đọc nên không theo dõi thay đổi (AsNoTracking).</summary>
public class AiUsageReportRepository(AppDbContext db) : IAiUsageReportRepository
{
    public async Task<IReadOnlyList<AiUsageGroupRow>> ByKindAndPurposeAsync(
        DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default) =>
        await AiUsageAggregator.ByKindAndPurpose(db.AiCalls.AsNoTracking(), fromUtc, toUtcExclusive, userId).ToListAsync(ct);

    public async Task<IReadOnlyList<AiUsageGroupRow>> ByDayAndKindAsync(
        DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default) =>
        await AiUsageAggregator.ByDayAndKind(db.AiCalls.AsNoTracking(), fromUtc, toUtcExclusive, userId).ToListAsync(ct);

    public async Task<(IReadOnlyList<AiUsageUserRow> Rows, int TotalUsers)> ByUserPageAsync(
        DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default)
    {
        var calls = db.AiCalls.AsNoTracking();
        var total = await AiUsageAggregator.UsersInWindow(calls, fromUtc, toUtcExclusive).CountAsync(ct);
        var rows = await AiUsageAggregator.ByUserPage(calls, fromUtc, toUtcExclusive, skip, take).ToListAsync(ct);
        return (rows, total);
    }

    public async Task<(IReadOnlyList<AiUsageTurnRow> Rows, int TotalTurns)> TurnsPageAsync(
        Guid userId, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default)
    {
        var total = await AiUsageAggregator.TurnIds(db.ChatMessages.AsNoTracking(), db.ChatSessions.AsNoTracking(), userId, fromUtc, toUtcExclusive)
            .CountAsync(ct);
        var rows = await AiUsageAggregator.TurnsPage(
                db.ChatMessages.AsNoTracking(), db.ChatSessions.AsNoTracking(), db.AiCalls.AsNoTracking(),
                userId, fromUtc, toUtcExclusive, skip, take)
            .ToListAsync(ct);
        return (rows, total);
    }

    public Task<AiUsageTurnHeader?> FindTurnAsync(Guid messageId, CancellationToken ct = default) =>
        AiUsageAggregator.TurnHeader(db.ChatMessages.AsNoTracking(), db.ChatSessions.AsNoTracking(), messageId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AiUsageCallDetailDto>> CallsOfTurnAsync(Guid messageId, CancellationToken ct = default) =>
        await AiUsageAggregator.CallsOfTurn(db.AiCalls.AsNoTracking(), messageId).ToListAsync(ct);

    public async Task<IReadOnlyList<AiUsageUserInfo>> UsersByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new AiUsageUserInfo(u.Id, u.FullName, u.Email))
                .ToListAsync(ct);
}
