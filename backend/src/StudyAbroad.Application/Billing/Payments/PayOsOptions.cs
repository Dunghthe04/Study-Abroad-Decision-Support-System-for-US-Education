namespace StudyAbroad.Application.Billing.Payments;

/// <summary>
/// Khóa payOS (appsettings "PayOs"; ghi đè bằng PAYOS_CLIENT_ID, PAYOS_API_KEY, PAYOS_CHECKSUM_KEY, PAYOS_RETURN_URL_BASE, PAYOS_USE_FAKE).
/// payOS không có sandbox: <see cref="UseFake"/> bật cổng giả cho dev và kiểm thử.
/// </summary>
public sealed class PayOsOptions
{
    public const string SectionName = "PayOs";

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;

    /// <summary>Địa chỉ trang web; payOS đưa người mua về {ReturnUrlBase}/orders/{mã đơn}.</summary>
    public string ReturnUrlBase { get; set; } = "http://localhost:3000";

    public bool UseFake { get; set; }
}
