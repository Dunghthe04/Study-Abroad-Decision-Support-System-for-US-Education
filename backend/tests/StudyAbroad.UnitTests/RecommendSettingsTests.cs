using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class RecommendSettingsTests
{
    [Fact]
    public void Parse_ReadsValuesFromJson()
    {
        var s = RecommendSettings.Parse("""
            {"gpaBand": 0.2, "maxResults": 8, "perCategory": {"reach": 2, "match": 4, "safety": 2}}
            """);
        Assert.Equal(0.2m, s.GpaBand);
        Assert.Equal(8, s.MaxResults);
        Assert.Equal(2, s.PerCategory.Reach);
        Assert.Equal(0.10m, s.BudgetTolerance);   // key thiếu → giá trị mặc định
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    public void Parse_InvalidJson_FallsBackToDefaults(string? json)
    {
        var s = RecommendSettings.Parse(json);
        Assert.Equal(0.3m, s.GpaBand);
        Assert.Equal(12, s.MaxResults);
    }
}
