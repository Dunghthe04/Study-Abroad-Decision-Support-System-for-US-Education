using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Universities;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/universities")]
public class UniversitiesController(IUniversityDetailService service) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<ActionResult<UniversityDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        var university = await service.GetByCodeAsync(code, ct);
        return university is null ? NotFound() : Ok(university);
    }
}
