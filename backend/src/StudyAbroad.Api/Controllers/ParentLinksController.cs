using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers
{
    // [Q11] Liên kết phụ huynh–học sinh và sự đồng ý xử lý dữ liệu trẻ em (Luật BVDLCN 91/2025 Điều 24)
    [ApiController]
    [Route("api/v1/parent-links")]
    public class ParentLinksController(IParentLinkService parentLinks) : ControllerBase
    {
        // Trang xác nhận đọc thông tin lời mời trước khi đăng nhập. Token trong body để không nằm trong log URL.
        [AllowAnonymous]
        [HttpPost("invite/lookup")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> LookupInvite(InviteTokenRequest request, CancellationToken ct) =>
            ToResult(await parentLinks.GetInviteAsync(request.Token, ct));

        [Authorize(Roles = UserRoles.Parent)]
        [HttpPost("invite/accept")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> AcceptInvite(InviteTokenRequest request, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Unauthorized();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return ToResult(await parentLinks.AcceptAsync(userId, request.Token, ip, ct));
        }

        [Authorize(Roles = UserRoles.Student)]
        [HttpPost("resend")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> Resend(CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Unauthorized();

            return ToResult(await parentLinks.ResendAsync(userId, ct));
        }

        [Authorize(Roles = UserRoles.Student)]
        [HttpPut("parent-email")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> ChangeParentEmail(ChangeParentEmailRequest request, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Unauthorized();

            return ToResult(await parentLinks.ChangeParentEmailAsync(userId, request.ParentEmail, ct));
        }

        [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
        [HttpPost("{id:guid}/revoke")]
        public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Unauthorized();

            return ToResult(await parentLinks.RevokeAsync(userId, id, ct));
        }

        [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Unauthorized();

            return ToResult(await parentLinks.GetMineAsync(userId, ct));
        }

        private IActionResult ToResult<T>(ParentLinkResult<T> result) => result.Error switch
        {
            ParentLinkError.None => Ok(new { message = result.Message, data = result.Value }),
            ParentLinkError.NotFound => Problem(result.Message, statusCode: StatusCodes.Status404NotFound),
            ParentLinkError.Expired => Problem(result.Message, statusCode: StatusCodes.Status410Gone),
            ParentLinkError.Forbidden => Problem(result.Message, statusCode: StatusCodes.Status403Forbidden),
            ParentLinkError.Conflict => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
            ParentLinkError.TooManyRequests => Problem(result.Message, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
        };
    }
}
