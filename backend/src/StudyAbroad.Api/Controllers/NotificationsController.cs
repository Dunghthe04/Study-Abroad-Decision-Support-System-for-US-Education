using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-110] Thông báo trong hệ thống. Mọi vai trò đã đăng nhập; chỉ thấy và thao tác trên thông báo của chính mình
/// (thông báo của người khác trả 404 như không tồn tại).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await notifications.GetMineAsync(userId, unreadOnly, page, pageSize, ct));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(new UnreadCountDto(await notifications.GetUnreadCountAsync(userId, ct)));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return await notifications.MarkReadAsync(userId, id, ct)
            ? NoContent()
            : NotFound(new { message = "Không tìm thấy thông báo." });
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(new { updated = await notifications.MarkAllReadAsync(userId, ct) });
    }
}
