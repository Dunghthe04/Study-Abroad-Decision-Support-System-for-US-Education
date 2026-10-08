using System.Net;
using System.Text;
using System.Text.Json;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Infrastructure.Advisor;

namespace StudyAbroad.UnitTests;

public class ExtracurricularScoringTests
{
    // Advisor giả: đổi Handler để giả lập advisor trả kết quả, ném lỗi hoặc trả lời chậm
    private sealed class FakeExtracurricularAi : IExtracurricularAi
    {
        public Func<ExtracurricularScoreRequest, CancellationToken, Task<ExtracurricularScoreResponse>> Handler { get; set; } =
            (_, _) => Task.FromResult(new ExtracurricularScoreResponse(0m, true, []));
        public List<ExtracurricularScoreRequest> Requests { get; } = [];

        public Task<ExtracurricularScoreResponse> ScoreAsync(ExtracurricularScoreRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Handler(request, ct);
        }
    }

    private static readonly ExtracurricularActivityInput Robotics =
        new("a1", "CLB Robotics", "Chủ nhiệm, người sáng lập", "THPT Chu Văn An", "Lập CLB 30 thành viên", 3, 24);

    private static readonly ScoredActivity RoboticsScored = new("a1", "founder", false, 3, 0.83m, 0.92m, true);

    [Fact]
    public async Task NoActivities_ScoresZeroWithoutCallingAdvisor()
    {
        var ai = new FakeExtracurricularAi();

        var result = await new ExtracurricularScoring(ai).ScoreAsync([], storedScore: 3.5m, timeoutSeconds: 30);

        Assert.Equal(0m, result.Score);
        Assert.True(result.Fresh);
        Assert.Null(result.Warning);
        Assert.Empty(ai.Requests);
    }

    [Fact]
    public async Task AdvisorResponds_UsesNewScoreAndBreakdown()
    {
        var ai = new FakeExtracurricularAi
        {
            Handler = (_, _) => Task.FromResult(new ExtracurricularScoreResponse(0.92m, true, [RoboticsScored]))
        };

        var result = await new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: 3.5m, timeoutSeconds: 30);

        Assert.Equal(0.92m, result.Score);   // điểm mới thay điểm cũ 3.5
        Assert.True(result.Fresh);
        Assert.True(result.AiUsed);
        Assert.Null(result.Warning);
        Assert.Equal("founder", Assert.Single(result.Activities).Role);
        Assert.Same(Robotics, Assert.Single(Assert.Single(ai.Requests).Activities));
    }

    [Fact]
    public async Task AdvisorFellBackToKeywords_KeepsScoreAndWarns()
    {
        var ai = new FakeExtracurricularAi
        {
            Handler = (_, _) => Task.FromResult(new ExtracurricularScoreResponse(0.85m, false, [RoboticsScored]))
        };

        var result = await new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: null, timeoutSeconds: 30);

        Assert.Equal(0.85m, result.Score);
        Assert.True(result.Fresh);
        Assert.False(result.AiUsed);
        Assert.Equal(ExtracurricularScoring.KeywordWarning, result.Warning);
    }

    [Fact]
    public async Task AdvisorDown_WithStoredScore_UsesStoredScore()
    {
        var ai = new FakeExtracurricularAi { Handler = (_, _) => throw new HttpRequestException("connection refused") };

        var result = await new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: 3.5m, timeoutSeconds: 30);

        Assert.Equal(3.5m, result.Score);
        Assert.False(result.Fresh);
        Assert.Empty(result.Activities);
        Assert.Equal(ExtracurricularScoring.StaleWarning, result.Warning);
    }

    [Fact]
    public async Task AdvisorDown_WithoutStoredScore_SkipsCriterion()
    {
        var ai = new FakeExtracurricularAi { Handler = (_, _) => throw new HttpRequestException("connection refused") };

        var result = await new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: null, timeoutSeconds: 30);

        Assert.Null(result.Score);
        Assert.Equal(ExtracurricularScoring.MissingWarning, result.Warning);
    }

    [Fact]
    public async Task AdvisorTooSlow_TimesOutAndUsesStoredScore()
    {
        var ai = new FakeExtracurricularAi
        {
            Handler = async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);   // bị hủy sau 1 giây
                return new ExtracurricularScoreResponse(4m, true, []);
            }
        };

        var result = await new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: 2.0m, timeoutSeconds: 1);

        Assert.Equal(2.0m, result.Score);
        Assert.False(result.Fresh);
    }

    [Fact]
    public async Task UserCancels_ExceptionIsNotSwallowed()
    {
        var ai = new FakeExtracurricularAi
        {
            Handler = async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new ExtracurricularScoreResponse(4m, true, []);
            }
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ExtracurricularScoring(ai).ScoreAsync([Robotics], storedScore: 2.0m, timeoutSeconds: 30, cts.Token));
    }

    // ---------- hợp đồng JSON với advisor (Python dùng camelCase) ----------

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestPath = request.RequestUri!.AbsolutePath;
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task Client_SendsCamelCaseAndReadsAdvisorResponse()
    {
        // JSON đúng như advisor trả về (ExtracurricularResponse bên Python)
        var handler = new CaptureHandler("""
            {"score": 0.92, "aiUsed": true, "activities": [
              {"id": "a1", "role": "founder", "reputableOrg": false, "impactLevel": 3,
               "quality": 0.83, "points": 0.92, "counted": true}]}
            """);
        var client = new ExtracurricularAiClient(new HttpClient(handler) { BaseAddress = new Uri("http://advisor/") });

        var result = await client.ScoreAsync(new ExtracurricularScoreRequest([Robotics]));

        Assert.Equal("/api/v1/profile/extracurricular", handler.RequestPath);
        var sent = JsonDocument.Parse(handler.RequestBody!).RootElement.GetProperty("activities")[0];
        Assert.Equal(3, sent.GetProperty("impactLevel").GetInt32());
        Assert.Equal("Chủ nhiệm, người sáng lập", sent.GetProperty("role").GetString());
        Assert.Equal(0.92m, result.Score);
        Assert.Equal(RoboticsScored, Assert.Single(result.Activities));
    }
}
