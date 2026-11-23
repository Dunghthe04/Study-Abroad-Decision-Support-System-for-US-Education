using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-104] Xác nhận Điều khoản sử dụng và Chính sách bảo mật của tài khoản đang đăng nhập.
/// GET cho biết còn phải đồng ý bản nào; POST ghi đồng ý theo đúng phiên bản đã đọc (409 policy_changed nếu bản vừa đổi).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/policy-acceptance")]
public class PolicyAcceptanceController(IPolicyAcceptanceService acceptance) : ControllerBase
{
    /// <summary>Tình trạng đồng ý của mình. Admin và moderator nhận danh sách rỗng, mustAccept = false.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PolicyAcceptanceStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyAcceptanceStatusDto>> GetStatus(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await acceptance.GetStatusAsync(userId, User.GetRole() ?? string.Empty, ct));
    }

    /// <summary>
    /// Đồng ý các tài liệu gửi lên, mỗi mục kèm phiên bản đã đọc. Học sinh, phụ huynh, trung tâm mới gọi được (người khác nhận 403).
    /// 400 nếu dữ liệu sai; 409 policy_changed nếu admin vừa công bố bản khác, khi đó không ghi gì cả.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent},{UserRoles.Center}")]
    [ProducesResponseType(typeof(PolicyAcceptanceStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyAcceptanceStatusDto>> Accept([FromBody] AcceptPoliciesRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return Ok(await acceptance.AcceptAsync(userId, User.GetRole() ?? string.Empty, request, ip, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PolicyRuleException ex)
        {
            return Conflict(new { code = ex.Code, error = ex.Message });
        }
    }
}
