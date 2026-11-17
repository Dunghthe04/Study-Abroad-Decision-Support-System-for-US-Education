using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-110] Đọc / ghi app.notifications. Mọi truy vấn đều lọc theo user_id.</summary>
public class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<PagedResult<Notification>> GetPageAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Notification>(items, page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

    public Task<Notification?> GetForUpdateAsync(Guid userId, Guid notificationId, CancellationToken ct = default) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

    public async Task SaveAsync(Notification notification, CancellationToken ct = default)
    {
        notification.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc, CancellationToken ct = default) =>
        db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.ReadAt, readAtUtc)
                .SetProperty(n => n.UpdatedAt, readAtUtc), ct);

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
    }
}
