using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-2080 / UC-58] Đề nghị tạm ngừng trung tâm bị gỡ tin lặp lại: Moderator gửi, Admin quyết định.
/// </summary>
[ApiController]
[Route("api/v1/admin/center-suspension-proposals")]
public class AdminCenterSuspensionProposalsController(ICenterSuspensionProposalService service) : ControllerBase
{
    /// <summary>Các trung tâm có từ 3 tin bị gỡ trở lên, đủ điều kiện để Moderator đề nghị tạm ngừng.</summary>
    [HttpGet("repeat-removal-centers")]
    [Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(IReadOnlyList<RepeatRemovalCenterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRepeatRemovalCenters(CancellationToken ct) =>
        Ok(await service.GetRepeatRemovalCentersAsync(ct));

    /// <summary>Danh sách đề nghị, mới nhất trước. status: pending (mặc định) | approved | rejected | all.</summary>
    [HttpGet]
    [Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(PagedResult<CenterSuspensionProposalDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        RunAsync(async _ => Ok(await service.ListAsync(status, page, pageSize, ct)));

    /// <summary>Moderator gửi đề nghị tạm ngừng một trung tâm (kèm lý do).</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Moderator)]
    [ProducesResponseType(typeof(CenterSuspensionProposalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Propose([FromBody] CreateSuspensionProposalRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            var dto = await service.ProposeAsync(userId, request.StudyCenterId, request.Reason, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        });

    /// <summary>Admin quyết định: approve true thì tạm ngừng trung tâm, false thì từ chối đề nghị.</summary>
    [HttpPost("{id:guid}/decide")]
    [Authorize(Roles = UserRoles.Admin)]
    [ProducesResponseType(typeof(CenterSuspensionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Decide(Guid id, [FromBody] DecideSuspensionProposalRequest request, CancellationToken ct) =>
        RunAsync(async userId => Ok(await service.DecideAsync(userId, id, request.Approve, request.Note, ct)));

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return await action(userId);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }
}
