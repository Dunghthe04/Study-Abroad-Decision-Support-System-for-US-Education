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

    [Fact]
    public void Parse_ReadsSawWeights()
    {
        // Đúng dạng dòng recommend.weights trong bảng app_settings
        var s = RecommendSettings.Parse("""
            {"weights": {"academic": 0.5, "finance": 0.2, "english": 0.1, "extracurricular": 0.2},
             "aiTimeoutSeconds": 90, "extracurricularTimeoutSeconds": 20}
            """);
        Assert.Equal(new SawWeights(0.5m, 0.2m, 0.1m, 0.2m), s.Weights);
        Assert.Equal(90, s.AiTimeoutSeconds);
        Assert.Equal(20, s.ExtracurricularTimeoutSeconds);
    }

    [Fact]
    public void Parse_OldKeys_AreIgnored()
    {
        // Dạng cũ (financeWeight, extracurricularWeight) không còn dùng → trọng số mặc định 0.4/0.3/0.1/0.2
        var s = RecommendSettings.Parse("""{"financeWeight": 1.0, "extracurricularWeight": 0.5}""");
        Assert.Equal(new SawWeights(), s.Weights);
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
