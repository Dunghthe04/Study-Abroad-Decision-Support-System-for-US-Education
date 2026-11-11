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
    public void Meta_HoldsModelLabelAndTotals()
    {
        var entities = JsonDocument.Parse("{\"subtype\":\"ambiguous\"}").RootElement;
        var slm = new AdvisorSlmDto(
            [new AdvisorSlmIntentDto("out_of_scope", "asdfgh", entities)], ["out_of_scope"], "ambiguous", 0.2, false);
        var usage = new[] { Call("ollama", "slm_intents", latency: 21000), Call("claude", "skill_tra_loi", latency: 2000) };

        var meta = JsonDocument.Parse(ChatTurn.Meta(slm, usage, "recommend", ["skill_tra_loi"], 23500)).RootElement;

        Assert.Equal(ChatTurn.MetaVersion, meta.GetProperty("v").GetInt32());
        Assert.Equal(23500, meta.GetProperty("turnMs").GetInt64());
        Assert.Equal(23000, meta.GetProperty("aiLatencyMs").GetInt32());
        Assert.Equal(2, meta.GetProperty("aiCalls").GetInt32());
        Assert.Equal(200, meta.GetProperty("inputTokens").GetInt32());
        Assert.Equal(20, meta.GetProperty("outputTokens").GetInt32());
        Assert.Equal("recommend", meta.GetProperty("action").GetString());
        var label = meta.GetProperty("slm").GetProperty("modelIntents")[0];
        Assert.Equal("out_of_scope", label.GetProperty("type").GetString());
        Assert.Equal("asdfgh", label.GetProperty("span").GetString());
        Assert.Equal("ambiguous", label.GetProperty("entities").GetProperty("subtype").GetString());
    }

    [Fact]
    public void Meta_CanBeFilteredByFallbackInSql_SoTheKeyIsCamelCase()
    {
        var meta = JsonDocument.Parse(ChatTurn.Meta(new AdvisorSlmDto(Fallback: true), null, null, null, 10)).RootElement;

        Assert.True(meta.GetProperty("slm").GetProperty("fallback").GetBoolean());
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
