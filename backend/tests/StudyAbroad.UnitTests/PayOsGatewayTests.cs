using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Infrastructure.Payments;

namespace StudyAbroad.UnitTests;

// PayOsGateway chạy với SDK payOS thật nhưng HttpClient giả: kiểm đúng đường dẫn, header, body, chữ ký và cách đổi lỗi (payOS không có sandbox)
public class PayOsGatewayTests
{
    private const string Key = "ck-test";

    private static readonly PayOsOptions Options = new() { ClientId = "cid", ApiKey = "akey", ChecksumKey = Key, ReturnUrlBase = "https://usas.test" };

    private static PayOsGateway Gateway(StubHandler handler, PayOsOptions? options = null) =>
        new(options ?? Options, NullLogger<PayOsGateway>.Instance, new HttpClient(handler));

    [Fact]
    public async Task CreateLink_PostsSignedRequestWithUnixSecondsExpiry_AndReadsTheLink()
    {
        var handler = new StubHandler(_ => Ok(new Dictionary<string, object?>
        {
            ["bin"] = "970422", ["accountNumber"] = "123", ["accountName"] = "USAS", ["amount"] = 79000, ["description"] = "USAS12345",
            ["orderCode"] = 1795000012345, ["currency"] = "VND", ["paymentLinkId"] = "pl-1", ["status"] = "PENDING",
            ["checkoutUrl"] = "https://pay.payos.vn/web/pl-1", ["qrCode"] = "00020101QR", ["expiredAt"] = 1795147200,
        }));

        var link = await Gateway(handler).CreateLinkAsync(
            1795000012345, 79000, "USAS12345", new DateTime(2026, 11, 20, 4, 0, 0, DateTimeKind.Utc), "https://usas.test/orders/1795000012345", "https://usas.test/orders/1795000012345", default);

        Assert.Equal(new GatewayLink("pl-1", "https://pay.payos.vn/web/pl-1", "00020101QR"), link);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "https://api-merchant.payos.vn/v2/payment-requests"), (request.Method, request.Uri));
        Assert.Contains("x-client-id=cid", request.Headers);
        Assert.Contains("x-api-key=akey", request.Headers);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(1795000012345, root.GetProperty("orderCode").GetInt64());
        Assert.Equal(79000, root.GetProperty("amount").GetInt64());
        Assert.Equal(1795147200, root.GetProperty("expiredAt").GetInt64());   // 2026-11-20 04:00 UTC, Unix giây
        // Chữ ký tạo link của payOS: HMAC trên amount, cancelUrl, description, orderCode, returnUrl xếp theo tên
        using var signed = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            amount = 79000, cancelUrl = "https://usas.test/orders/1795000012345", description = "USAS12345",
            orderCode = 1795000012345, returnUrl = "https://usas.test/orders/1795000012345",
        }));
        Assert.Equal(PayOsSignature.Sign(signed.RootElement, Key), root.GetProperty("signature").GetString());
    }

    [Theory]
    [InlineData("PENDING", "PENDING")]
    [InlineData("PAID", "PAID")]
    [InlineData("UNDERPAID", "UNDERPAID")]
    [InlineData("CANCELLED", "CANCELLED")]
    [InlineData("EXPIRED", "EXPIRED")]
    [InlineData("PROCESSING", "PROCESSING")]
    [InlineData("FAILED", "FAILED")]
    public async Task GetStatus_MapsEveryPayOsStatusToUpperCase(string payOsStatus, string expected)
    {
        var handler = new StubHandler(_ => Ok(new Dictionary<string, object?>
        {
            ["id"] = "pl-1", ["orderCode"] = 1795000012345, ["amount"] = 79000, ["amountPaid"] = 79000, ["amountRemaining"] = 0,
            ["status"] = payOsStatus, ["createdAt"] = "2026-11-20T03:00:00+07:00", ["transactions"] = Array.Empty<object>(),
            ["canceledAt"] = null, ["cancellationReason"] = null,
        }));

        var status = await Gateway(handler).GetStatusAsync(1795000012345, default);

        Assert.Equal(new GatewayStatus(expected, 79000), status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "https://api-merchant.payos.vn/v2/payment-requests/1795000012345"), (request.Method, request.Uri));
    }

    [Fact]
    public async Task Cancel_PostsTheReasonToTheCancelRoute()
    {
        var handler = new StubHandler(_ => Ok(new Dictionary<string, object?>
        {
            ["id"] = "pl-1", ["orderCode"] = 1795000012345, ["amount"] = 79000, ["amountPaid"] = 0, ["amountRemaining"] = 79000,
            ["status"] = "CANCELLED", ["createdAt"] = "2026-11-20T03:00:00+07:00", ["transactions"] = Array.Empty<object>(),
            ["canceledAt"] = "2026-11-20T03:01:00+07:00", ["cancellationReason"] = "Người dùng hủy đơn",
        }));

        await Gateway(handler).CancelAsync(1795000012345, "Người dùng hủy đơn", default);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "https://api-merchant.payos.vn/v2/payment-requests/1795000012345/cancel"), (request.Method, request.Uri));
        Assert.Contains("Người dùng hủy đơn", JsonDocument.Parse(request.Body).RootElement.GetProperty("cancellationReason").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SdkErrors_BecomeHttpRequestException_ForCreateGetAndCancel(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => ("{\"code\":\"101\",\"desc\":\"loi\"}", status));
        var gateway = Gateway(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.CreateLinkAsync(1, 79000, "USAS00001", DateTime.UtcNow.AddMinutes(30), "https://a/b", "https://a/b", default));
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.GetStatusAsync(1, default));
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.CancelAsync(1, "x", default));
    }

    [Fact]
    public async Task EmptyKeys_DoNotBreakConstruction_AndFailOnUse()
    {
        var gateway = Gateway(new StubHandler(_ => ("{\"code\":\"201\",\"desc\":\"khoa sai\"}", HttpStatusCode.Unauthorized)), new PayOsOptions());   // dựng không ném lỗi

        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.GetStatusAsync(1, default));
    }

    [Fact]
    public async Task Webhook_ValidSignature_IsVerifiedBySdk_AndSanitized()
    {
        var gateway = Gateway(new StubHandler(_ => Ok(new Dictionary<string, object?>())));

        var verified = await gateway.VerifyWebhookAsync(WebhookBody(Key), default);

        Assert.NotNull(verified);
        Assert.Equal((1795000012345, 79000L, "00", "TF230204212323"), (verified.OrderCode, verified.Amount, verified.Code, verified.Reference));
        using var data = JsonDocument.Parse(verified.SanitizedDataJson);
        Assert.False(data.RootElement.TryGetProperty("counterAccountName", out _));
        Assert.False(data.RootElement.TryGetProperty("virtualAccountNumber", out _));
        Assert.Equal("USAS12345", data.RootElement.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("")]
    public async Task Webhook_BadSignature_ReturnsNull(string signingKey)
    {
        var gateway = Gateway(new StubHandler(_ => Ok(new Dictionary<string, object?>())));

        Assert.Null(await gateway.VerifyWebhookAsync(WebhookBody(signingKey), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"code\":\"00\",\"data\":null,\"signature\":\"abc\"}")]
    [InlineData("[1,2]")]
    public async Task Webhook_MalformedBody_ReturnsNull(string body)
    {
        var gateway = Gateway(new StubHandler(_ => Ok(new Dictionary<string, object?>())));

        Assert.Null(await gateway.VerifyWebhookAsync(body, default));
    }

    // ---- helpers

    private static (string Body, HttpStatusCode Status) Ok(Dictionary<string, object?> data)
    {
        var dataJson = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(dataJson);
        return ($"{{\"code\":\"00\",\"desc\":\"success\",\"data\":{dataJson},\"signature\":\"{PayOsSignature.Sign(doc.RootElement, Key)}\"}}", HttpStatusCode.OK);
    }

    /// <summary>Body webhook như payOS gửi, ký bằng <paramref name="signingKey"/>.</summary>
    internal static string WebhookBody(string signingKey, string code = "00", long amount = 79000)
    {
        var data = new Dictionary<string, object?>
        {
            ["orderCode"] = 1795000012345, ["amount"] = amount, ["description"] = "USAS12345", ["accountNumber"] = "12345678",
            ["reference"] = "TF230204212323", ["transactionDateTime"] = "2026-11-20 10:00:00", ["currency"] = "VND",
            ["paymentLinkId"] = "pl-1", ["code"] = code, ["desc"] = "success", ["counterAccountBankId"] = "",
            ["counterAccountBankName"] = "NH A", ["counterAccountName"] = "NGUYEN VAN A", ["counterAccountNumber"] = "999",
            ["virtualAccountName"] = "", ["virtualAccountNumber"] = "VA1",
        };
        var dataJson = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(dataJson);
        return $"{{\"code\":\"{code}\",\"desc\":\"success\",\"success\":true,\"data\":{dataJson},\"signature\":\"{(signingKey.Length == 0 ? "" : PayOsSignature.Sign(doc.RootElement, signingKey))}\"}}";
    }

    private sealed record Seen(string Method, string Uri, List<string> Headers, string Body);

    private sealed class StubHandler(Func<Seen, (string Body, HttpStatusCode Status)> respond) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var headers = request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}").ToList();
            var seen = new Seen(request.Method.Method, request.RequestUri!.ToString(), headers, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            Requests.Add(seen);
            var (body, status) = respond(seen);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
