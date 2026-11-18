using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-114] Moderator kiểm tra thay đổi website / fanpage do trung tâm tự sửa (UC-92): Đạt hoặc Không đạt (lý do bắt buộc).
/// Thu hồi dấu xác minh (UC-59) và ẩn trung tâm (UC-72) dùng các endpoint có sẵn.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Moderator)]
[Route("api/v1/moderator/center-profile-changes")]
public class ModeratorCenterProfileChangesController(ICenterProfileChangeReviewService service) : ControllerBase
{
    /// <summary>Danh sách thay đổi. status: pending (mặc định) | passed | failed | all.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CenterProfileChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.SearchAsync(status, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>Đạt: thay đổi được giữ, đóng mục kiểm tra.</summary>
    [HttpPost("{id:guid}/pass")]
    [ProducesResponseType(typeof(CenterProfileChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pass(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } moderatorId)
            return Unauthorized();
        return ToActionResult(await service.PassAsync(id, moderatorId, ct));
    }

    /// <summary>Không đạt: lý do bắt buộc, trung tâm nhận thông báo để sửa lại.</summary>
    [HttpPost("{id:guid}/fail")]
    [ProducesResponseType(typeof(CenterProfileChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fail(Guid id, [FromBody] FailCenterProfileChangeRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } moderatorId)
            return Unauthorized();
        return ToActionResult(await service.FailAsync(id, request?.Reason, moderatorId, ct));
    }

    private IActionResult ToActionResult(CenterProfileChangeReviewResult result)
    {
        if (result.NotFound)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = result.Error, Status = StatusCodes.Status404NotFound });
        if (!result.Success)
            return ValidationProblem(result.Error);
        return Ok(result.Change);
    }
}
