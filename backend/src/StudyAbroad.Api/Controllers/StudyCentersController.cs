using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;

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
