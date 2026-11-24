using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Community;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/admin/forum")]
public sealed class AdminForumController(ICommunityService community) : ControllerBase
{
    [HttpGet("reports")]
    [ProducesResponseType(typeof(PagedResult<CommunityReportDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListReports(
        [FromQuery] string? status = "open",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        RunAsync(async _ => Ok(await community.ListReportsAsync(status, page, pageSize, ct)));

    [HttpPost("reports/{id:guid}/hide")]
    public Task<IActionResult> Hide(Guid id, CancellationToken ct) =>
        RunAsync(async handlerId =>
        {
            await community.HideFromReportAsync(handlerId, id, ct);
            return Ok(new { message = "Đã ẩn tin nhắn." });
        });

    [HttpPost("messages/{id:guid}/hide")]
    public Task<IActionResult> HideMessage(Guid id, CancellationToken ct) =>
        RunAsync(async handlerId =>
        {
            await community.HideMessageAsync(handlerId, id, ct);
            return Ok(new { message = "Đã ẩn tin nhắn." });
        });

    [HttpPost("reports/{id:guid}/dismiss")]
    public Task<IActionResult> Dismiss(Guid id, CancellationToken ct) =>
        RunAsync(async handlerId =>
        {
            await community.DismissReportAsync(handlerId, id, ct);
            return Ok(new { message = "Đã bỏ báo cáo." });
        });

    [HttpPost("users/{userId:guid}/restrict")]
    public Task<IActionResult> Restrict(Guid userId, [FromBody] RestrictCommunityUserRequest request, CancellationToken ct) =>
        RunAsync(async handlerId =>
        {
            await community.RestrictUserAsync(handlerId, userId, request, ct);
            return Ok(new { message = "Đã hạn chế quyền chat." });
        });

    [HttpPost("users/{userId:guid}/unrestrict")]
    public Task<IActionResult> Unrestrict(Guid userId, CancellationToken ct) =>
        RunAsync(async handlerId =>
        {
            await community.UnrestrictUserAsync(handlerId, userId, ct);
            return Ok(new { message = "Đã gỡ hạn chế quyền chat." });
        });

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        try
        {
            return await action(userId);
        }
        catch (CommunityRuleException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
        }
    }
}
