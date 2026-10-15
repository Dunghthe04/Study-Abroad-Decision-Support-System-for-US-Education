using System.Net;
using System.Text;
using System.Text.Json;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Infrastructure.Advisor;

namespace StudyAbroad.UnitTests;

public class RecommendationAiClientTests
{
    //Advisor giả: ghi lại request, trả câu trả lời soạn sẵn
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public string? LastPath;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return await respond(request, ct);
        }
    }

    private static (RecommendationAiClient Client, StubHandler Handler) Make(HttpStatusCode status, string body, TimeSpan? timeout = null)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://advisor:8000/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        return (new RecommendationAiClient(http), handler);
    }

    [Fact]
    public async Task ReadExtracurricular_ReadsTierAndSnakeCaseKeys()
    {
        var (client, handler) = Make(HttpStatusCode.OK, """
            {"act":[{"role":0.8,"scope":1,"q":0.61}],"tong_hd":0.61,"bonus":0.15,"m":0.19,"tier":2,
             "skill_version":"2.0","model":"qwen3:8b","ai_used":false}
            """);
        var result = await client.ReadExtracurricularAsync(new AiEcRequest(
            [new AiEcActivity("Đội trưởng CLB Robotics", 1, 24, "leader"), new AiEcActivity("Tình nguyện", 3, 12)],
            [new AiEcAward("Giải Nhì Tin học", 3)]));

        Assert.Equal("/recommend/ec", handler.LastPath);
        var sent = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("Đội trưởng CLB Robotics", sent.GetProperty("act")[0].GetProperty("text").GetString());
        Assert.Equal(3, sent.GetProperty("awards")[0].GetProperty("level").GetInt32());
        Assert.Equal("leader", sent.GetProperty("act")[0].GetProperty("role").GetString());
        Assert.False(sent.GetProperty("act")[1].TryGetProperty("role", out _));     // không chọn vai trò ở form: bỏ key
        Assert.Equal((2, 0.61m, 0.8m, false), (result.Tier, result.ActivityTotal, result.Activities[0].Role, result.AiUsed));
    }

    [Fact]
    public async Task Explain_PostsSnakeCaseKeysAndDropsNulls()
    {
        var (client, handler) = Make(HttpStatusCode.OK, """
            {"summary":"Colgate là trường rất khó vào.","strengths":[],"improvements":["Nâng SAT"],
             "skill_version":"1.1","model":"qwen3:8b"}
            """);
        var result = await client.ExplainAsync(new AiExplainRequest(
            new AiExplainStudent(3.6m, 1350, null, null, null, 50000m, 3, null),
            new AiExplainSchool("Colgate", "NY", "private", "Khó vào xa", true, 17.5m, null, 1450, 1510, 17.2m, 0m,
                95610m, 45610m, 0m)));

        Assert.Equal("/recommend/explain", handler.LastPath);
        var sent = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal(3, sent.GetProperty("student").GetProperty("ec_tier").GetInt32());
        Assert.False(sent.GetProperty("student").TryGetProperty("ielts", out _));      // null bị bỏ
        Assert.True(sent.GetProperty("school").GetProperty("need_aid").GetBoolean());
        Assert.Equal(["Nâng SAT"], result.Improvements);
    }

    [Fact]
    public async Task AdvisorError_ThrowsWithStatusAndDetail()
    {
        var (client, _) = Make(HttpStatusCode.BadGateway, """{"detail":"AI không trả được bậc ngoại khóa hợp lệ sau 2 lần gọi"}""");
        var ex = await Assert.ThrowsAsync<RecommendationAiException>(() =>
            client.ReadExtracurricularAsync(new AiEcRequest([], [])));
        Assert.Contains("502", ex.Message);
        Assert.Contains("2 lần gọi", ex.Message);
    }

    [Fact]
    public async Task InvalidJson_Throws()
    {
        var (client, _) = Make(HttpStatusCode.OK, "không phải json");
        await Assert.ThrowsAsync<RecommendationAiException>(() => client.ReadExtracurricularAsync(new AiEcRequest([], [])));
    }

    [Fact]
    public async Task AdvisorDown_Throws()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var client = new RecommendationAiClient(new HttpClient(handler) { BaseAddress = new Uri("http://advisor:8000/") });
        var ex = await Assert.ThrowsAsync<RecommendationAiException>(() => client.ReadExtracurricularAsync(new AiEcRequest([], [])));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Timeout_Throws()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);   // advisor chậm hơn timeout
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new RecommendationAiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://advisor:8000/"), Timeout = TimeSpan.FromMilliseconds(100)
        });
        var ex = await Assert.ThrowsAsync<RecommendationAiException>(() => client.ReadExtracurricularAsync(new AiEcRequest([], [])));
        Assert.Contains("quá thời gian", ex.Message);
    }
}
