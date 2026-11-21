using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Billing;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-77] Các gói đang bán cho trang gói. Ai cũng xem được; không có số token.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/plans")]
public class PlansController(IEntitlementService entitlements) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await entitlements.GetPlansOnSaleAsync(ct));
}
