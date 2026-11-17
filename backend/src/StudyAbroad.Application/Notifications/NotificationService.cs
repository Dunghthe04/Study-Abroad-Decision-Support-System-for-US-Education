using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Notifications;

/// <summary>[UC-110] Một thông báo trong ứng dụng. <paramref name="LinkUrl"/> là màn hình liên quan để mở tới (có thể trống).</summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>[UC-110] Một trang thông báo của người dùng hiện tại kèm số thông báo chưa đọc (tính trên toàn bộ, không theo trang).</summary>
public record NotificationListDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>[UC-110] Đọc / ghi bảng notifications. Mọi truy vấn đều lọc theo userId để mỗi người chỉ thấy thông báo của mình.</summary>
public interface INotificationRepository
{
    Task<PagedResult<Notification>> GetPageAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Thông báo theo id của đúng người dùng (thông báo của người khác coi như không có), được theo dõi để sửa.</summary>
    Task<Notification?> GetForUpdateAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task SaveAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Đánh dấu đã đọc mọi thông báo chưa đọc của người dùng. Trả về số thông báo vừa đổi.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<NotificationListDto> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tạo một thông báo cho một người dùng. Dùng bởi các chức năng khác (duyệt hồ sơ, yêu cầu liên hệ...).</summary>
    Task NotifyAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2134 / UC-110] Người dùng xem danh sách thông báo của mình, đánh dấu đã đọc và mở tới màn hình liên quan.
/// Mỗi người chỉ thấy thông báo của mình: userId luôn lấy từ token, không nhận từ client.
/// </summary>
public class NotificationService(INotificationRepository notifications) : INotificationService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxTypeLength = 32;
    public const int MaxTitleLength = 256;
    public const int MaxBodyLength = 2000;
    public const int MaxLinkLength = 512;

    public async Task<NotificationListDto> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var paged = await notifications.GetPageAsync(userId, unreadOnly, safePage, safePageSize, ct);
        var unread = await notifications.CountUnreadAsync(userId, ct);

        return new NotificationListDto(paged.Items.Select(ToDto).ToList(), safePage, safePageSize, paged.TotalCount, unread);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        notifications.CountUnreadAsync(userId, ct);

    /// <summary>Đánh dấu một thông báo đã đọc. Đã đọc rồi thì giữ nguyên thời điểm đọc lần đầu. Thông báo của người khác trả "không tìm thấy".</summary>
    public async Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("Mã thông báo không hợp lệ.");

        var notification = await notifications.GetForUpdateAsync(userId, notificationId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thông báo.");

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await notifications.SaveAsync(notification, ct);
        }

        return ToDto(notification);
    }

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) =>
        notifications.MarkAllReadAsync(userId, DateTime.UtcNow, ct);

    public Task NotifyAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Người nhận thông báo không hợp lệ.");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Thông báo phải có loại và tiêu đề.");

        return notifications.AddRangeAsync([new Notification
        {
            UserId = userId,
            Type = Truncate(type.Trim(), MaxTypeLength)!,
            Title = Truncate(title.Trim(), MaxTitleLength)!,
            Body = Truncate(string.IsNullOrWhiteSpace(body) ? null : body.Trim(), MaxBodyLength),
            LinkUrl = SafeLink(linkUrl),
        }], ct);
    }

    /// <summary>Chỉ nhận đường dẫn nội bộ (bắt đầu bằng một dấu "/") để thông báo không dẫn người dùng ra trang ngoài.</summary>
    public static string? SafeLink(string? linkUrl)
    {
        var link = linkUrl?.Trim();
        if (string.IsNullOrEmpty(link) || link.Length > MaxLinkLength) return null;
        return link.StartsWith('/') && !link.StartsWith("//") && !link.Contains('\\') ? link : null;
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type, n.Title, n.Body, SafeLink(n.LinkUrl), n.ReadAt is not null, n.CreatedAt, n.ReadAt);
}
