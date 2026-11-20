using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Infrastructure.Payments;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Công cụ dev: giả lập payOS thanh toán một đơn (payOS không có sandbox). Mọi route trả 404 trừ khi môi trường Development và PayOs:UseFake.
/// Kiểm ở resource filter để 404 đến trước cả việc đọc body.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[TypeFilter(typeof(DevPayOsOnlyFilter))]
[Route("api/v1/dev/payos")]
public class DevPayOsController(IOrderService orders, IOrderRepository orderRepository, PayOsOptions payOs, FakeGatewayState state) : ControllerBase
{
    public sealed record SetStatusRequest(string Status, long AmountPaid);

    public sealed record PayRequest(long? Amount);

    /// <summary>Đặt trạng thái link giả mà job đối soát sẽ thấy (PENDING, PAID, UNDERPAID, CANCELLED, EXPIRED...).</summary>
    [HttpPost("{orderCode:long}/status")]
    public IActionResult SetStatus(long orderCode, [FromBody] SetStatusRequest request)
    {
        state.Set(orderCode, request.Status.ToUpperInvariant(), request.AmountPaid);
        return Ok(new { orderCode, status = request.Status.ToUpperInvariant(), amountPaid = request.AmountPaid });
    }

    /// <summary>Dựng webhook đúng dạng payOS (ký bằng checksum key) rồi xử lý như webhook thật.</summary>
    [HttpPost("{orderCode:long}/pay")]
    public async Task<IActionResult> Pay(long orderCode, [FromBody] PayRequest? request, CancellationToken ct)
    {
        var order = await orderRepository.FindByCodeAsync(orderCode, ct);
        var amount = request?.Amount ?? (long)(order?.AmountVnd ?? 0);
        state.Set(orderCode, GatewayStatuses.Paid, amount);

        var data = new Dictionary<string, object?>
        {
            ["orderCode"] = orderCode,
            ["amount"] = amount,
            ["description"] = order?.Description ?? string.Empty,
            ["accountNumber"] = "12345678",
            ["reference"] = $"FAKE{orderCode}",
            ["transactionDateTime"] = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
            ["currency"] = "VND",
            ["paymentLinkId"] = $"fake-{orderCode}",
            ["code"] = "00",
            ["desc"] = "success",
            ["counterAccountName"] = "NGUYEN VAN A",   // phải bị lược khỏi payload lưu lại
        };
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var dataJson = JsonSerializer.Serialize(data, options);
        using var doc = JsonDocument.Parse(dataJson);
        var signature = PayOsSignature.Sign(doc.RootElement, payOs.ChecksumKey);
        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["code"] = "00",
            ["desc"] = "success",
            ["success"] = true,
            ["data"] = doc.RootElement,
            ["signature"] = signature,
        }, options);

        return Ok(new { handled = await orders.HandleWebhookAsync(body, DateTime.UtcNow, ct) });
    }
}

/// <summary>404 cho mọi route dev khi không phải Development hoặc không bật cổng giả.</summary>
public sealed class DevPayOsOnlyFilter(IHostEnvironment environment, PayOsOptions payOs) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!(environment.IsDevelopment() && payOs.UseFake))
            context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
