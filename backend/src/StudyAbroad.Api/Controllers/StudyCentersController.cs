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
}
