using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// Công thức tiền của một lần gọi AI: giá Haiku 5.5, bậc giá theo độ dài prompt, cache, và các trường hợp "không biết giá".
public class AiPricingTests
{
    private const string Haiku = "claude-haiku-5-5";

    [Fact]
    public void Haiku_ChargesInputAndOutputPerMillionTokens()
    {
        // Prompt ngắn (tới 100K): 0,10 USD mỗi triệu token vào, 0,50 USD mỗi triệu token ra
        Assert.Equal(0.01m, AiPricing.Cost("claude", Haiku, 100_000, 0, 0));
        Assert.Equal(0.05m, AiPricing.Cost("claude", Haiku, 0, 100_000, 0));
        Assert.Equal(0.00045m, AiPricing.Cost("claude", Haiku, 2000, 500, 0));
        // Prompt trên 100K: giá bậc dài, 0,50 USD mỗi triệu token vào
        Assert.Equal(0.50m, AiPricing.Cost("claude", Haiku, 1_000_000, 0, 0));
    }

    [Fact]
    public void ShortPrompt_AtTheThreshold_UsesTheNormalRate_AndOneTokenOverUsesTheLongRate()
    {
        Assert.Equal(100_000 * 0.10m / 1_000_000m, AiPricing.Cost("claude", Haiku, AiPricing.LongPromptThreshold, 0, 0));
        // vượt 100K thì cả lần gọi tính giá bậc dài (0,50 vào, 2,50 ra)
        Assert.Equal(100_001 * 0.50m / 1_000_000m + 1000 * 2.50m / 1_000_000m,
            AiPricing.Cost("claude", Haiku, AiPricing.LongPromptThreshold + 1, 1000, 0));
    }

    [Fact]
    public void LongPromptTier_CountsTheCachedPartOfThePrompt()
    {
        // 60K vào + 60K đọc từ cache = prompt 120K: bậc dài, dù phần "vào" riêng chưa tới 100K
        var cost = AiPricing.Cost("claude", Haiku, 60_000, 0, 60_000);

        Assert.Equal((60_000 * 0.50m + 60_000 * 0.50m) / 1_000_000m, cost);   // giá cache chưa xác nhận nên tạm bằng giá vào của bậc đó
    }

    [Fact]
    public void CachedTokens_WithoutAConfirmedCachePrice_AreChargedAtTheInputRate_NeverFree()
    {
        var withCache = AiPricing.Cost("claude", Haiku, 1000, 0, 4000);
        var asPlainInput = AiPricing.Cost("claude", Haiku, 5000, 0, 0);

        Assert.Equal(asPlainInput, withCache);   // tính dư một chút còn hơn báo thiếu tiền
    }

    [Fact]
    public void Ollama_IsFree_WhateverTheModelOrTokens()
    {
        Assert.Equal(0m, AiPricing.Cost("ollama", "usas-slm-v4", 5000, 800, 0));
        Assert.Equal(0m, AiPricing.Cost("OLLAMA", null, 5000, 800, 0));
    }

    [Theory]
    [InlineData("claude", "claude-model-moi")]
    [InlineData("claude", null)]
    [InlineData("claude", "")]
    [InlineData(null, Haiku)]
    [InlineData("openai", Haiku)]
    public void UnknownPrice_IsNull_NotZero(string? kind, string? model)
    {
        Assert.Null(AiPricing.Cost(kind, model, 1000, 100, 0));
    }

    [Fact]
    public void ModelNameMatching_IgnoresCaseAndSurroundingSpaces()
    {
        Assert.Equal(AiPricing.Cost("claude", Haiku, 1000, 100, 0), AiPricing.Cost("Claude", "  CLAUDE-HAIKU-5-5 ", 1000, 100, 0));
    }

    [Fact]
    public void NegativeTokens_AreTreatedAsZero_SoCostIsNeverNegative()
    {
        Assert.Equal(0m, AiPricing.Cost("claude", Haiku, -5, -5, -5));
    }
}
