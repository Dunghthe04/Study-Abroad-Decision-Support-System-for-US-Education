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
        // [AC-1 / USAS-12] Đăng ký tài khoản: tạo user unverified và gửi mã OTP kích hoạt qua Email
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => StatusCode(StatusCodes.Status201Created, new { message = result.Message, user = result.User }),
                AuthError.EmailTaken or AuthError.PhoneTaken => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
                _ => ValidationProblem(result.Message),
            };
        }

        // [USAS-12] Xác thực mã OTP kích hoạt email đăng ký
        [HttpPost("verify-email")]
        public async Task<ActionResult<AuthResponse>> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
        {
            var result = await auth.VerifyEmailAsync(request, ct);
            if (result.Error == AuthError.None && result.Response != null)
            {
                // Tự động đăng nhập qua HttpOnly Cookie sau khi xác thực thành công
                Response.Cookies.Append("usas_access_token", result.Response.AccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = result.Response.ExpiresAt,
                    Path = "/"
                });
                return Ok(result.Response);
            }

            return result.Error switch
            {
                AuthError.OtpExpired => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP đã hết hạn"),
                AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không đúng"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [USAS-12] Gửi lại mã OTP (verify_email, reset_password, unlock_account)
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp(ResendOtpRequest request, CancellationToken ct)
        {
            var result = await auth.ResendOtpAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [AC-2 / USAS-362] Đăng nhập: Email + Password (Tự động tạm khóa sau 5 lần sai liên tiếp)
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
                return Ok(result.Response);
            }

            return result.Error switch
            {
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị khóa bởi Admin"),
                AuthError.TempLocked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị tạm khóa"),
                AuthError.Unverified => Problem(result.Message, statusCode: StatusCodes.Status403Forbidden, title: "Email chưa xác thực"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        // [USAS-12 / USAS-17] Mở khóa tài khoản tạm thời (sau khi bị tạm khóa vì nhập sai 5 lần)
        [HttpPost("unlock-account")]
        public async Task<IActionResult> UnlockAccount(UnlockAccountRequest request, CancellationToken ct)
        {
            var result = await auth.UnlockAccountAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Không thể tự mở khóa"),
                AuthError.OtpExpired => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP đã hết hạn"),
                AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không đúng"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [USAS-12] Yêu cầu đặt lại mật khẩu (Gửi mã OTP qua Email)
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
        {
            var result = await auth.ForgotPasswordAsync(request, ct);
            return Ok(new { message = result.Message });
        }

        // [USAS-12] Đặt lại mật khẩu mới với mã OTP (và thu hồi toàn bộ session cũ)
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        {
            var result = await auth.ResetPasswordAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked),
                AuthError.OtpExpired => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP đã hết hạn"),
                AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không đúng"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
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
