using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Notifications;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-2134 / UC-110] Người dùng đã đăng nhập xem thông báo của mình, đánh dấu đã đọc. Mỗi người chỉ thấy thông báo của mình
/// (userId lấy từ token).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>Một trang thông báo, mới nhất trước, kèm số chưa đọc. unreadOnly = true thì chỉ lấy thông báo chưa đọc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = NotificationService.DefaultPageSize,
        CancellationToken ct = default) =>
        RunAsync(async userId => Ok(await service.GetMineAsync(userId, unreadOnly, page, pageSize, ct)));

    /// <summary>Số thông báo chưa đọc, dùng cho huy hiệu trên thanh điều hướng.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> GetUnreadCount(CancellationToken ct) =>
        RunAsync(async userId => Ok(new { unreadCount = await service.CountUnreadAsync(userId, ct) }));

    /// <summary>Đánh dấu một thông báo đã đọc. Thông báo của người khác trả 404.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        RunAsync(async userId => Ok(await service.MarkReadAsync(userId, id, ct)));

    /// <summary>Đánh dấu đã đọc toàn bộ thông báo của người dùng hiện tại.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        RunAsync(async userId => Ok(new { updated = await service.MarkAllReadAsync(userId, ct) }));

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return await action(userId);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }
}
