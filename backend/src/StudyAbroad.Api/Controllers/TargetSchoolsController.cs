using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Quản lý danh sách trường mục tiêu của học sinh (UC-30, UC-31, UC-32, UC-33).
/// </summary>
[ApiController]
[Route("api/v1/target-schools")]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
public class TargetSchoolsController(ITargetSchoolService service) : ControllerBase
{
    /// <summary>
    /// [UC-30] Thêm một trường đại học vào danh sách mục tiêu của học sinh.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TargetSchoolDto>> AddTargetSchool(
        [FromBody] AddTargetSchoolRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await service.AddTargetSchoolAsync(userId, request, ct);
        if (!result.Success || result.TargetSchool is null)
            return ValidationProblem(result.ErrorMessage);

        return CreatedAtAction(nameof(GetById), new { id = result.TargetSchool.Id }, result.TargetSchool);
    }

    /// <summary>
    /// Lấy chi tiết một trường mục tiêu theo ID của học sinh đang đăng nhập.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TargetSchoolDto>> GetById(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var targetSchool = await service.GetByIdAsync(id, userId, ct);
        return targetSchool is null ? NotFound() : Ok(targetSchool);
    }

    /// <summary>
    /// [UC-31] Xóa một trường khỏi danh sách mục tiêu theo TargetSchool ID hoặc University Code / ID.
    /// </summary>
    [HttpDelete("{identifier}")]
    public async Task<IActionResult> RemoveTargetSchool(string identifier, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(identifier) || string.Equals(identifier, "undefined", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Mã định danh trường không hợp lệ.");

        if (Guid.TryParse(identifier, out var id))
        {
            var success = await service.RemoveTargetSchoolAsync(userId, id, ct);
            if (success)
                return NoContent();
        }

        var successByUni = await service.RemoveTargetSchoolByUniversityAsync(userId, identifier, ct);
        return successByUni ? NoContent() : NotFound();
    }

    [NonAction]
    public Task<IActionResult> RemoveTargetSchool(Guid id, CancellationToken ct) =>
        RemoveTargetSchool(id.ToString(), ct);

    /// <summary>
    /// [UC-31] Xóa một trường khỏi danh sách mục tiêu theo University Code hoặc University ID.
    /// </summary>
    [HttpDelete("by-university/{universityIdentifier}")]
    public async Task<IActionResult> RemoveByUniversity(string universityIdentifier, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var success = await service.RemoveTargetSchoolByUniversityAsync(userId, universityIdentifier, ct);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Kiểm tra xem một trường đại học đã có trong danh sách mục tiêu của học sinh hay chưa.
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<TargetSchoolDto?>> CheckTargetSchool(
        [FromQuery] string? universityCode,
        [FromQuery] Guid? universityId,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var dto = await service.CheckTargetSchoolAsync(userId, universityCode, universityId, ct);
        return dto is not null ? Ok(dto) : NoContent();
    }
}
