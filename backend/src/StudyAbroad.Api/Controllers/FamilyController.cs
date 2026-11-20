using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-126..131] Gói Gia đình: mời, hủy mời, chấp nhận / từ chối, danh sách thành viên, gỡ, rời.
/// Lỗi nghiệp vụ là BillingRuleException → <c>{ code, message }</c> (BillingRuleExceptionFilter). Token lời mời nằm trong body, không nằm trong URL (log).
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Student + "," + UserRoles.Parent)]
[Route("api/v1/family")]
public class FamilyController(IFamilyService family) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await family.GetAsync(userId, User.GetRole() ?? string.Empty, DateTime.UtcNow, ct));
    }

    [HttpPost("invites")]
    public async Task<IActionResult> Invite(FamilyInviteRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.InviteAsync(userId, request.Email ?? string.Empty, request.SeatRole ?? string.Empty, DateTime.UtcNow, ct);
        return Accepted(new { message = "Đã gửi lời mời" });   // luôn như nhau dù email có tài khoản hay không
    }

    [HttpPost("invites/{inviteId:guid}/resend")]
    public async Task<IActionResult> Resend(Guid inviteId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.ResendInviteAsync(userId, inviteId, DateTime.UtcNow, ct);
        return Accepted(new { message = "Đã gửi lại lời mời" });
    }

    [HttpDelete("invites/{inviteId:guid}")]
    public async Task<IActionResult> CancelInvite(Guid inviteId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.CancelInviteAsync(userId, inviteId, DateTime.UtcNow, ct);
        return NoContent();
    }

    /// <summary>Trang chấp nhận mở được khi chưa đăng nhập; token 256 bit là bí mật nên không cần giới hạn tốc độ (429 của limiter còn sai dạng { code, message }).</summary>
    [AllowAnonymous]
    [HttpPost("invites/lookup")]
    [ProducesResponseType(typeof(FamilyInviteLookupDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupInvite(FamilyTokenRequest request, CancellationToken ct) =>
        Ok(await family.LookupAsync(request.Token ?? string.Empty, DateTime.UtcNow, ct));

    [HttpPost("invites/accept")]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvite(FamilyTokenRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await family.AcceptAsync(userId, User.GetRole() ?? string.Empty, request.Token ?? string.Empty, DateTime.UtcNow, ct));
    }

    [HttpPost("invites/decline")]
    public async Task<IActionResult> DeclineInvite(FamilyTokenRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.DeclineAsync(userId, request.Token ?? string.Empty, DateTime.UtcNow, ct);
        return NoContent();
    }

    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid memberId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.RemoveMemberAsync(userId, memberId, DateTime.UtcNow, ct);
        return NoContent();
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        await family.LeaveAsync(userId, DateTime.UtcNow, ct);
        return NoContent();
    }
}

public sealed record FamilyInviteRequest(string? Email, string? SeatRole);

public sealed record FamilyTokenRequest(string? Token);
