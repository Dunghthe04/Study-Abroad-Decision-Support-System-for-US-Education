using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>1.5b: phụ huynh làm thay con managed (con chưa có tài khoản).
/// Nằm dưới /api/v1/auth để trình duyệt gửi kèm cookie refresh (cookie đó chỉ gửi tới đường dẫn này).</summary>
[ApiController]
[Route("api/v1/auth/acting")]
public class ActingController(ActingService acting) : ControllerBase
{
    // Vào làm thay: phiên của trình duyệt chuyển sang con, phiên phụ huynh được cất lại để thoát thì lấy về.
    // 404 khi không phải con managed của mình (không nói con thật hay con của người khác).
    [Authorize(Roles = UserRoles.Parent)]
    [HttpPost("{childId:guid}")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public async Task<IActionResult> Start(Guid childId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId)
            return Unauthorized();

        var parentRefreshToken = Request.Cookies[SessionCookies.RefreshToken];
        var result = await acting.StartAsync(parentId, childId, SessionCookies.ReadAccessToken(Request), parentRefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

        switch (result.Outcome)
        {
            case ActingStartOutcome.Ok:
                SessionCookies.AppendSession(HttpContext, result.AccessToken!, result.AccessExpiresAt, result.RefreshToken, result.SessionExpiresAt);
                SessionCookies.AppendParentRefresh(HttpContext, parentRefreshToken!, result.ParentSessionExpiresAt);
                return Ok(new { expiresAt = result.AccessExpiresAt });
            case ActingStartOutcome.NotFound:
                return NotFound(new { message = "Không tìm thấy con này trong danh sách con chưa có tài khoản của bạn." });
            default:
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
        }
    }

    // Thoát làm thay: đóng phiên của con, trả lại phiên phụ huynh.
    // Không cần [Authorize]: phiên làm thay đã hết hạn hoặc bị thu hồi thì vẫn thoát được về phụ huynh.
    [HttpDelete]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        var result = await acting.StopAsync(SessionCookies.ReadAccessToken(Request), Request.Cookies[SessionCookies.RefreshToken],
            Request.Cookies[SessionCookies.ParentRefreshToken], ct);

        switch (result?.Outcome)
        {
            case null:
                return BadRequest(new { message = "Bạn không đang làm thay con." });
            case RefreshOutcome.Ok:
                SessionCookies.AppendSession(HttpContext, result.AccessToken!, result.AccessExpiresAt, result.RefreshToken, result.SessionExpiresAt);
                SessionCookies.ClearParentRefresh(HttpContext);
                return Ok(new { expiresAt = result.AccessExpiresAt });
            case RefreshOutcome.Retry:
                return Conflict(new { message = "Phiên vừa được gia hạn, vui lòng thử lại." });
            default:
                SessionCookies.ClearSession(HttpContext);
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
        }
    }

    // Đang làm thay ai: frontend dùng để hiện thanh "Bạn đang làm thay ..." và nút thoát.
    [Authorize]
    [HttpGet]
    public IActionResult Get() =>
        ActingSessionGuard.ReadActingParentId(User) is null
            ? Ok(new { acting = false })
            : Ok(new { acting = true, childId = User.GetUserId(), childName = User.FindFirstValue(ClaimTypes.Name) });
}
