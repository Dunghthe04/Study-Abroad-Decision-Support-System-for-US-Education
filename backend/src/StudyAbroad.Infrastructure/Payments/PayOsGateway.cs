using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V2.PaymentRequests;
using StudyAbroad.Application.Billing.Payments;

namespace StudyAbroad.Infrastructure.Payments;

/// <summary>
/// Cổng thanh toán payOS thật (SDK NuGet payOS 2.1.1, https://api-merchant.payos.vn). Không có sandbox nên kiểm thử dùng <see cref="FakePaymentGateway"/>.
/// SDK dựng lười: khóa trống không làm hỏng lúc khởi động mà báo lỗi khi dùng (người gọi trả gateway_unavailable).
/// </summary>
public sealed class PayOsGateway(StudyAbroad.Application.Billing.Payments.PayOsOptions options, ILogger<PayOsGateway> logger, HttpClient? httpClient = null) : IPaymentGateway
{
    private readonly Lazy<PayOSClient> client = new(() => new PayOSClient(new PayOS.PayOSOptions
    {
        ClientId = options.ClientId,
        ApiKey = options.ApiKey,
        ChecksumKey = options.ChecksumKey,
        HttpClient = httpClient,
        MaxRetries = 1,
    }));

    public Task<GatewayLink> CreateLinkAsync(long orderCode, long amountVnd, string description, DateTime expiresAtUtc, string returnUrl, string cancelUrl, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var response = await client.Value.PaymentRequests.CreateAsync(
                new CreatePaymentLinkRequest
                {
                    OrderCode = orderCode,
                    Amount = amountVnd,
                    Description = description,
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl,
                    ExpiredAt = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                },
                new PayOS.Models.RequestOptions<CreatePaymentLinkRequest> { CancellationToken = ct });
            return new GatewayLink(response.PaymentLinkId, response.CheckoutUrl, response.QrCode);
        }, ct);

    public Task<GatewayStatus> GetStatusAsync(long orderCode, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var link = await client.Value.PaymentRequests.GetAsync(orderCode, new PayOS.Models.RequestOptions { CancellationToken = ct });
            return new GatewayStatus(link.Status.ToString().ToUpperInvariant(), link.AmountPaid);
        }, ct);

    public Task CancelAsync(long orderCode, string reason, CancellationToken ct) =>
        CallAsync(async () =>
        {
            await client.Value.PaymentRequests.CancelAsync(orderCode, reason, new PayOS.Models.RequestOptions<CancelPaymentLinkRequest> { CancellationToken = ct });
            return true;
        }, ct);

    public async Task<VerifiedWebhook?> VerifyWebhookAsync(string rawBody, CancellationToken ct)
    {
        try
        {
            // SDK kiểm chữ ký trên data; sau đó đọc lại body thô để lấy mã, số tiền và bản data đã lược bỏ tài khoản đối ứng
            await client.Value.Webhooks.VerifyAsync(rawBody);
            return PayOsWebhook.ReadAlreadyVerified(rawBody);
        }
        catch (Exception ex) when (ex is PayOSException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Webhook payOS bị từ chối: {Reason}", ex.Message);
            return null;
        }
    }

    // Mọi lỗi SDK và quá thời gian đổi thành HttpRequestException để người gọi xử lý một kiểu
    private static async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is PayOSException or InvalidOperationException or ArgumentException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            throw new HttpRequestException($"payOS: {ex.Message}", ex);
        }
    }
}
