using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-151] Học sinh, Phụ huynh hoặc Trung tâm (về chính trung tâm mình) báo cáo đánh giá vi phạm.</summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent},{UserRoles.Center}")]
[Route("api/v1/study-centers/{centerId:guid}/reviews/{reviewId:guid}/report")]
public class CenterReviewReportsController(ICenterReviewReportService reports) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CenterReviewReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(
        Guid centerId,
        Guid reviewId,
        [FromBody] CreateCenterReviewReportRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var dto = await reports.ReportAsync(
                userId,
                User.GetEmail(),
                User.GetRole() ?? string.Empty,
                centerId,
                reviewId,
                request,
                ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Không có quyền", Detail = ex.Message, Status = StatusCodes.Status403Forbidden });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }
}
