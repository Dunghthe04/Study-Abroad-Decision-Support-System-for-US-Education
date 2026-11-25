using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-124..126] Admin xem và xử lý đơn hàng. Lỗi quy tắc trả { code, message } qua BillingRuleExceptionFilter.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/orders")]
public class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    /// <summary>Danh sách đơn, mới nhất trước. status chữ thường; q là mã đơn, mã GD payOS, tên/email người mua hoặc mã người mua đã xóa; from/to là khoảng ngày (VN).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await orders.ListAsync(status, q, page, pageSize, from, to, ct));

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
    [ProducesResponseType(typeof(AdminOrderRecheckDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recheck(long orderCode, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return Ok(await orders.RecheckAsync(adminId, orderCode, DateTime.UtcNow, ct));
    }

    /// <summary>[UC-124 2a, UC-91] Xem họ tên và email của tài khoản đã xóa (giữ 10 năm theo UC-75); mỗi lần xem ghi nhật ký truy cập.</summary>
    [HttpPost("{orderCode:long}/reveal-buyer")]
    [ProducesResponseType(typeof(AdminOrderBuyerInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevealBuyer(long orderCode, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        return Ok(await orders.RevealBuyerAsync(adminId, orderCode, ip, string.IsNullOrWhiteSpace(ua) ? null : ua, ct));
    }
}
