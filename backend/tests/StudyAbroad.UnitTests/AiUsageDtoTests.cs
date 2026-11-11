using System.Text.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// Backend phải đọc được khóa aiUsage mà advisor gửi (camelCase). Chuỗi JSON dưới đây theo đúng khuôn advisor/app/schemas/chat.py: AiUsageCall.
public class AiUsageDtoTests
{
    private const string Usage =
        "\"aiUsage\":[{\"source\":\"claude\",\"model\":\"claude-haiku-5-5\",\"purpose\":\"skill_tra_loi\",\"skillVersion\":\"1.1\"," +
        "\"inputTokens\":2382,\"outputTokens\":126,\"cachedTokens\":7,\"ok\":true,\"error\":null,\"latencyMs\":810}," +
        "{\"source\":\"ollama\",\"model\":\"usas-slm-v4\",\"purpose\":\"slm_intents\",\"skillVersion\":\"3.0\"," +
        "\"inputTokens\":0,\"outputTokens\":0,\"cachedTokens\":0,\"ok\":false,\"error\":\"timeout\",\"latencyMs\":30000}]";

    // Cấu hình AdvisorClient dùng cho 4 phản hồi viết lời; chat dùng cấu hình Web mặc định
    private static readonly JsonSerializerOptions PostJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ChatResponse_ReadsEveryFieldOfEachCall()
    {
        var res = JsonSerializer.Deserialize<AdvisorChatResponse>($"{{\"answer\":\"x\",\"citations\":[],{Usage}}}", WebJson)!;

        Assert.Equal(2, res.AiUsage!.Count);
        Assert.Equal(new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.1", 2382, 126, 7, true, null, 810), res.AiUsage[0]);
        Assert.Equal(new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 0, 0, 0, false, "timeout", 30000), res.AiUsage[1]);
    }

    [Fact]
    public void ExplainResponse_ReadsAiUsage_AndStillReadsSnakeCaseSkillVersion()
    {
        var json = $"{{\"skill_version\":\"2.0\",\"model\":\"m\",\"status\":\"OK\",\"summary\":\"s\",\"strengths\":[],\"improvements\":[],{Usage}}}";

        var res = JsonSerializer.Deserialize<ExplainResponseDto>(json, PostJson)!;

        Assert.Equal("2.0", res.SkillVersion);
        Assert.Equal(["skill_tra_loi", "slm_intents"], res.AiUsage!.Select(u => u.Purpose));
    }

    [Fact]
    public void NoteResponses_ReadAiUsage()
    {
        var json = $"{{\"skill_version\":\"1.0\",\"model\":\"m\",\"status\":\"OK\",\"answer\":\"a\",{Usage}}}";

        Assert.Equal(2, JsonSerializer.Deserialize<ScenarioNoteResponseDto>(json, PostJson)!.AiUsage!.Count);
        Assert.Equal(2, JsonSerializer.Deserialize<CompareNoteResponseDto>(json, PostJson)!.AiUsage!.Count);
        Assert.Equal(2, JsonSerializer.Deserialize<GoalNoteResponseDto>(json, PostJson)!.AiUsage!.Count);
    }

    [Fact]
    public void EcResponse_ReadsAiUsage()
    {
        var res = JsonSerializer.Deserialize<EcTierResponse>($"{{\"tier\":3,\"ai_used\":true,{Usage}}}", PostJson)!;

        Assert.Equal(3, res.Tier);
        Assert.Equal(2, res.AiUsage!.Count);
    }

    [Fact]
    public void OldAdvisorWithoutAiUsage_StillReads_AndGivesNull()
    {
        var chat = JsonSerializer.Deserialize<AdvisorChatResponse>("{\"answer\":\"x\",\"citations\":[]}", WebJson)!;
        var note = JsonSerializer.Deserialize<GoalNoteResponseDto>(
            "{\"skill_version\":\"1.0\",\"model\":\"m\",\"status\":\"OK\",\"answer\":\"a\"}", PostJson)!;

        Assert.Null(chat.AiUsage);
        Assert.Null(note.AiUsage);
    }

    [Fact]
    public void CallMissingSomeFields_FallsBackToDefaults()
    {
        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(
            "{\"answer\":\"x\",\"citations\":[],\"aiUsage\":[{\"source\":\"ollama\",\"model\":\"m\"}]}", WebJson)!;

        var call = Assert.Single(res.AiUsage!);
        Assert.Equal(new AiUsageDto("ollama", "m"), call);   // token 0, ok = true, purpose rỗng
    }
}
