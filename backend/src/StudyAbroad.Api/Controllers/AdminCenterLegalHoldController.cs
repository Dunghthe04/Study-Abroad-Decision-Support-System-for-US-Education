using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-93] Administrator giữ pháp lý (Legal Hold) dữ liệu một trung tâm khi bị nghi lừa đảo, có tranh chấp
/// hoặc cơ quan có thẩm quyền yêu cầu: chặn duyệt yêu cầu xóa và chặn job xóa hẳn tới khi gỡ. Mỗi lần bật/gỡ ghi audit.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/study-centers/{centerId:guid}/legal-hold")]
public class AdminCenterLegalHoldController(ICenterDeletionService service) : ControllerBase
{
    /// <summary>Tình trạng giữ pháp lý và yêu cầu xóa đang mở (nếu có) của trung tâm.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CenterLegalHoldDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid centerId, CancellationToken ct) =>
        ToResult(await service.GetLegalHoldAsync(centerId, ct));

    /// <summary>Bật giữ pháp lý, lý do bắt buộc (tối đa 500 ký tự).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CenterLegalHoldDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Set(Guid centerId, [FromBody] SetCenterLegalHoldRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await service.SetLegalHoldAsync(adminId, centerId, request?.Reason, ct));
    }

    /// <summary>Gỡ giữ pháp lý khi việc điều tra kết thúc, lý do bắt buộc.</summary>
    [HttpPost("release")]
    [ProducesResponseType(typeof(CenterLegalHoldDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release(Guid centerId, [FromBody] SetCenterLegalHoldRequest? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await service.ReleaseLegalHoldAsync(adminId, centerId, request?.Reason, ct));
    }

    private IActionResult ToResult(CenterLegalHoldResult result)
    {
        if (result.NotFound)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = result.Error, Status = StatusCodes.Status404NotFound });
        return result.Success ? Ok(result.Hold) : ValidationProblem(result.Error);
    }
}
