using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Universities;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/universities")]
public class UniversitiesController(IUniversityDetailService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UniversityListItemDto>>> Search(
        [FromQuery] UniversitySearchQuery query,
        CancellationToken ct)
    {
        var result = await service.SearchAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<UniversityFilterOptionsDto>> GetFilterOptions(CancellationToken ct)
    {
        var options = await service.GetFilterOptionsAsync(ct);
        return Ok(options);
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<UniversityDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        var university = await service.GetByCodeAsync(code, ct);
        return university is null ? NotFound() : Ok(university);
    }
}
