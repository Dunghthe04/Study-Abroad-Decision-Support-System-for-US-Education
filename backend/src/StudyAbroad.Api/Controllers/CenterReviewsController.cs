using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-2070 / UC-45] Học sinh / phụ huynh đã gửi yêu cầu liên hệ thì đánh giá trung tâm. Xem đánh giá (UC-41) ở StudyCentersController.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/study-centers/{centerId:guid}/reviews")]
public class CenterReviewsController(ICenterReviewService service) : ControllerBase
{
    /// <summary>Người dùng hiện tại có được đánh giá trung tâm này không (đã gửi yêu cầu liên hệ, chưa đánh giá).</summary>
    [HttpGet("eligibility")]
    [ProducesResponseType(typeof(CenterReviewEligibilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetEligibility(Guid centerId, CancellationToken ct) =>
        RunAsync(async userId => Ok(await service.GetEligibilityAsync(userId, centerId, ct)));

    /// <summary>Gửi đánh giá 1 đến 5 sao kèm nhận xét tùy chọn. Mỗi người một đánh giá cho mỗi trung tâm.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CenterReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Create(Guid centerId, [FromBody] CreateCenterReviewRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            var dto = await service.CreateAsync(userId, centerId, request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        });

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
