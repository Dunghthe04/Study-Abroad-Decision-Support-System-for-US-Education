namespace StudyAbroad.Application.Billing.Payments;

/// <summary>Cổng thanh toán (payOS). Lỗi mạng hoặc lỗi payOS ném <see cref="HttpRequestException"/>; người gọi đổi thành gateway_unavailable.</summary>
public interface IPaymentGateway
{
    Task<GatewayLink> CreateLinkAsync(long orderCode, long amountVnd, string description, DateTime expiresAtUtc, string returnUrl, string cancelUrl, CancellationToken ct);

    Task<GatewayStatus> GetStatusAsync(long orderCode, CancellationToken ct);

    Task CancelAsync(long orderCode, string reason, CancellationToken ct);

    /// <summary>Null = chữ ký sai hoặc body không đọc được.</summary>
    Task<VerifiedWebhook?> VerifyWebhookAsync(string rawBody, CancellationToken ct);
}

public sealed record GatewayLink(string PaymentLinkId, string CheckoutUrl, string QrCode);

/// <param name="Status">PENDING | PROCESSING | PAID | UNDERPAID | CANCELLED | EXPIRED | FAILED</param>
public sealed record GatewayStatus(string Status, long AmountPaid);

/// <param name="Code">"00" = thanh toán thành công (cả mã ngoài và mã trong data đều "00").</param>
/// <param name="SanitizedDataJson">data của webhook đã bỏ các trường tài khoản đối ứng, an toàn để lưu.</param>
public sealed record VerifiedWebhook(long OrderCode, long Amount, string Code, string? Reference, string SanitizedDataJson)
{
    public const string SuccessCode = "00";
}

/// <summary>Các trạng thái <see cref="GatewayStatus.Status"/> của payOS.</summary>
public static class GatewayStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Paid = "PAID";
    public const string Underpaid = "UNDERPAID";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
    public const string Failed = "FAILED";
}
