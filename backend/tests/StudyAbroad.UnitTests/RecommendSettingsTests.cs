using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class RecommendSettingsTests
{
    [Fact]
    public void Parse_ReadsValuesFromJson()
    {
        var s = RecommendSettings.Parse("""
            {"budgetTolerance": 0.2, "aiTimeoutSeconds": 90}
            """);
        Assert.Equal(0.2m, s.BudgetTolerance);
        Assert.Equal(90, s.AiTimeoutSeconds);
        Assert.True(s.AiEnabled);   // key thiếu → giá trị mặc định
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    public void Parse_InvalidJson_FallsBackToDefaults(string? json)
    {
        var s = RecommendSettings.Parse(json);
        Assert.Equal(0.10m, s.BudgetTolerance);
        Assert.True(s.AiEnabled);
    }
}
