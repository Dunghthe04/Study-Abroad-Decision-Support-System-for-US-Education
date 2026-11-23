using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Tài khoản trung tâm chưa đăng nhập: [UC-64] tự đăng ký và [UC-91] xác thực email bằng OTP.
/// Phần đổi liên lạc khi đã đăng nhập nằm ở <see cref="CenterPortalController"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/center-accounts")]
public class CenterAccountController(ICenterAccountService accounts) : ControllerBase
{
    /// <summary>[UC-64] Trung tâm tự đăng ký tài khoản; mã OTP gửi tới email để kích hoạt.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public async Task<IActionResult> Register(RegisterCenterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(request, ct, HttpContext.Connection.RemoteIpAddress?.ToString());
        return result.Error switch
        {
            AuthError.None => StatusCode(StatusCodes.Status201Created, new { message = result.Message, email = request.Email }),
            _ => ValidationProblem(result.Message),
        };
    }

    /// <summary>[UC-91] Xác thực email trung tâm qua mã OTP 6 số; thành công thì đăng nhập luôn qua HttpOnly Cookie.</summary>
    [HttpPost("verify-email")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public async Task<IActionResult> VerifyEmail(VerifyCenterEmailRequest request, CancellationToken ct)
    {
        var result = await accounts.VerifyEmailAsync(request, ct);
        if (result.Error == AuthError.None && result.Response != null)
        {
            // [Chống XSS]: token chỉ nằm trong HttpOnly Cookie, body không chứa token
            var response = result.Response;
            SessionCookies.AppendSession(HttpContext, response.AccessToken, response.ExpiresAt, response.RefreshToken, response.SessionExpiresAt);
            return Ok(new { expiresAt = response.ExpiresAt, user = response.User });
        }

        return result.Error switch
        {
            AuthError.OtpInvalid => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Mã OTP không hợp lệ"),
            _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
