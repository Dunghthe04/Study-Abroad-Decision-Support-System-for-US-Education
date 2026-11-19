namespace StudyAbroad.Application.Notifications;

/// <summary>
/// [Chat realtime] Báo trình duyệt của người dùng biết vừa có thông báo mới (toast và chuông) ngay khi nó được tạo,
/// thay vì chờ làm mới định kỳ. Cài đặt ở tầng Api (SignalR). Lỗi đẩy không được làm hỏng việc tạo thông báo.
/// </summary>
public interface INotificationPusher
{
    Task PushAsync(Guid userId, string title, string? body, string? linkUrl, CancellationToken ct = default);
}
