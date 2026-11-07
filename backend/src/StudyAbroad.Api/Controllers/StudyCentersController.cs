using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Route("api/v1/study-centers")]
public class StudyCentersController(IStudyCenterService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<StudyCenterDto>> Search([FromQuery] StudyCenterQuery query, CancellationToken ct) =>
        service.SearchAsync(query, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudyCenterDto>> GetById(Guid id, CancellationToken ct)
    {
        var center = await service.GetByIdAsync(id, ct);
        return center is null ? NotFound() : Ok(center);
    }

    /// <summary>[UC-71] Thêm một trung tâm tư vấn vào danh bạ (Moderator hoặc Admin).</summary>
    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
    public async Task<ActionResult<StudyCenterDto>> Create(
        [FromBody] CreateStudyCenterRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await service.CreateAsync(request, actorId, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return CreatedAtAction(nameof(GetById), new { id = result.Center.Id }, result.Center);
    }

    /// <summary>[Q11 / UC-71 Phụ trợ / UC-64] Moderator hoặc Admin tạo tài khoản người dùng cho Trung tâm tư vấn (role center).</summary>
    [HttpPost("accounts")]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
    public async Task<ActionResult<UserDto>> CreateCenterAccount(
        [FromBody] CreateCenterAccountRequest request,
        [FromServices] IAuthService auth,
        CancellationToken ct)
    {
        var result = await auth.CreateCenterAccountAsync(request, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }

    /// <summary>Chat (B6) lấy tối đa 3 trung tâm đã xác minh cho một dịch vụ; cùng thành phố xếp trước.</summary>
    [HttpGet("referrals")]
    public async Task<ActionResult<IReadOnlyList<StudyCenterDto>>> Refer(
        [FromQuery] CenterReferralQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            return BadRequest("service is required");
        return Ok(await service.ReferAsync(query, ct));
    }

    /// <summary>Các thành phố đang có trung tâm gợi ý được (chat đưa cho AI chọn thành phố của học sinh).</summary>
    [HttpGet("referral-cities")]
    public Task<IReadOnlyList<string>> ReferralCities(CancellationToken ct) => service.GetReferralCitiesAsync(ct);

}
