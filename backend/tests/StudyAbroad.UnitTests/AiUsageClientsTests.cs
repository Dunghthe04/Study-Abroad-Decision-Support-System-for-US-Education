using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Infrastructure.Advisor;

namespace StudyAbroad.UnitTests;

// Hai client gọi advisor ngoài chat (bậc ngoại khóa, OCR bảng điểm) bỏ số đo token vào giỏ chung của yêu cầu.
public class AiUsageClientsTests
{
    private const string Usage =
        "\"aiUsage\":[{\"source\":\"claude\",\"model\":\"claude-haiku-5-5\",\"purpose\":\"skill_ec\",\"skillVersion\":\"1.0\"," +
        "\"inputTokens\":900,\"outputTokens\":120,\"cachedTokens\":0,\"ok\":true,\"error\":null,\"latencyMs\":2500}]";

    private static HttpClient Client(HttpStatusCode status, string json) =>
        new(new Canned(status, json)) { BaseAddress = new Uri("http://advisor.test/") };

    // ---- bậc ngoại khóa

    [Fact]
    public async Task EcClient_PutsTheUsageIntoTheBasket_AndStillReturnsTheTier()
    {
        var basket = new AiUsageCollector();
        var client = new EcTierClient(Client(HttpStatusCode.OK, $"{{\"tier\":4,\"ai_used\":true,{Usage}}}"), NullLogger<EcTierClient>.Instance, basket);

        var tier = await client.ReadTierAsync(new EcTierRequest([], []));

        Assert.Equal(4, tier);
        var call = Assert.Single(basket.Snapshot());
        Assert.Equal(("claude", "skill_ec", 900, 120), (call.Source, call.Purpose, call.InputTokens, call.OutputTokens));
    }

    [Fact]
    public async Task EcClient_StillRecordsTheUsage_WhenTheTierIsInvalid_BecauseTheTokensWereSpent()
    {
        var basket = new AiUsageCollector();
        var client = new EcTierClient(Client(HttpStatusCode.OK, $"{{\"tier\":9,\"ai_used\":true,{Usage}}}"), NullLogger<EcTierClient>.Instance, basket);

        var tier = await client.ReadTierAsync(new EcTierRequest([], []));

        Assert.Null(tier);                       // bậc sai thì dùng công thức dự phòng như cũ
        Assert.Single(basket.Snapshot());        // nhưng token đã tiêu thật
    }

    [Fact]
    public async Task EcClient_RecordsNothing_WhenAdvisorAnswersWithAnError()
    {
        var basket = new AiUsageCollector();
        var client = new EcTierClient(Client(HttpStatusCode.InternalServerError, "{}"), NullLogger<EcTierClient>.Instance, basket);

        Assert.Null(await client.ReadTierAsync(new EcTierRequest([], [])));
        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public async Task EcClient_WorksWithoutABasket()
    {
        var client = new EcTierClient(Client(HttpStatusCode.OK, $"{{\"tier\":3,\"ai_used\":true,{Usage}}}"), NullLogger<EcTierClient>.Instance);

        Assert.Equal(3, await client.ReadTierAsync(new EcTierRequest([], [])));
    }

    // ---- OCR bảng điểm

    [Fact]
    public async Task OcrClient_PutsBothStepsUsageIntoTheBasket()
    {
        const string json =
            "{\"success\":true,\"modelUsed\":\"qwen3-vl\",\"terms\":[],\"aiUsage\":[" +
            "{\"source\":\"ollama\",\"model\":\"qwen3-vl\",\"purpose\":\"transcript_ocr_read\",\"inputTokens\":700,\"outputTokens\":300,\"latencyMs\":18000}," +
            "{\"source\":\"claude\",\"model\":\"claude-haiku-5-5\",\"purpose\":\"transcript_ocr_parse\",\"inputTokens\":2100,\"outputTokens\":400,\"latencyMs\":2200}]}";
        var basket = new AiUsageCollector();
        var client = new TranscriptOcrClient(Client(HttpStatusCode.OK, json), NullLogger<TranscriptOcrClient>.Instance, basket);

        var response = await client.OcrTranscriptAsync(new TranscriptOcrRequest());

        Assert.True(response.Success);
        Assert.Equal(["transcript_ocr_read", "transcript_ocr_parse"], basket.Snapshot().Select(c => c.Purpose));
        Assert.Equal(["ollama", "claude"], basket.Snapshot().Select(c => c.Source));
    }

    [Fact]
    public async Task OcrClient_RecordsNothing_WhenAdvisorIsDown_AndGivesTheManualFallback()
    {
        var basket = new AiUsageCollector();
        var client = new TranscriptOcrClient(Client(HttpStatusCode.ServiceUnavailable, "{}"), NullLogger<TranscriptOcrClient>.Instance, basket);

        var response = await client.OcrTranscriptAsync(new TranscriptOcrRequest());

        Assert.False(response.Success);
        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public async Task OcrClient_AnOldAdvisorWithoutAiUsage_AddsNothingAndStillWorks()
    {
        var basket = new AiUsageCollector();
        var client = new TranscriptOcrClient(Client(HttpStatusCode.OK, "{\"success\":true,\"terms\":[]}"), NullLogger<TranscriptOcrClient>.Instance, basket);

        Assert.True((await client.OcrTranscriptAsync(new TranscriptOcrRequest())).Success);
        Assert.Empty(basket.Snapshot());
    }

    private sealed class Canned(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
