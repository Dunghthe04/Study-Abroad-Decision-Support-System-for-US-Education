using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Notifications;

public interface INotificationRepository
{
    /// <summary>[UC-110] Thông báo của <paramref name="userId"/>, mới nhất trước. Luôn lọc theo UserId.</summary>
    Task<PagedResult<Notification>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đánh dấu đã đọc một thông báo CỦA userId. Trả false nếu không tồn tại hoặc của người khác.</summary>
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);
}
