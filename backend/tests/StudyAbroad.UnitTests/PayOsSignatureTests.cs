using System.Text.Json;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Infrastructure.Payments;

namespace StudyAbroad.UnitTests;

// Chữ ký webhook payOS (P17) và cổng giả
public class PayOsSignatureTests
{
    // Dữ liệu mẫu của payOS; chữ ký tính độc lập bằng Python:
    //   s = "&".join(f"{k}={data[k]}" for k in sorted(data))
    //   hmac.new(b"dev-checksum-key", s.encode("utf-8"), hashlib.sha256).hexdigest()
    private const string SampleData = """
        {"orderCode":123,"amount":3000,"description":"VQRIO123","accountNumber":"12345678","reference":"TF230204212323",
         "transactionDateTime":"2023-02-04 18:25:00","currency":"VND","paymentLinkId":"124c33293c43417ab7879e14c8d9eb18",
         "code":"00","desc":"Thành công","counterAccountBankId":"","counterAccountBankName":"","counterAccountName":"",
         "counterAccountNumber":"","virtualAccountName":"","virtualAccountNumber":""}
        """;
    private const string SampleString =
        "accountNumber=12345678&amount=3000&code=00&counterAccountBankId=&counterAccountBankName=&counterAccountName=&counterAccountNumber=" +
        "&currency=VND&desc=Thành công&description=VQRIO123&orderCode=123&paymentLinkId=124c33293c43417ab7879e14c8d9eb18" +
        "&reference=TF230204212323&transactionDateTime=2023-02-04 18:25:00&virtualAccountName=&virtualAccountNumber=";
    private const string SampleSignature = "3418f0f083c9329cbd6975679ba4b64a0e3a7aecbc7f7f5a9cb3f8995d23005e";
    private const string Key = "dev-checksum-key";

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void KnownVector_DataStringAndSignatureMatchPython()
    {
        var data = Parse(SampleData);

        Assert.Equal(SampleString, PayOsSignature.DataString(data));
        Assert.Equal(SampleSignature, PayOsSignature.Sign(data, Key));
        Assert.True(PayOsSignature.Verify(data, SampleSignature, Key));
    }

    [Fact]
    public void DataString_SortsOrdinally_AndFormatsValues()
    {
        var data = Parse("""{"b":2,"B":1,"a":null,"flag":true,"off":false,"price":1.5,"big":1795000012345,"nested":{"x":[1,2]},"s":"x y"}""");

        Assert.Equal("B=1&a=&b=2&big=1795000012345&flag=true&nested={\"x\":[1,2]}&off=false&price=1.5&s=x y", PayOsSignature.DataString(data));
    }

    [Fact]
    public void SignThenVerify_RoundTrips_AndUppercaseSignatureIsAccepted()
    {
        var data = Parse("""{"orderCode":1,"amount":2000}""");
        var signature = PayOsSignature.Sign(data, "k");

        Assert.Matches("^[0-9a-f]{64}$", signature);
        Assert.True(PayOsSignature.Verify(data, signature, "k"));
        Assert.True(PayOsSignature.Verify(data, signature.ToUpperInvariant(), "k"));
    }

    [Theory]
    [InlineData("wrong-key", true)]
    [InlineData("k", false)]
    public void Verify_RejectsWrongKeyOrTamperedData(string key, bool tamperKey)
    {
        var data = Parse("""{"orderCode":1,"amount":2000}""");
        var signature = PayOsSignature.Sign(data, "k");
        var checkedData = tamperKey ? data : Parse("""{"orderCode":1,"amount":2001}""");

        Assert.False(PayOsSignature.Verify(checkedData, signature, key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("3418f0f083c9329cbd6975679ba4b64a0e3a7aecbc7f7f5a9cb3f8995d23005")]       // thiếu một ký tự
    [InlineData("3418f0f083c9329cbd6975679ba4b64a0e3a7aecbc7f7f5a9cb3f8995d23005ef")]    // thừa
    public void Verify_RejectsMissingOrMalformedSignature(string? signature)
    {
        Assert.False(PayOsSignature.Verify(Parse(SampleData), signature, Key));
    }

    [Fact]
    public void Verify_RejectsEmptyKey()
    {
        var data = Parse(SampleData);

        Assert.False(PayOsSignature.Verify(data, PayOsSignature.Sign(data, ""), ""));
    }

    [Fact]
    public void Sanitize_DropsCounterAccountAndVirtualAccountFields_KeepsTheRest()
    {
        var sanitized = Parse(PayOsSignature.Sanitize(Parse(SampleData)));

        Assert.Equal(["accountNumber", "amount", "code", "currency", "desc", "description", "orderCode", "paymentLinkId", "reference", "transactionDateTime"],
            sanitized.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Thành công", sanitized.GetProperty("desc").GetString());
        Assert.DoesNotContain("counterAccount", PayOsSignature.Sanitize(Parse(SampleData)));
    }

    // ---- đọc webhook

    [Fact]
    public void Webhook_Valid_IsReadWithSanitizedData()
    {
        var webhook = PayOsWebhook.Verify(Body(SampleData, SampleSignature, "00"), Key);

        Assert.NotNull(webhook);
        Assert.Equal((123L, 3000L, "00", "TF230204212323"), (webhook.OrderCode, webhook.Amount, webhook.Code, webhook.Reference));
        Assert.DoesNotContain("counterAccount", webhook.SanitizedDataJson);
    }

    [Fact]
    public void Webhook_OuterCodeIsNotSigned_SoItCannotTurnAFailureIntoSuccess()
    {
        // data.code = "01" (thất bại, đã ký); kẻ gian đổi mã ngoài thành "00": vẫn không phải thành công
        var failedData = SampleData.Replace("\"code\":\"00\"", "\"code\":\"01\"");
        var failedSignature = PayOsSignature.Sign(Parse(failedData), Key);

        var webhook = PayOsWebhook.Verify(Body(failedData, failedSignature, "00"), Key);

        Assert.NotNull(webhook);
        Assert.Equal("01", webhook.Code);
    }

    [Fact]
    public void Webhook_OuterFailureCode_IsNotSuccessEvenIfDataSaysOk()
    {
        var webhook = PayOsWebhook.Verify(Body(SampleData, SampleSignature, "02"), Key);

        Assert.Equal("02", webhook!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("[]")]
    [InlineData("{\"code\":\"00\"}")]
    [InlineData("{\"code\":\"00\",\"data\":[],\"signature\":\"x\"}")]
    [InlineData("{\"code\":\"00\",\"data\":{\"amount\":1},\"signature\":\"x\"}")]
    public void Webhook_Malformed_IsNull(string body) => Assert.Null(PayOsWebhook.Verify(body, Key));

    [Fact]
    public void Webhook_WrongSignature_IsNull() =>
        Assert.Null(PayOsWebhook.Verify(Body(SampleData, SampleSignature.Replace('3', '4'), "00"), Key));

    [Fact]
    public void Webhook_OrderCodeOrAmountNotANumber_IsNull()
    {
        var data = SampleData.Replace("\"orderCode\":123", "\"orderCode\":\"123\"");

        Assert.Null(PayOsWebhook.Verify(Body(data, PayOsSignature.Sign(Parse(data), Key), "00"), Key));
    }

    // ---- cổng giả

    [Fact]
    public async Task FakeGateway_CreateLink_StatusAndCancel()
    {
        var state = new FakeGatewayState();
        var gateway = new FakePaymentGateway(new PayOsOptions { ReturnUrlBase = "http://web.test/", ChecksumKey = Key }, state);

        var link = await gateway.CreateLinkAsync(42, 79000, "USAS00042", DateTime.UtcNow.AddMinutes(30), "r", "c", default);
        Assert.Equal(new GatewayLink("fake-42", "http://web.test/orders/42", "FAKE-QR-42"), link);
        Assert.Equal(new GatewayStatus("PENDING", 0), await gateway.GetStatusAsync(42, default));

        state.Set(42, "PAID", 79000);
        Assert.Equal(new GatewayStatus("PAID", 79000), await gateway.GetStatusAsync(42, default));

        await gateway.CancelAsync(42, "x", default);
        Assert.Equal("CANCELLED", (await gateway.GetStatusAsync(42, default)).Status);
    }

    [Fact]
    public async Task FakeGateway_VerifiesWebhookWithTheChecksumKey()
    {
        var gateway = new FakePaymentGateway(new PayOsOptions { ChecksumKey = Key }, new FakeGatewayState());

        Assert.NotNull(await gateway.VerifyWebhookAsync(Body(SampleData, SampleSignature, "00"), default));
        Assert.Null(await gateway.VerifyWebhookAsync(Body(SampleData, "0".PadRight(64, '0'), "00"), default));
    }

    private static string Body(string dataJson, string signature, string outerCode) =>
        $"{{\"code\":\"{outerCode}\",\"desc\":\"success\",\"success\":true,\"data\":{dataJson},\"signature\":\"{signature}\"}}";
}
