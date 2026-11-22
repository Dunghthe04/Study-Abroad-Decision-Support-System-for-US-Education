using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Community;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/community")]
public sealed class CommunityController(ICommunityService community) : ControllerBase
{
    [HttpGet("messages")]
    [ProducesResponseType(typeof(IReadOnlyList<CommunityMessageDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] DateTime? before,
        [FromQuery] Guid? beforeId,
        [FromQuery] DateTime? after,
        [FromQuery] Guid? afterId,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        RunAsync(async userId => Ok(await community.ListMessagesAsync(userId, before, beforeId, after, afterId, take, ct)));

    [HttpGet("restriction")]
    [ProducesResponseType(typeof(CommunityRestrictionDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Restriction(CancellationToken ct) =>
        RunAsync(async userId => Ok(await community.RestrictionStatusAsync(userId, ct)));

    [HttpPost("messages")]
    [EnableRateLimiting(CommunityPostRateLimit.PolicyName)]
    [ProducesResponseType(typeof(CommunityMessageDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Send([FromBody] SendCommunityMessageRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            var dto = await community.SendAsync(userId, request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        });

    [HttpPost("messages/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Report(Guid id, [FromBody] ReportCommunityMessageRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            await community.ReportAsync(userId, id, request, ct);
            return NoContent();
        });

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
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
