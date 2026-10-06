using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Auth;
using System.Security.Claims;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(IAuthService auth) : ControllerBase
    {
        // [AC-1] Đăng ký tài khoản: Email + Password, lưu SĐT liên hệ nếu có
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => StatusCode(StatusCodes.Status201Created, result.User),
                AuthError.EmailTaken => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
                _ => ValidationProblem(result.Message),
            };
        }

        // [AC-2] Đăng nhập: Email + Password, trả về JWT Token và UserDto
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await auth.LoginAsync(request, ip, userAgent, ct);
            return result.Error switch
            {
                AuthError.None => Ok(result.Response),
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked),
                _ => Problem(result.Message, statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        // [AC-3] Đăng xuất: thu hồi phiên trên server (đánh dấu IsRevoked = true)
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var authHeader = Request.Headers.Authorization.ToString();
            var rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader.Trim();

            await auth.LogoutAsync(rawToken, ct);
            return Ok(new { message = "Đăng xuất thành công." });
        }

        // Lấy thông tin cá nhân của người dùng hiện tại từ Token Claims
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var user = await auth.GetMeAsync(userId, ct);
            return user != null ? Ok(user) : NotFound();
        }
    }
}
