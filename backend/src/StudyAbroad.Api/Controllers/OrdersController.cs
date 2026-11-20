using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-78, UC-112] Đơn hàng gói dịch vụ của chính người dùng. Chủ đơn lấy từ token, không nhận từ client.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Student + "," + UserRoles.Parent)]
[Route("api/v1/orders")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    public sealed record CreateOrderRequest(string PlanCode);

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        var order = await orders.CreateAsync(userId, User.GetRole() ?? string.Empty, request.PlanCode, DateTime.UtcNow, ct);
        return CreatedAtAction(nameof(Get), new { orderCode = order.OrderCode }, order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await orders.ListMineAsync(userId, page, pageSize, ct));
    }

    [HttpGet("{orderCode:long}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long orderCode, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await orders.GetMineAsync(userId, orderCode, ct));
    }

    [HttpPost("{orderCode:long}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long orderCode, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return Ok(await orders.CancelAsync(userId, orderCode, DateTime.UtcNow, ct));
    }
}
