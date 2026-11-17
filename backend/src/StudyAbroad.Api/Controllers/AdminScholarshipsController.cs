using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Scholarships;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-109] Học bổng: Moderator thêm, sửa, ẩn/hiện; Admin chỉ xem. Không có xóa.</summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/admin/scholarships")]
public class AdminScholarshipsController(IAdminScholarshipService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminScholarshipDto>> Search([FromQuery] AdminScholarshipQuery query, CancellationToken ct) =>
        service.SearchAsync(query, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminScholarshipDto>> GetById(Guid id, CancellationToken ct)
    {
        var scholarship = await service.GetAsync(id, ct);
        return scholarship is null ? NotFound() : Ok(scholarship);
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminScholarshipDto>> Create([FromBody] SaveScholarshipRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.CreateAsync(request, actorId, ct);
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminScholarshipDto>> Update(Guid id, [FromBody] SaveScholarshipRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.UpdateAsync(id, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/visibility")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminScholarshipDto>> SetVisibility(
        Guid id, [FromBody] SetScholarshipVisibilityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.SetVisibilityAsync(id, request.IsActive, request.Reason, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }
}
