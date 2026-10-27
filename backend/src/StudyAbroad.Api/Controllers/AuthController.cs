using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
    {
        private const string AccessTokenCookie = "usas_access_token";

        // [AC-1 / USAS-12] Đăng ký tài khoản: tạo user unverified và gửi mã OTP kích hoạt qua Email.
        // Trùng email/SĐT vẫn trả cùng thông báo (chống dò tài khoản), chủ hộp thư nhận mail báo riêng.
        [HttpPost("register")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => StatusCode(StatusCodes.Status201Created, new { message = result.Message }),
                _ => ValidationProblem(result.Message),
            };
        }

        // [USAS-12] Xác thực mã OTP kích hoạt email đăng ký
        [HttpPost("verify-email")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
        {
            var result = await auth.VerifyEmailAsync(request, ct);
            if (result.Error == AuthError.None && result.Response != null)
            {
                // Tự động đăng nhập qua HttpOnly Cookie sau khi xác thực thành công
                return SetSessionCookie(result.Response);
            }

            return result.Error switch
            {
                AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không hợp lệ"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [USAS-12] Gửi lại mã OTP (verify_email, reset_password)
        [HttpPost("resend-otp")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> ResendOtp(ResendOtpRequest request, CancellationToken ct)
        {
            var result = await auth.ResendOtpAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [AC-2 / USAS-362] Đăng nhập: Email + Password (Tự động tạm khóa sau 5 lần sai liên tiếp)
        [HttpPost("login")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await auth.LoginAsync(request, ip, userAgent, ct);
            if (result.Error == AuthError.None && result.Response != null)
            {
                return SetSessionCookie(result.Response);
            }

            return result.Error switch
            {
                AuthError.Locked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị khóa bởi Admin"),
                AuthError.TempLocked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị tạm khóa"),
                AuthError.Unverified => Problem(result.Message, statusCode: StatusCodes.Status403Forbidden, title: "Email chưa xác thực"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        // [USAS-12] Yêu cầu đặt lại mật khẩu (Gửi mã OTP qua Email)
        [HttpPost("forgot-password")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
        {
            var result = await auth.ForgotPasswordAsync(request, ct);
            return Ok(new { message = result.Message });
        }

        // [USAS-12] Đặt lại mật khẩu mới với mã OTP (và thu hồi toàn bộ session cũ)
        [HttpPost("reset-password")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        {
            var result = await auth.ResetPasswordAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không hợp lệ"),
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // [UC-06] Đổi mật khẩu khi đã đăng nhập: giữ phiên hiện tại, đăng xuất các thiết bị khác, gửi mail thông báo
        [Authorize]
        [HttpPost("change-password")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var result = await auth.ChangePasswordAsync(userId.Value, GetRawToken(), request, ct);
            if (result.Error == AuthError.TempLocked)
            {
                // Mọi phiên đã bị thu hồi: xóa luôn cookie trên trình duyệt này
                Response.Cookies.Delete(AccessTokenCookie, CreateCookieOptions(expires: null));
            }

            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.TempLocked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị tạm khóa"),
                // 400 thay vì 401: sai mật khẩu hiện tại không có nghĩa phiên hết hạn
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
            };
        }

        // [AC-3] Đăng xuất: thu hồi phiên trên server (đánh dấu IsRevoked = true) và xóa HttpOnly Cookie
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var rawToken = GetRawToken();
            if (!string.IsNullOrEmpty(rawToken))
            {
                await auth.LogoutAsync(rawToken, ct);
            }

            // Xóa HttpOnly Cookie trên trình duyệt
            Response.Cookies.Delete(AccessTokenCookie, CreateCookieOptions(expires: null));

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

        // Sửa họ tên và số điện thoại của chính mình (email đăng nhập không đổi được)
        [Authorize]
        [HttpPut("me")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> UpdateMe(UpdateProfileRequest request, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var result = await auth.UpdateProfileAsync(userId.Value, request, ct);
            return result.Error == AuthError.None
                ? Ok(new { message = result.Message, user = result.User })
                : Problem(result.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        // Token của phiên hiện tại: lấy từ HttpOnly Cookie, không có thì từ header Authorization
        private string GetRawToken()
        {
            var rawToken = Request.Cookies[AccessTokenCookie];
            if (!string.IsNullOrEmpty(rawToken))
            {
                return rawToken;
            }

            var authHeader = Request.Headers.Authorization.ToString();
            return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader.Trim();
        }

        // [Chống XSS]: token chỉ nằm trong HttpOnly Cookie, body không chứa token
        private IActionResult SetSessionCookie(AuthResponse response)
        {
            Response.Cookies.Append(AccessTokenCookie, response.AccessToken, CreateCookieOptions(response.ExpiresAt));
            return Ok(new { expiresAt = response.ExpiresAt, user = response.User });
        }

        // [Bảo mật] Ngoài Development cookie luôn Secure, không phụ thuộc Request.IsHttps (sau proxy có thể sai)
        private CookieOptions CreateCookieOptions(DateTimeOffset? expires) => new()
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expires,
            Path = "/"
        };
    }
}
