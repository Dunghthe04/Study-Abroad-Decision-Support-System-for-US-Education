using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-58 2a, UC-03 3d] Administrator đặt lại xác thực hai lớp cho Moderator. Tách khỏi AdminUsersController
/// nhưng dùng chung tiền tố route quản trị tài khoản.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/users")]
public class AdminTwoFactorController(ITwoFactorResetService resets) : ControllerBase
{
    [HttpPost("{id:guid}/reset-2fa")]
    public async Task<IActionResult> ResetTwoFactor(Guid id, TwoFactorResetRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await resets.ResetModeratorAsync(id, request, actorId, ip, userAgent, ct);
        return result.Error switch
        {
            TwoFactorResetError.None => NoContent(),
            TwoFactorResetError.NotFound => NotFound(),
            _ => ValidationProblem(result.Message),
        };
    }
}
