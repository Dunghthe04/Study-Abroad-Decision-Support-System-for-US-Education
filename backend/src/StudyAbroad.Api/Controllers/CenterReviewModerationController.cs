using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[USAS-2079 / UC-57] Moderator gỡ đánh giá trung tâm vi phạm quy tắc.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Moderator)]
[Route("api/v1/study-centers/{centerId:guid}/reviews/{reviewId:guid}")]
public class CenterReviewModerationController(ICenterReviewModerationService service) : ControllerBase
{
    /// <summary>
    /// Gỡ một đánh giá kèm lý do bắt buộc (tối đa 500 ký tự). Đánh giá biến khỏi danh sách công khai và điểm trung bình, có nhật ký.
    /// Đánh giá của trung tâm khác trả 404; gỡ lặp lại không đổi gì.
    /// </summary>
    [HttpPost("remove")]
    [ProducesResponseType(typeof(CenterReviewRemovalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid centerId,
        Guid reviewId,
        [FromBody] RemoveCenterReviewRequest? request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return Ok(await service.RemoveAsync(userId, centerId, reviewId, request?.Reason, ct));
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

    /// <summary>[UC-53 2b / UC-152] Moderator gỡ phản hồi vi phạm của trung tâm dưới một đánh giá.</summary>
    [HttpPost("reply/remove")]
    [ProducesResponseType(typeof(CenterReviewRemovalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReply(
        Guid centerId,
        Guid reviewId,
        [FromBody] RemoveCenterReviewRequest? request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return Ok(await service.RemoveReplyAsync(userId, centerId, reviewId, request?.Reason, ct));
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
