using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-132..134] Admin xem và xử lý đơn hàng. Lỗi quy tắc trả { code, message } qua BillingRuleExceptionFilter.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/orders")]
public class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    /// <summary>Danh sách đơn, mới nhất trước. status chữ thường (tùy chọn); q là mã đơn hoặc một phần email người mua (tùy chọn).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await orders.ListAsync(status, q, page, pageSize, ct));

    [HttpGet("{orderCode:long}")]
    [ProducesResponseType(typeof(AdminOrderDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long orderCode, CancellationToken ct) => Ok(await orders.GetAsync(orderCode, ct));

    [HttpPost("{orderCode:long}/activate")]
    [ProducesResponseType(typeof(AdminOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(long orderCode, [FromBody] AdminOrderNoteRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return Ok(await orders.ActivateAsync(adminId, orderCode, request.Note, DateTime.UtcNow, ct));
    }

    [HttpPost("{orderCode:long}/reject")]
    [ProducesResponseType(typeof(AdminOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long orderCode, [FromBody] AdminOrderNoteRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return Ok(await orders.RejectAsync(adminId, orderCode, request.Note, DateTime.UtcNow, ct));
    }

    [HttpPost("{orderCode:long}/recheck")]
    [ProducesResponseType(typeof(AdminOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recheck(long orderCode, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return Ok(await orders.RecheckAsync(adminId, orderCode, DateTime.UtcNow, ct));
    }
}
