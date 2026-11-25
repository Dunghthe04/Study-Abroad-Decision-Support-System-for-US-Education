using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-151 / UC-53] Moderator xem hàng đợi báo cáo đánh giá trung tâm và bác bỏ báo cáo (gỡ đánh giá dùng CenterReviewModerationController).</summary>
[ApiController]
[Authorize(Roles = UserRoles.Moderator)]
[Route("api/v1/moderator/center-review-reports")]
public class ModeratorCenterReviewReportsController(ICenterReviewReportService reports) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CenterReviewReportDto>>> List(
        [FromQuery] string? status = "open",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await reports.SearchForModeratorAsync(status, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    [HttpPost("{reportId:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(
        Guid reportId,
        [FromBody] DismissCenterReviewReportRequest? request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } moderatorId)
            return Unauthorized();

        try
        {
            await reports.DismissAsync(moderatorId, reportId, request?.Note, ct);
            return Ok(new { message = "Đã bác bỏ báo cáo đánh giá." });
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
