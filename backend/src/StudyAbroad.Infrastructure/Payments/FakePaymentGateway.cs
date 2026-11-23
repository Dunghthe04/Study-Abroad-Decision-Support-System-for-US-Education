using System.Collections.Concurrent;
using StudyAbroad.Application.Billing.Payments;

namespace StudyAbroad.Infrastructure.Payments;

/// <summary>Trạng thái giả của các link thanh toán (theo mã đơn), dùng chung một tiến trình. Mặc định PENDING, 0 đ.</summary>
public sealed class FakeGatewayState
{
    private readonly ConcurrentDictionary<long, GatewayStatus> statuses = new();

    public GatewayStatus Get(long orderCode) =>
        statuses.TryGetValue(orderCode, out var s) ? s : new GatewayStatus(GatewayStatuses.Pending, 0);

    public void Set(long orderCode, string status, long amountPaid) =>
        statuses[orderCode] = new GatewayStatus(status, amountPaid);
}

/// <summary>Cổng thanh toán giả (PayOs:UseFake) cho dev và kiểm thử: không gọi mạng, webhook vẫn kiểm chữ ký bằng checksum key.</summary>
public sealed class FakePaymentGateway(PayOsOptions options, FakeGatewayState state) : IPaymentGateway
{
    public Task<GatewayLink> CreateLinkAsync(long orderCode, long amountVnd, string description, DateTime expiresAtUtc, string returnUrl, string cancelUrl, CancellationToken ct) =>
        Task.FromResult(new GatewayLink($"fake-{orderCode}", $"{options.ReturnUrlBase.TrimEnd('/')}/orders/{orderCode}", $"FAKE-QR-{orderCode}"));

    public Task<GatewayStatus> GetStatusAsync(long orderCode, CancellationToken ct) =>
        Task.FromResult(state.Get(orderCode));

    public Task CancelAsync(long orderCode, string reason, CancellationToken ct)
    {
        state.Set(orderCode, GatewayStatuses.Cancelled, state.Get(orderCode).AmountPaid);
        return Task.CompletedTask;
    }

    public Task<VerifiedWebhook?> VerifyWebhookAsync(string rawBody, CancellationToken ct) =>
        Task.FromResult(PayOsWebhook.Verify(rawBody, options.ChecksumKey));
}
