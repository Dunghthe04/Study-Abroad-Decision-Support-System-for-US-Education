using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-79] Admin quản lý gói dịch vụ. Lỗi quy tắc trả { code, message } qua BillingRuleExceptionFilter.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/plans")]
public class AdminPlansController(IPlanAdminService plans) : ControllerBase
{
    /// <summary>Mọi gói (cả gói ngừng bán), theo thứ tự hiển thị rồi tên.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<AdminPlanDto>> List(CancellationToken ct) => await plans.ListAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePlanRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return StatusCode(StatusCodes.Status201Created, await plans.CreateAsync(adminId, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePlanRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return Ok(await plans.UpdateAsync(adminId, id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        await plans.DeleteAsync(adminId, id, ct);
        return NoContent();
    }
}
