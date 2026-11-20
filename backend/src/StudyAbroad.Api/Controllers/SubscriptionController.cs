using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-77, UC-78] Gói của tôi: tier, các pool theo phần trăm, kỳ xếp hàng, đơn đang chờ. Không có số token.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Student + "," + UserRoles.Parent)]
[Route("api/v1/subscription")]
public class SubscriptionController(IEntitlementService entitlements) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SubscriptionSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await entitlements.GetSummaryAsync(userId, DateTime.UtcNow, ct));
    }
}
