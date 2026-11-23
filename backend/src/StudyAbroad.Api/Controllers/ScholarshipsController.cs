using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Scholarships;

namespace StudyAbroad.Api.Controllers;

/// <summary>[USAS-2030] UC-29: tìm học bổng, khách cũng dùng được.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/scholarships")]
public class ScholarshipsController(IScholarshipSearchService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ScholarshipDto>>> Search(
        [FromQuery] ScholarshipSearchQuery query,
        CancellationToken ct) => Ok(await service.SearchAsync(query, ct));
}
