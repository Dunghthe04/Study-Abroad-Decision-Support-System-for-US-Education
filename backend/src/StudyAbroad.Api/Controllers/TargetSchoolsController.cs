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
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent},{UserRoles.Admin},{UserRoles.Moderator}")]
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
    /// [UC-33] Xem danh sách các trường mục tiêu của học sinh kèm tóm tắt và hạn nộp gần nhất.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<TargetSchoolSummaryDto>> GetTargetSchools(
        [FromQuery] string? category,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var summary = await service.GetTargetSchoolsAsync(userId, category, status, ct);
        return Ok(summary);
    }

    /// <summary>
    /// [UC-32] Chọn hoặc thay đổi vòng nộp hồ sơ (Early Action, Early Decision, Regular...) và hạn nộp.
    /// </summary>
    [HttpPatch("{id:guid}/round")]
    public async Task<ActionResult<TargetSchoolDto>> UpdateApplicationRound(
        Guid id,
        [FromBody] UpdateApplicationRoundRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await service.UpdateApplicationRoundAsync(userId, id, request, ct);
        if (!result.Success || result.TargetSchool is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.TargetSchool);
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
    /// [UC-31] Xóa một trường khỏi danh sách mục tiêu theo ID của mục tiêu.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveTargetSchool(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var success = await service.RemoveTargetSchoolAsync(userId, id, ct);
        return success ? NoContent() : NotFound();
    }

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
        return Ok(dto);
    }
}
