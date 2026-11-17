using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-2138 / UC-114] Moderator kiểm tra các thay đổi website / fanpage mà trung tâm tự sửa (UC-92): Đạt hoặc Không đạt kèm lý do.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Moderator)]
[Route("api/v1/admin/center-profile-changes")]
public class AdminCenterProfileChangesController(ICenterProfileChangeService service) : ControllerBase
{
    /// <summary>Danh sách thay đổi. status: pending (mặc định) | passed | failed | withdrawn | all.</summary>
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
            return Ok(await service.GetAsync(status, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>Ghi kết quả kiểm tra. Không đạt bắt buộc có lý do; trung tâm nhận thông báo. Thay đổi đã xử lý thì không xử lý lại.</summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(CenterProfileChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewCenterProfileChangeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } moderatorId)
            return Unauthorized();

        try
        {
            return Ok(await service.ReviewAsync(moderatorId, id, request, ct));
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
