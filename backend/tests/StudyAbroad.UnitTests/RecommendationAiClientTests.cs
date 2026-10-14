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

    private const string ScoreResponse = """
        {"skill_version":"1.1","model":"qwen3:8b","failed":["X"],
         "items":[{"c":"190099","w":[0.75,0.25],"pg":17.2,"conf":"H","ps":0,"A":12.9,"adj":0,"S":12.9,
                   "g":"R","r":0.25,"g2":"RF","F":0,"H":0.906,"Q":0.905,"gap":45610}]}
        """;

    [Fact]
    public async Task Score_PostsCompactJsonAndReadsUppercaseKeys()
    {
        var (client, handler) = Make(HttpStatusCode.OK, ScoreResponse);
        var school = new AiSchool("190099", 0.175m, null, [1450, 1510], 0.23m, "opt", null, 3.85m, [3, 2, 2], 95610m, null, 0.905m);
        var result = await client.ScoreAsync(new AiScoreRequest(new AiStudent(3.6m, null, 50000m, 3), [school]));

        Assert.Equal("/recommend/score", handler.LastPath);
        // Key ngắn đúng khuôn advisor; null bị bỏ (không có "iacc", "sat" của học sinh, "aid")
        Assert.Equal(
            """{"student":{"gpa":3.6,"budget":50000,"tier":3},"schools":[{"c":"190099","acc":0.175,"sat":[1450,1510],"subm":0.23,"satp":"opt","avg":3.85,"L":[3,2,2],"coa":95610,"q":0.905}]}""",
            handler.LastBody);

        var item = Assert.Single(result.Items);
        Assert.Equal((12.9m, 12.9m, "RF"), (item.A, item.S, item.G2));   // "A", "S" viết hoa vẫn đọc đúng
        Assert.Equal(["X"], result.Failed);
        Assert.Equal("1.1", result.SkillVersion);
    }

    [Fact]
    public async Task ReadExtracurricular_ReadsTierAndSnakeCaseKeys()
    {
        var (client, handler) = Make(HttpStatusCode.OK, """
            {"act":[{"role":0.8,"scope":1,"q":0.61}],"tong_hd":0.61,"bonus":0.15,"m":0.19,"tier":2,
             "skill_version":"1.1","model":"qwen3:8b"}
            """);
        var result = await client.ReadExtracurricularAsync(new AiEcRequest(
            [new AiEcActivity("Đội trưởng CLB Robotics", 1, 24)], [new AiEcAward("Giải Nhì Tin học", 3)]));

        Assert.Equal("/recommend/ec", handler.LastPath);
        var sent = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("Đội trưởng CLB Robotics", sent.GetProperty("act")[0].GetProperty("text").GetString());
        Assert.Equal(3, sent.GetProperty("awards")[0].GetProperty("level").GetInt32());
        Assert.Equal((2, 0.61m, 0.8m), (result.Tier, result.ActivityTotal, result.Activities[0].Role));
    }

    [Fact]
    public async Task Pick_SendsNeedAidAndUppercaseCriteria()
    {
        var (client, handler) = Make(HttpStatusCode.OK, """
            {"status":"OK","reach":[],"match":[{"c":"C","X":0.414,"why":["GPA_MID"]}],"safety":[],
             "aid":[{"c":"190099","X":null,"why":["NEED_AID"]}],"other":[],"skill_version":"1.1","model":"qwen3:8b"}
            """);
        var result = await client.PickAsync(new AiPickRequest([2, 2, 2], "preferred",
            [new AiPickItem("C", "M", "MAIN", 0.08m, 0.261m, 0.9m, null, 50m, 58.33m, 0m, null)]));

        Assert.Equal("/recommend/pick", handler.LastPath);
        Assert.Equal(
            """{"pref":[2,2,2],"needAid":"preferred","items":[{"c":"C","g2":"M","blk":"MAIN","F":0.08,"H":0.261,"Q":0.9,"pg":50,"ps":58.33,"adj":0}]}""",
            handler.LastBody);
        Assert.Equal("C", Assert.Single(result.Match).Code);
        Assert.Null(result.Aid[0].X);
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
        await Assert.ThrowsAsync<RecommendationAiException>(() => client.PickAsync(new AiPickRequest([2, 2, 2], "no", [])));
    }

    [Fact]
    public async Task AdvisorDown_Throws()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var client = new RecommendationAiClient(new HttpClient(handler) { BaseAddress = new Uri("http://advisor:8000/") });
        var ex = await Assert.ThrowsAsync<RecommendationAiException>(() => client.PickAsync(new AiPickRequest([2, 2, 2], "no", [])));
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
        var ex = await Assert.ThrowsAsync<RecommendationAiException>(() => client.PickAsync(new AiPickRequest([2, 2, 2], "no", [])));
        Assert.Contains("quá thời gian", ex.Message);
    }
}
