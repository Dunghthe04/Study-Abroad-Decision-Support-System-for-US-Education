using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[USAS-2120 / UC-92] Trung tâm gửi, xem và rút yêu cầu xóa tài khoản và dữ liệu của chính mình.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Center)]
[Route("api/v1/center/deletion-request")]
public class CenterDeletionRequestsController(ICenterDeletionService service) : ControllerBase
{
    /// <summary>Yêu cầu xóa gần nhất của trung tâm, 204 nếu chưa từng gửi.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> GetMine(CancellationToken ct) =>
        CenterDeletionControllerHelper.RunAsync(this, async userId =>
        {
            var latest = await service.GetMineAsync(userId, ct);
            return latest is null ? NoContent() : Ok(latest);
        });

    /// <summary>Gửi yêu cầu xóa. Mỗi trung tâm chỉ có một yêu cầu đang chờ xử lý.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CenterDeletionRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Create([FromBody] CreateCenterDeletionRequest? request, CancellationToken ct) =>
        CenterDeletionControllerHelper.RunAsync(this, async userId =>
            StatusCode(StatusCodes.Status201Created, await service.RequestAsync(userId, request ?? new CreateCenterDeletionRequest(), ct)));

    /// <summary>Rút yêu cầu khi Administrator chưa xử lý.</summary>
    [HttpDelete]
    [ProducesResponseType(typeof(CenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Cancel(CancellationToken ct) =>
        CenterDeletionControllerHelper.RunAsync(this, async userId => Ok(await service.CancelAsync(userId, ct)));
}

/// <summary>
/// [USAS-2121 / UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm: kiểm tra danh tính, thực hiện xóa, báo kết quả.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/center-deletion-requests")]
public class AdminCenterDeletionRequestsController(ICenterDeletionService service) : ControllerBase
{
    /// <summary>Danh sách yêu cầu. status: pending (mặc định, gần hết hạn lên đầu) | completed | rejected | cancelled | all.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CenterDeletionRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        CenterDeletionControllerHelper.RunAsync(this, async _ => Ok(await service.GetAsync(status, page, pageSize, ct)));

    /// <summary>Thực hiện xóa. Phải xác nhận đã kiểm tra danh tính. Không hoàn tác được.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(CenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Complete(Guid id, [FromBody] CompleteCenterDeletionRequest request, CancellationToken ct) =>
        CenterDeletionControllerHelper.RunAsync(this, async adminId => Ok(await service.CompleteAsync(adminId, id, request, ct)));

    /// <summary>Từ chối yêu cầu (ví dụ không xác minh được danh tính). Lý do bắt buộc.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(CenterDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectCenterDeletionRequest request, CancellationToken ct) =>
        CenterDeletionControllerHelper.RunAsync(this, async adminId => Ok(await service.RejectAsync(adminId, id, request, ct)));
}

internal static class CenterDeletionControllerHelper
{
    public static async Task<IActionResult> RunAsync(ControllerBase controller, Func<Guid, Task<IActionResult>> action)
    {
        if (controller.User.GetUserId() is not { } userId)
            return controller.Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return await action(userId);
        }
        catch (ArgumentException ex)
        {
            return controller.ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return controller.NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }
}
