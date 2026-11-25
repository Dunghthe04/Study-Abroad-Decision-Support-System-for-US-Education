using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-152] Cổng trung tâm: xem đánh giá về trung tâm mình và đăng / sửa (trong 24h) phản hồi công khai.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Center)]
[Route("api/v1/center/reviews")]
public class CenterPortalReviewsController(ICenterReviewReplyService replies) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CenterReviewsDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CenterReviewPaging.DefaultPageSize,
        CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        try
        {
            return Ok(await replies.GetOwnCenterReviewsAsync(userId, User.GetEmail(), page, pageSize, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }

    [HttpPost("{reviewId:guid}/reply")]
    [ProducesResponseType(typeof(CenterReviewDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReply(
        Guid reviewId,
        [FromBody] UpsertCenterReviewReplyRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        try
        {
            var dto = await replies.CreateReplyAsync(userId, User.GetEmail(), reviewId, request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
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

    [HttpPut("{reviewId:guid}/reply")]
    [ProducesResponseType(typeof(CenterReviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EditReply(
        Guid reviewId,
        [FromBody] UpsertCenterReviewReplyRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        try
        {
            var dto = await replies.EditReplyAsync(userId, User.GetEmail(), reviewId, request, ct);
            return Ok(dto);
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
