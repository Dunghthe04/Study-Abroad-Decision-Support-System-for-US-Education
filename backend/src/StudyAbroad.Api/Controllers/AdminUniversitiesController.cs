using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-65, UC-66, UC-67] Dữ liệu trường. Quyết định D2 bản 14/11: Moderator thêm, sửa, ẩn; Admin chỉ xem.
/// Cả hai role đọc được (GET); từng thao tác ghi do Moderator giữ.
/// </summary>
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

    /// <summary>[UC-22, UC-62] Trường có hỗ trợ tài chính trung bình lớn hơn học phí: dữ liệu bất thường để Moderator kiểm tra lại nguồn.</summary>
    [HttpGet("aid-anomalies")]
    public Task<IReadOnlyList<AdminAidAnomalyDto>> ListAidAnomalies(CancellationToken ct) =>
        service.ListAidAnomaliesAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUniversityDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var university = await service.GetAsync(id, ct);
        return university is null ? NotFound() : Ok(university);
    }

    /// <summary>[UC-65] Nhập thêm một trường (chỉ Moderator).</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Moderator)]
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

    /// <summary>[UC-65] Thêm một bậc học cho trường (chỉ Moderator).</summary>
    [HttpPost("{id:guid}/offerings")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminUniversityOfferingDto>> CreateOffering(
        Guid id, [FromBody] CreateOfferingRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.CreateOfferingAsync(id, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    /// <summary>[UC-66] Sửa thông tin trường (chỉ Moderator).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminUniversityDetailDto>> Update(Guid id, [FromBody] UpdateUniversityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.UpdateUniversityAsync(id, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    /// <summary>[UC-66] Sửa một bậc học (offering) của trường: học phí, hạn nộp, điều kiện... (chỉ Moderator).</summary>
    [HttpPut("{id:guid}/offerings/{offeringId:guid}")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminUniversityOfferingDto>> UpdateOffering(
        Guid id, Guid offeringId, [FromBody] UpdateOfferingRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.UpdateOfferingAsync(id, offeringId, request, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    /// <summary>[UC-67] Ẩn hoặc hiện trường khỏi tìm kiếm và gợi ý (chỉ Moderator).</summary>
    [HttpPatch("{id:guid}/visibility")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<AdminUniversityDetailDto>> SetVisibility(
        Guid id, [FromBody] SetUniversityVisibilityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await service.SetVisibilityAsync(id, request.IsActive, request.Reason, actorId, ct);
        if (result.NotFound) return NotFound();
        if (!result.Success) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }
}
