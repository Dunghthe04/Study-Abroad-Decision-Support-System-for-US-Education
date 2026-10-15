using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class RecommendSettingsTests
{
    [Fact]
    public void Parse_ReadsValuesFromJson()
    {
        var s = RecommendSettings.Parse("""
            {"maxResults": 8, "perCategory": {"reach": 2, "match": 4, "safety": 2}, "aiTimeoutSeconds": 90}
            """);
        Assert.Equal(8, s.MaxResults);
        Assert.Equal(2, s.PerCategory.Reach);
        Assert.Equal(90, s.AiTimeoutSeconds);
        Assert.Equal(0.10m, s.BudgetTolerance);   // key thiếu → giá trị mặc định
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    public void Parse_InvalidJson_FallsBackToDefaults(string? json)
    {
        var s = RecommendSettings.Parse(json);
        Assert.Equal(12, s.MaxResults);
        Assert.Equal(0.10m, s.BudgetTolerance);
    }
}
