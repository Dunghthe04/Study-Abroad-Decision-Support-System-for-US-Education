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
            {"weights": {"academic": 0.6, "finance": 0.2, "extracurricular": 0.2},
             "academicParts": {"gpa": 0.5, "sat": 0.3, "english": 0.2},
             "aiTimeoutSeconds": 90, "extracurricularTimeoutSeconds": 20}
            """);
        Assert.Equal(new SawWeights(0.6m, 0.2m, 0.2m), s.Weights);
        Assert.Equal(new AcademicParts(0.5m, 0.3m, 0.2m), s.AcademicParts);
        Assert.Equal(90, s.AiTimeoutSeconds);
        Assert.Equal(20, s.ExtracurricularTimeoutSeconds);
    }

    [Fact]
    public void Parse_OldKeys_AreIgnored()
    {
        // Dạng cũ (financeWeight, extracurricularWeight, weights.english) không còn dùng → mặc định 0.5/0.3/0.2
        var s = RecommendSettings.Parse("""{"financeWeight": 1.0, "weights": {"english": 0.1}}""");
        Assert.Equal(new SawWeights(), s.Weights);
        Assert.Equal(new AcademicParts(), s.AcademicParts);
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
