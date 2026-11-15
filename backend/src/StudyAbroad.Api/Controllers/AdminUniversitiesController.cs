using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-65, UC-66, UC-67] Quản trị dữ liệu trường cho admin và moderator.</summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/admin/universities")]
public class AdminUniversitiesController(IAdminUniversityService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminUniversityListItemDto>> Search([FromQuery] AdminUniversityQuery query, CancellationToken ct) =>
        service.SearchAsync(query, ct);

    /// <summary>Danh mục ngành để chọn khi sửa offering.</summary>
    [HttpGet("majors")]
    public Task<IReadOnlyList<AdminMajorOptionDto>> ListMajors(CancellationToken ct) =>
        service.ListMajorsAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUniversityDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var university = await service.GetAsync(id, ct);
        return university is null ? NotFound() : Ok(university);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUniversityDetailDto>> Create([FromBody] CreateUniversityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.CreateUniversityAsync(request, actorId, ct);
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}/offerings")]
    public async Task<ActionResult<IReadOnlyList<AdminUniversityOfferingDto>>> GetOfferings(Guid id, CancellationToken ct)
    {
        var offerings = await service.GetOfferingsAsync(id, ct);
        return offerings is null ? NotFound() : Ok(offerings);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUniversityDetailDto>> Update(Guid id, [FromBody] UpdateUniversityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.UpdateUniversityAsync(id, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/offerings/{offeringId:guid}")]
    public async Task<ActionResult<AdminUniversityOfferingDto>> UpdateOffering(
        Guid id, Guid offeringId, [FromBody] UpdateOfferingRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.UpdateOfferingAsync(id, offeringId, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/visibility")]
    public async Task<ActionResult<AdminUniversityDetailDto>> SetVisibility(
        Guid id, [FromBody] SetUniversityVisibilityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var university = await service.SetVisibilityAsync(id, request.IsActive, actorId, ct);
        return university is null ? NotFound() : Ok(university);
    }
}
