using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Notifications;

/// <summary>[UC-110] Xem, đánh dấu đã đọc thông báo của chính mình và tạo thông báo cho các UC khác.</summary>
public class NotificationService(INotificationRepository repository, ILogger<NotificationService>? logger = null) : INotificationService
{
    public const int MaxPageSize = 50;
    public const int MaxTitleLength = 256;
    public const int MaxBodyLength = 2000;
    public const int MaxLinkLength = 512;

    public async Task<PagedResult<NotificationDto>> GetMineAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var result = await repository.ListAsync(userId, unreadOnly, safePage, safeSize, ct);
        return new PagedResult<NotificationDto>(
            result.Items.Select(NotificationMapper.ToDto).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        repository.CountUnreadAsync(userId, ct);

    public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) =>
        notificationId == Guid.Empty ? Task.FromResult(false) : repository.MarkReadAsync(userId, notificationId, ct);

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) =>
        repository.MarkAllReadAsync(userId, ct);

    public async Task NotifyAsync(
        Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default)
    {
        try
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(title))
                return;

            await repository.AddAsync(new Notification
            {
                UserId = userId,
                Type = type.Trim(),
                Title = Truncate(title.Trim(), MaxTitleLength)!,
                Body = Truncate(body?.Trim(), MaxBodyLength),
                // Chỉ nhận đường dẫn nội bộ để thông báo không dẫn người dùng ra trang ngoài
                LinkUrl = IsInternalLink(linkUrl) ? Truncate(linkUrl!.Trim(), MaxLinkLength) : null,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Không tạo được thông báo {Type} cho người dùng {UserId}", type, userId);
        }
    }

    private static bool IsInternalLink(string? link) =>
        !string.IsNullOrWhiteSpace(link) && link.StartsWith('/') && !link.StartsWith("//");

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
