using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// Đổi số đo advisor gửi (AiUsageDto) thành dòng ai_calls: đúng cột, đúng nghĩa, cắt theo độ rộng cột.
public class AiCallMapperTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Message = Guid.NewGuid();

    [Fact]
    public void MapsEveryFieldToItsColumn()
    {
        var usage = new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.1", 2382, 126, 7, true, null, 810);

        var call = AiCallMapper.ToEntity(usage, User, Message);

        Assert.Equal(("claude", "skill_tra_loi", "1.1", "claude-haiku-5-5"), (call.Kind, call.Purpose, call.SkillVersion, call.Model));
        Assert.Equal((2382, 126, 7), (call.PromptTokens, call.CompletionTokens, call.CachedTokens));
        Assert.Equal((810, true, null), (call.LatencyMs, call.Success, call.Error));
        Assert.Equal((User, Message), (call.UserId, call.ChatMessageId));
    }

    [Fact]
    public void TotalIsInputPlusOutput_CachedTokensAreCountedSeparately()
    {
        var call = AiCallMapper.ToEntity(new AiUsageDto("claude", "m", "p", "1", 100, 20, 50, true, null, 1), User, null);

        Assert.Equal(120, call.TotalTokens);
        Assert.Equal(50, call.CachedTokens);
    }

    [Fact]
    public void FailedCall_KeepsTheReasonAndSuccessFalse()
    {
        var call = AiCallMapper.ToEntity(new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 0, 0, 0, false, "timeout", 30000), User, null);

        Assert.False(call.Success);
        Assert.Equal("timeout", call.Error);
        Assert.Equal((0, 0), (call.PromptTokens, call.CompletionTokens));
    }

    [Fact]
    public void CallWithoutAMessage_HasNoLink()
    {
        var call = AiCallMapper.ToEntity(new AiUsageDto("ollama", "m"), User, null);

        Assert.Null(call.ChatMessageId);
    }

    [Fact]
    public void ClaudeCall_StoresItsCost_AtTheTimeOfTheCall()
    {
        // 2000 vào x 0,10 + 500 ra x 0,50 (USD mỗi triệu token) = 0,00045
        var call = AiCallMapper.ToEntity(new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.1", 2000, 500, 0, true, null, 800), User, null);

        Assert.Equal(0.00045m, call.CostUsd);
    }

    [Fact]
    public void OllamaCall_CostsNothing_AndAFailedCallCostsNothing()
    {
        var local = AiCallMapper.ToEntity(new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 1700, 180, 0, true, null, 900), User, null);
        var failed = AiCallMapper.ToEntity(new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.1", 0, 0, 0, false, "timeout", 30000), User, null);
        var failedUnknownModel = AiCallMapper.ToEntity(new AiUsageDto("claude", "", "skill_tra_loi", "1.1", 0, 0, 0, false, "timeout", 30000), User, null);

        Assert.Equal(0m, local.CostUsd);
        Assert.Equal(0m, failed.CostUsd);
        Assert.Equal(0m, failedUnknownModel.CostUsd);   // lỗi thì chưa tiêu token: 0, không phải "chưa biết giá"
    }

    [Fact]
    public void ClaudeCallWithAnUnknownModel_LeavesTheCostEmpty_NotZero()
    {
        var call = AiCallMapper.ToEntity(new AiUsageDto("claude", "claude-model-moi", "skill_tra_loi", "1.1", 1000, 100, 0, true, null, 500), User, null);

        Assert.Null(call.CostUsd);
    }

    [Fact]
    public void EmptyPurposeAndVersion_BecomeNull_SoEmbeddingCallsAreNotLabelledWithAnEmptyText()
    {
        var call = AiCallMapper.ToEntity(new AiUsageDto("ollama", "bge-m3", "", "", 7, 0, 0, true, null, 5), User, null);

        Assert.Null(call.Purpose);
        Assert.Null(call.SkillVersion);
    }

    [Fact]
    public void LongValues_AreCutToTheColumnWidth_SoOneOddValueCannotBreakTheWholeWrite()
    {
        var usage = new AiUsageDto(new string('s', 40), new string('m', 200), new string('p', 200), "1", 1, 1, 0, false, new string('e', 5000), 1);

        var call = AiCallMapper.ToEntity(usage, User, null);

        Assert.Equal(16, call.Kind.Length);
        Assert.Equal(64, call.Purpose!.Length);
        Assert.Equal(64, call.Model!.Length);
        Assert.Equal(1000, call.Error!.Length);
    }
}
