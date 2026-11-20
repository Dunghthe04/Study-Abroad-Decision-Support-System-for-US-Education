using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm (trung tâm gửi ở UC-92).
/// Duyệt: khóa tài khoản, ẩn trung tâm, đóng lead đang mở, hẹn ngày xóa hẳn. Từ chối: báo lý do qua email.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/center-deletion-requests")]
public class AdminCenterDeletionRequestsController(ICenterDeletionService service) : ControllerBase
{
    /// <summary>Danh sách yêu cầu. status: pending | approved | completed | rejected | all (mặc định tất cả, chờ xử lý lên đầu).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminCenterDeletionRequestDto>), StatusCodes.Status200OK)]
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

    /// <summary>Duyệt yêu cầu sau khi đã kiểm tra danh tính người gửi (ghi chú bắt buộc).</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(AdminCenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCenterDeletionRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await service.ApproveAsync(adminId, id, request?.IdentityCheckNote, ct));
    }

    /// <summary>Từ chối yêu cầu, lý do bắt buộc.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(AdminCenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCenterDeletionRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await service.RejectAsync(adminId, id, request?.Reason, ct));
    }

    /// <summary>
    /// Gia hạn một lần, tối đa 20 ngày, thời gian xử lý yêu cầu (Nghị định 356/2025/NĐ-CP Điều 5 khoản 4).
    /// Lý do bắt buộc và được gửi cho người yêu cầu.
    /// </summary>
    [HttpPost("{id:guid}/extend")]
    [ProducesResponseType(typeof(AdminCenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendCenterDeletionRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await service.ExtendAsync(adminId, id, request?.Reason, ct));
    }

    private IActionResult ToResult(CenterDeletionActionResult result)
    {
        if (result.NotFound)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = result.Error, Status = StatusCodes.Status404NotFound });
        return result.Success ? Ok(result.Request) : ValidationProblem(result.Error);
    }
}
