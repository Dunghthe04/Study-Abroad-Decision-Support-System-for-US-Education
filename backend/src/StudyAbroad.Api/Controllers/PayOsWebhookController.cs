using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Billing.Orders;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-78] Webhook payOS báo thanh toán. Không đăng nhập: danh tính là chữ ký HMAC trên data. Không giới hạn tốc độ để payOS gửi lại thoải mái.
/// Chữ ký sai: 400 invalid_signature. Mọi trường hợp khác (kể cả đơn không biết): 200 để payOS không gửi lại mãi. Lỗi hệ thống: 500 để payOS gửi lại.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/payments/payos/webhook")]
public class PayOsWebhookController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // Đọc nguyên văn body: chữ ký được kiểm trên đúng dữ liệu payOS gửi
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(ct);
        if (!await orders.HandleWebhookAsync(rawBody, DateTime.UtcNow, ct))
            return BadRequest(new { code = "invalid_signature", message = "Chữ ký webhook không hợp lệ." });
        return Ok(new { success = true });
    }
}
