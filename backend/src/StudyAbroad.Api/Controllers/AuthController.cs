using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(IAuthService auth, SessionRefreshService refresh) : ControllerBase
    {

        // [AC-1 / USAS-12] Đăng ký tài khoản: tạo user unverified và gửi mã OTP kích hoạt qua Email.
        // Trùng email/SĐT vẫn trả cùng thông báo (chống dò tài khoản), chủ hộp thư nhận mail báo riêng.
        [HttpPost("register")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(request, ct, HttpContext.Connection.RemoteIpAddress?.ToString());
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
                AuthError.PendingDeletion => PendingDeletionProblem(result),
                _ => Problem(result.Message, statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        // [UC-80] Đăng nhập vào tài khoản đang chờ xóa: 403 kèm code, ngày xóa vĩnh viễn và mã khôi phục
        private ObjectResult PendingDeletionProblem(AuthResult result)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, StatusCodes.Status403Forbidden, title: "Tài khoản đang chờ xóa", detail: result.Message);
            problem.Extensions["code"] = "account_pending_deletion";
            problem.Extensions["deletionDate"] = result.PendingDeletion?.DeletionDate;
            problem.Extensions["restoreToken"] = result.PendingDeletion?.RestoreToken;
            return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
        }

        // [UC-80] Khôi phục tài khoản đang chờ xóa bằng mã khôi phục nhận lúc đăng nhập; thành công thì đăng nhập luôn
        [HttpPost("restore")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public async Task<IActionResult> Restore(RestoreAccountRequest request, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await auth.RestoreAccountAsync(request, ip, userAgent, ct);
            if (result.Error == AuthError.None && result.Response != null)
                return SetSessionCookie(result.Response);

            return Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Không khôi phục được tài khoản");
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
                SessionCookies.ClearSession(HttpContext);
            }

            return result.Error switch
            {
                AuthError.None => Ok(new { message = result.Message }),
                AuthError.TempLocked => Problem(result.Message, statusCode: StatusCodes.Status423Locked, title: "Tài khoản bị tạm khóa"),
                // 400 thay vì 401: sai mật khẩu hiện tại không có nghĩa phiên hết hạn
                _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
            };
        }

        // Gia hạn phiên: đổi refresh token cũ lấy cặp access token + refresh token mới.
        // 409 = tab khác vừa gia hạn trước, gửi lại request là được. 401 = phải đăng nhập lại.
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var result = await refresh.RefreshAsync(Request.Cookies[SessionCookies.RefreshToken], ct);
            switch (result.Outcome)
            {
                case RefreshOutcome.Ok:
                    SessionCookies.AppendSession(HttpContext, result.AccessToken!, result.AccessExpiresAt, result.RefreshToken, result.SessionExpiresAt);
                    return Ok(new { expiresAt = result.AccessExpiresAt });
                case RefreshOutcome.Retry:
                    return Conflict(new { message = "Phiên vừa được gia hạn, vui lòng thử lại." });
                default:
                    // 1.5b: giữ cookie phụ huynh, phiên làm thay hết hạn thì DELETE /auth/acting vẫn về được phiên phụ huynh
                    SessionCookies.ClearSession(HttpContext, includeParent: false);
                    return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
        }

        // [AC-3] Đăng xuất: thu hồi phiên trên server (đánh dấu IsRevoked = true) và xóa HttpOnly Cookie.
        // Không cần [Authorize]: access token hết hạn rồi vẫn đăng xuất được nhờ refresh token.
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var rawToken = GetRawToken();
            if (!string.IsNullOrEmpty(rawToken))
            {
                await auth.LogoutAsync(rawToken, ct);
            }
            await refresh.RevokeAsync(Request.Cookies[SessionCookies.RefreshToken], ct);
            // 1.5b: đăng xuất lúc đang làm thay thì đóng luôn phiên phụ huynh
            await refresh.RevokeAsync(Request.Cookies[SessionCookies.ParentRefreshToken], ct);

            // Xóa HttpOnly Cookie trên trình duyệt
            SessionCookies.ClearSession(HttpContext);

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

        // [UC-80] Người dùng yêu cầu xóa tài khoản: Ẩn danh hóa PII, thu hồi phiên và dọn dẹp cookie
        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMe(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập." });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await auth.AnonymizeUserAccountAsync(userId.Value, ip, userAgent, ct);
            if (result.Error == AuthError.RoleNotAllowed)
            {
                return Problem(result.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            if (result.Error != AuthError.None)
            {
                return Problem(result.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            // Xóa cookie phiên làm việc
            SessionCookies.ClearSession(HttpContext);

            return Ok(new { message = result.Message });
        }

        // Token của phiên hiện tại: lấy từ HttpOnly Cookie, không có thì từ header Authorization
        private string GetRawToken() => SessionCookies.ReadAccessToken(Request);

        // [Chống XSS]: token chỉ nằm trong HttpOnly Cookie, body không chứa token
        private IActionResult SetSessionCookie(AuthResponse response)
        {
            SessionCookies.AppendSession(HttpContext, response.AccessToken, response.ExpiresAt, response.RefreshToken, response.SessionExpiresAt);
            return Ok(new { expiresAt = response.ExpiresAt, user = response.User });
        }
    }
}
