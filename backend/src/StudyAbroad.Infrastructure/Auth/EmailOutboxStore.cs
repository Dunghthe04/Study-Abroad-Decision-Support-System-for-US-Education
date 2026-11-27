using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>Đọc ghi bảng email_outbox. Tách interface để test luồng gửi không cần Postgres.</summary>
public interface IEmailOutboxStore
{
    /// <summary>Ghi mail mới; có dedupe_key thì xóa các mail pending cùng khóa trong cùng transaction.</summary>
    Task AddAsync(EmailOutbox row, CancellationToken ct);

    /// <summary>Lấy một mail tới giờ gửi (cũ trước), đổi sang sending và khóa tới <paramref name="lockedUntil"/>.
    /// Dòng sending đã quá hạn khóa cũng được lấy lại. Null khi không còn mail nào.</summary>
    Task<EmailOutbox?> ClaimNextAsync(DateTime nowUtc, DateTime lockedUntil, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Trả mail về pending, hẹn giờ gửi lại.</summary>
    Task RescheduleAsync(Guid id, int attempts, DateTime nextAttemptAt, string? lastError, CancellationToken ct);
}

/// <summary>
/// Mỗi lần gọi mở một DbContext riêng: mail không dính vào change tracker hay transaction của request đang gọi,
/// và dùng được từ singleton (sender, job nền).
/// </summary>
public sealed class EfEmailOutboxStore(IServiceScopeFactory scopeFactory) : IEmailOutboxStore
{
    public async Task AddAsync(EmailOutbox row, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (row.DedupeKey is not null)
        {
            await db.EmailOutbox
                .Where(x => x.DedupeKey == row.DedupeKey && x.Status == EmailOutboxStatuses.Pending)
                .ExecuteDeleteAsync(ct);
        }
        db.EmailOutbox.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<EmailOutbox?> ClaimNextAsync(DateTime nowUtc, DateTime lockedUntil, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // SKIP LOCKED: nhiều api chạy cùng lúc không lấy trùng một dòng
        var rows = await db.EmailOutbox.FromSqlInterpolated($"""
            UPDATE app.email_outbox
               SET status = {EmailOutboxStatuses.Sending}, locked_until = {lockedUntil}, updated_at = {nowUtc}
             WHERE id = (
                   SELECT id FROM app.email_outbox
                    WHERE (status = {EmailOutboxStatuses.Pending} AND next_attempt_at <= {nowUtc})
                       OR (status = {EmailOutboxStatuses.Sending} AND locked_until < {nowUtc})
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
            RETURNING *
            """).AsNoTracking().ToListAsync(ct);
        return rows.FirstOrDefault();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.EmailOutbox.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task RescheduleAsync(Guid id, int attempts, DateTime nextAttemptAt, string? lastError, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        await db.EmailOutbox.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, EmailOutboxStatuses.Pending)
            .SetProperty(x => x.Attempts, attempts)
            .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
            .SetProperty(x => x.LockedUntil, (DateTime?)null)
            .SetProperty(x => x.LastError, lastError)
            .SetProperty(x => x.UpdatedAt, now), ct);
    }
}
