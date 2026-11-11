using Microsoft.Extensions.Configuration;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

/// <summary>Bảng giá dùng trong test: cùng giá Haiku 5.5 như appsettings.json.</summary>
internal static class TestPricing
{
    public static AiPricing Haiku() => new(new AiPricingOptions
    {
        LongPromptThreshold = 100_000,
        Models =
        {
            ["claude-haiku-5-5"] = new ModelPrice
            {
                InputPerMTok = 0.10m, OutputPerMTok = 0.50m, LongInputPerMTok = 0.50m, LongOutputPerMTok = 2.50m,
            },
        },
    });
}

// Công thức tiền của một lần gọi AI: giá đọc từ cấu hình, bậc giá theo độ dài prompt, và các trường hợp "không biết giá".
public class AiPricingTests
{
    private const string Haiku = "claude-haiku-5-5";
    private static readonly AiPricing Pricing = TestPricing.Haiku();

    [Fact]
    public void Catalog_ListsTheModelsThatCanBePriced_WithTheSamePricesTheCostUses()
    {
        var catalog = Pricing.Catalog();

        var haiku = Assert.Single(catalog, price => price.Model == Haiku);
        Assert.Equal(0.10m, haiku.InputPerMTok);
        Assert.Equal(0.50m, haiku.OutputPerMTok);
        Assert.Equal(100_000, haiku.LongPromptThreshold);
        // Mỗi model trong bảng phải tính ra tiền (khác null), nếu không trang admin hiện giá mà báo cáo lại báo "chưa biết giá"
        Assert.All(catalog, price => Assert.NotNull(Pricing.Cost("claude", price.Model, 1000, 1000)));
    }

    [Fact]
    public void Haiku_ChargesInputAndOutputPerMillionTokens()
    {
        // Prompt ngắn (tới 100K): 0,10 USD mỗi triệu token vào, 0,50 USD mỗi triệu token ra
        Assert.Equal(0.01m, Pricing.Cost("claude", Haiku, 100_000, 0));
        Assert.Equal(0.05m, Pricing.Cost("claude", Haiku, 0, 100_000));
        Assert.Equal(0.00045m, Pricing.Cost("claude", Haiku, 2000, 500));
        // Prompt trên 100K: giá bậc dài, 0,50 USD mỗi triệu token vào
        Assert.Equal(0.50m, Pricing.Cost("claude", Haiku, 1_000_000, 0));
    }

    [Fact]
    public void ShortPrompt_AtTheThreshold_UsesTheNormalRate_AndOneTokenOverUsesTheLongRate()
    {
        Assert.Equal(100_000 * 0.10m / 1_000_000m, Pricing.Cost("claude", Haiku, Pricing.LongPromptThreshold, 0));
        // vượt 100K thì cả lần gọi tính giá bậc dài (0,50 vào, 2,50 ra)
        Assert.Equal(100_001 * 0.50m / 1_000_000m + 1000 * 2.50m / 1_000_000m,
            Pricing.Cost("claude", Haiku, Pricing.LongPromptThreshold + 1, 1000));
    }

    [Fact]
    public void Ollama_IsFree_WhateverTheModelOrTokens()
    {
        Assert.Equal(0m, Pricing.Cost("ollama", "usas-slm-v4", 5000, 800));
        Assert.Equal(0m, Pricing.Cost("OLLAMA", null, 5000, 800));
    }

    [Theory]
    [InlineData("claude", "claude-model-moi")]
    [InlineData("claude", null)]
    [InlineData("claude", "")]
    [InlineData(null, Haiku)]
    [InlineData("openai", Haiku)]
    public void UnknownPrice_IsNull_NotZero(string? kind, string? model)
    {
        Assert.Null(Pricing.Cost(kind, model, 1000, 100));
    }

    [Fact]
    public void ModelNameMatching_IgnoresCaseAndSurroundingSpaces()
    {
        Assert.Equal(Pricing.Cost("claude", Haiku, 1000, 100), Pricing.Cost("Claude", "  CLAUDE-HAIKU-5-5 ", 1000, 100));
    }

    [Fact]
    public void NegativeTokens_AreTreatedAsZero_SoCostIsNeverNegative()
    {
        Assert.Equal(0m, Pricing.Cost("claude", Haiku, -5, -5));
    }

    [Fact]
    public void WithoutAnyConfiguredPrice_EveryClaudeCallIsUnpriced_ButLocalStaysFree()
    {
        Assert.Null(AiPricing.None.Cost("claude", Haiku, 1000, 100));
        Assert.Equal(0m, AiPricing.None.Cost("ollama", "usas-slm-v4", 1000, 100));
        Assert.Empty(AiPricing.None.Catalog());
    }

    [Fact]
    public void LongTierPrices_WhenLeftOut_FallBackToTheNormalPrices()
    {
        var pricing = new AiPricing(new AiPricingOptions
        {
            Models = { ["claude-x"] = new ModelPrice { InputPerMTok = 1m, OutputPerMTok = 2m } },
        });

        Assert.Equal(200_000 * 1m / 1_000_000m, pricing.Cost("claude", "claude-x", 200_000, 0));
        var row = Assert.Single(pricing.Catalog());
        Assert.Equal(1m, row.LongInputPerMTok);
        Assert.Equal(2m, row.LongOutputPerMTok);
    }

    [Fact]
    public void ANegativePrice_IsAConfigurationMistake_SoThatModelBecomesUnpricedInsteadOfPayingNegativeMoney()
    {
        var pricing = new AiPricing(new AiPricingOptions
        {
            Models = { ["claude-bad"] = new ModelPrice { InputPerMTok = -1m, OutputPerMTok = 2m } },
        });

        Assert.Null(pricing.Cost("claude", "claude-bad", 1000, 100));
        Assert.Empty(pricing.Catalog());
    }

    [Fact]
    public void PricesAreReadFromConfiguration_AndAnEnvironmentStyleKeyOverridesTheDefault()
    {
        // appsettings.json đặt giá mặc định; biến môi trường AiPricing__Models__claude-haiku-5-5__InputPerMTok ghi đè đúng một số
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiPricing:Models:claude-haiku-5-5:InputPerMTok"] = "0.10",
                ["AiPricing:Models:claude-haiku-5-5:OutputPerMTok"] = "0.50",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiPricing:Models:claude-haiku-5-5:InputPerMTok"] = "0.20",
            })
            .Build();

        var options = config.GetSection(AiPricingOptions.SectionName).Get<AiPricingOptions>()!;
        var pricing = new AiPricing(options);

        // 1.000.000 token vào x 0,20 + 0 ra: giá vào mới, giá ra giữ nguyên
        Assert.Equal(0.20m, pricing.Cost("claude", "claude-haiku-5-5", 100_000, 0) * 10);
        Assert.Equal(0.05m, pricing.Cost("claude", "claude-haiku-5-5", 0, 100_000));
    }
}
