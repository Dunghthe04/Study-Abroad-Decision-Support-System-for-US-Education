using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;

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
                AuthError.EmailTaken or AuthError.PhoneTaken => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
                _ => ValidationProblem(result.Message),
            };
        }

        // [AC-2] Đăng nhập: Email + Password, trả về JWT Token và UserDto (thiết lập HttpOnly Cookie)
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await auth.LoginAsync(request, ip, userAgent, ct);
            if (result.Error == AuthError.None && result.Response != null)
            {
                // [Chống XSS]: Lưu access token vào HttpOnly Cookie
                Response.Cookies.Append("usas_access_token", result.Response.AccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = result.Response.ExpiresAt,
                    Path = "/"
                });
            }

            return result.Error switch
            {
                AuthError.None => Ok(result.Response),
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked),
                _ => Problem(result.Message, statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        // [AC-3] Đăng xuất: thu hồi phiên trên server (đánh dấu IsRevoked = true) và xóa HttpOnly Cookie
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var rawToken = Request.Cookies["usas_access_token"];
            if (string.IsNullOrEmpty(rawToken))
            {
                var authHeader = Request.Headers.Authorization.ToString();
                rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader["Bearer ".Length..].Trim()
                    : authHeader.Trim();
            }

            if (!string.IsNullOrEmpty(rawToken))
            {
                await auth.LogoutAsync(rawToken, ct);
            }

            // Xóa HttpOnly Cookie trên trình duyệt
            Response.Cookies.Delete("usas_access_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            return Ok(new { message = "Đăng xuất thành công." });
        }

        // [AC-4] Lấy thông tin cá nhân của người dùng hiện tại từ Token Claims
        // Chống IDOR: trích xuất UserId từ Token đã ký số (User.GetUserId()), không nhận ID từ client param.
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await auth.GetMeAsync(userId.Value, ct);
            return user != null ? Ok(user) : NotFound();
        }
    }
}
