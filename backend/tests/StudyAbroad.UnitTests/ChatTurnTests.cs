using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// Quyết định outcome, review_status và dựng meta_json của một lượt chat (ChatTurn). Chỉ logic thuần, chưa đụng database.
public class ChatTurnTests
{
    private static AdvisorSlmDto Slm(string outOfScope) => new(Intents: ["out_of_scope"], OutOfScope: outOfScope, Confidence: 0.4);
    private static AiUsageDto Call(string source, string purpose, bool ok = true, int latency = 100) =>
        new(source, "m", purpose, "1.0", ok ? 100 : 0, ok ? 10 : 0, 0, ok, ok ? null : "timeout", latency);

    // ---- outcome

    [Fact]
    public void NormalTurn_IsAnswered()
    {
        var slm = new AdvisorSlmDto(Intents: ["collect_profile", "knowledge"], Confidence: 0.93);

        Assert.Equal(ChatOutcomes.Answered, ChatTurn.Outcome(slm, [Call("ollama", "slm_intents"), Call("claude", "skill_tra_loi")]));
    }

    [Fact]
    public void NoSlmSummaryAndNoUsage_IsAnswered() => Assert.Equal(ChatOutcomes.Answered, ChatTurn.Outcome(null, null));

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("exception")]
    public void NotUnderstoodOrOffTopic_IsNoAnswer(string subtype) =>
        Assert.Equal(ChatOutcomes.NoAnswer, ChatTurn.Outcome(Slm(subtype), null));

    [Theory]
    [InlineData("sensitive")]      // bị chặn có chủ ý, hệ thống đã làm đúng việc của nó
    [InlineData("greeting")]
    [InlineData("farewell")]
    [InlineData("about_system")]
    [InlineData("human_handoff")]
    public void DeliberateOutOfScopeKinds_AreAnswered(string subtype) =>
        Assert.Equal(ChatOutcomes.Answered, ChatTurn.Outcome(Slm(subtype), null));

    [Fact]
    public void AmbiguousSubtypeOnlyCountsWhenTheTurnReallyRanAsOutOfScope()
    {
        // Luật code đã cứu câu thành knowledge: intents không còn out_of_scope nên không phải no_answer dù outOfScope còn sót giá trị
        var rescued = new AdvisorSlmDto(Intents: ["knowledge"], OutOfScope: "ambiguous");

        Assert.Equal(ChatOutcomes.Answered, ChatTurn.Outcome(rescued, null));
    }

    [Fact]
    public void StepWhereEveryCallFailed_IsFailed()
    {
        var usage = new[] { Call("ollama", "slm_intents", ok: false), Call("ollama", "slm_intents", ok: false) };

        Assert.Equal(ChatOutcomes.Failed, ChatTurn.Outcome(null, usage));
    }

    [Fact]
    public void FailedCallThatWasRetriedSuccessfully_IsNotFailed()
    {
        // Học sinh vẫn nhận được câu trả lời, nên không tính là lỗi
        var usage = new[] { Call("claude", "skill_tra_loi", ok: false), Call("claude", "skill_tra_loi", ok: true) };

        Assert.Equal(ChatOutcomes.Answered, ChatTurn.Outcome(null, usage));
    }

    [Fact]
    public void Failed_WinsOverNoAnswer()
    {
        // Ollama tắt: SLM dùng kết quả dự phòng "mơ hồ". Lỗi do hệ thống, không phải câu khó hiểu
        var slm = new AdvisorSlmDto(Intents: ["out_of_scope"], OutOfScope: "ambiguous", Confidence: 0, Fallback: true);
        var usage = new[] { Call("ollama", "slm_intents", ok: false), Call("ollama", "slm_intents", ok: false) };

        Assert.Equal(ChatOutcomes.Failed, ChatTurn.Outcome(slm, usage));
    }

    [Fact]
    public void FailedSteps_NamesTheSkill_OrTheSourceWhenThereIsNone()
    {
        var usage = new[] { Call("claude", "skill_explain", ok: false), Call("ollama", "", ok: false), Call("ollama", "slm_intents") };

        Assert.Equal(["skill_explain", "ollama"], ChatTurn.FailedSteps(usage));
    }

    // ---- review_status

    [Fact]
    public void ReviewStatus_IsOpenOnlyWhenTheTurnWasNotAnswered()
    {
        Assert.Null(ChatTurn.ReviewStatus(ChatOutcomes.Answered));
        Assert.Equal(ChatReviewStatuses.Open, ChatTurn.ReviewStatus(ChatOutcomes.NoAnswer));
        Assert.Equal(ChatReviewStatuses.Open, ChatTurn.ReviewStatus(ChatOutcomes.Failed));
    }

    // ---- meta_json

    [Fact]
    public void UserMeta_HoldsTheLabelTheModelGaveToTheQuestion()
    {
        var entities = JsonDocument.Parse("{\"subtype\":\"ambiguous\"}").RootElement;
        var slm = new AdvisorSlmDto(
            [new AdvisorSlmIntentDto("out_of_scope", "asdfgh", entities)], ["out_of_scope"], "ambiguous", 0.2, false);

        var meta = JsonDocument.Parse(ChatTurn.UserMeta(slm)!).RootElement;

        Assert.Equal(ChatTurn.MetaVersion, meta.GetProperty("v").GetInt32());
        var label = meta.GetProperty("slm").GetProperty("modelIntents")[0];
        Assert.Equal("out_of_scope", label.GetProperty("type").GetString());
        Assert.Equal("asdfgh", label.GetProperty("span").GetString());
        Assert.Equal("ambiguous", label.GetProperty("entities").GetProperty("subtype").GetString());
        Assert.False(meta.TryGetProperty("local", out _));   // số đo của AI thuộc về tin trả lời, không phải câu hỏi
    }

    [Fact]
    public void UserMeta_IsNullWhenTheTurnDidNotGoThroughTheSlm() => Assert.Null(ChatTurn.UserMeta(null));

    [Fact]
    public void UserMeta_CanBeFilteredByFallbackInSql_SoTheKeyIsCamelCase()
    {
        var meta = JsonDocument.Parse(ChatTurn.UserMeta(new AdvisorSlmDto(Fallback: true))!).RootElement;

        Assert.True(meta.GetProperty("slm").GetProperty("fallback").GetBoolean());
    }

    [Fact]
    public void ReplyMeta_SplitsLocalAiFromCloudAi()
    {
        var usage = new[]
        {
            Call("ollama", "slm_intents", latency: 21000),
            Call("claude", "skill_tra_loi", latency: 2000),
            Call("claude", "skill_explain", latency: 1000),
        };

        var meta = JsonDocument.Parse(ChatTurn.ReplyMeta(usage, "recommend", ["skill_tra_loi"], 23500)).RootElement;

        Assert.Equal(23500, meta.GetProperty("turnMs").GetInt64());
        Assert.Equal("recommend", meta.GetProperty("action").GetString());
        var local = meta.GetProperty("local");
        Assert.Equal((1, 100, 10, 21000), (local.GetProperty("calls").GetInt32(), local.GetProperty("inputTokens").GetInt32(),
            local.GetProperty("outputTokens").GetInt32(), local.GetProperty("latencyMs").GetInt32()));
        var cloud = meta.GetProperty("cloud");
        Assert.Equal((2, 200, 20, 3000), (cloud.GetProperty("calls").GetInt32(), cloud.GetProperty("inputTokens").GetInt32(),
            cloud.GetProperty("outputTokens").GetInt32(), cloud.GetProperty("latencyMs").GetInt32()));
        Assert.False(meta.TryGetProperty("other", out _));
        Assert.False(meta.TryGetProperty("slm", out _));      // nhãn thuộc về tin hỏi
    }

    [Fact]
    public void ReplyMeta_KeepsAnUnknownSourceInOther_SoNoDataIsLost()
    {
        var meta = JsonDocument.Parse(ChatTurn.ReplyMeta([Call("gemini", "x")], null, null, 5)).RootElement;

        Assert.Equal(1, meta.GetProperty("other").GetProperty("calls").GetInt32());
        Assert.Equal(0, meta.GetProperty("local").GetProperty("calls").GetInt32());
    }

    [Fact]
    public void ReplyMeta_WithoutUsage_HasEmptyBuckets()
    {
        var meta = JsonDocument.Parse(ChatTurn.ReplyMeta(null, null, null, 5)).RootElement;

        Assert.Equal(0, meta.GetProperty("local").GetProperty("calls").GetInt32());
        Assert.Equal(0, meta.GetProperty("cloud").GetProperty("inputTokens").GetInt32());
        Assert.False(meta.TryGetProperty("action", out _));   // khóa null không ghi, tránh nhiễu
    }

    // ---- đọc khối slm từ JSON advisor gửi

    [Fact]
    public void ChatResponse_ReadsTheSlmBlockOfTheAdvisor()
    {
        const string json = "{\"answer\":\"x\",\"citations\":[],\"slm\":{\"modelIntents\":[{\"type\":\"knowledge\",\"span\":\"I-20 là gì\"," +
            "\"entities\":{\"tasks\":[\"school_info\"]}}],\"intents\":[\"knowledge\"],\"outOfScope\":null,\"confidence\":0.88,\"fallback\":false}}";

        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(["knowledge"], res.Slm!.Intents);
        Assert.Equal(0.88, res.Slm.Confidence);
        Assert.Equal("I-20 là gì", res.Slm.ModelIntents![0].Span);
        Assert.Equal("school_info", res.Slm.ModelIntents[0].Entities!.Value.GetProperty("tasks")[0].GetString());
    }

    [Fact]
    public void OldAdvisorWithoutSlmBlock_StillReads()
    {
        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(
            "{\"answer\":\"x\",\"citations\":[]}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(res.Slm);
    }
}
