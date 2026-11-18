using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Notifications;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Trả false (controller trả 404) nếu thông báo không phải của người dùng.</summary>
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [UC-110] Tạo thông báo cho một người dùng. Không bao giờ ném lỗi: thông báo hỏng không được làm hỏng thao tác chính.
    /// </summary>
    Task NotifyAsync(Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default);
}
