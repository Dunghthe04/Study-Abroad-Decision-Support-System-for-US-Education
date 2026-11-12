using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// Bảng quy đổi grade_scale.10 đọc từ DB: kiểm tra hợp lệ, cache, và TranscriptGpa dùng đúng bảng được truyền vào
public class GradeScaleTests
{
    // Bảng thử khác bảng mặc định: từ 8.0 đã là 4.0, dưới 8.0 là 2.0
    private static GradeScaleConfigDto CustomScale() => new(
        AcademicConstants.GradeScale10AppSettingKey, "Bảng thử", "table",
        [new(0m, 7.99m, 2.0m, "C"), new(8.0m, 10m, 4.0m, "A")]);

    private static GradeScaleConfigDto WithRules(params GradeScaleRuleDto[] rules) =>
        new(AcademicConstants.GradeScale10AppSettingKey, "Bảng thử", "table", rules.ToList());

    // Mốc VEF (cận dưới mỗi khoảng) và hai mốc hệ thống 5→2.0, 0→0 (kế hoạch GPA weighted, mục 4)
    private static GradeScaleConfigDto VefScale() => new(
        AcademicConstants.GradeScale10AppSettingKey, "VEF", "linear",
        [
            new(0m, 0m, 0m, ""), new(5m, 5m, 2.0m, ""), new(7.0m, 7.0m, 3.00m, ""), new(7.2m, 7.2m, 3.10m, ""),
            new(7.4m, 7.4m, 3.20m, ""), new(7.6m, 7.6m, 3.30m, ""), new(7.8m, 7.8m, 3.40m, ""), new(8.0m, 8.0m, 3.50m, ""),
            new(8.2m, 8.2m, 3.55m, ""), new(8.4m, 8.4m, 3.60m, ""), new(8.6m, 8.6m, 3.65m, ""), new(8.8m, 8.8m, 3.70m, ""),
            new(9.0m, 9.0m, 3.75m, ""), new(9.2m, 9.2m, 3.80m, ""), new(9.5m, 9.5m, 3.85m, ""), new(9.75m, 9.75m, 3.90m, ""),
            new(10m, 10m, 4.00m, ""),
        ]);

    [Theory]
    [InlineData(10, 4.00)]   // đúng mốc cuối
    [InlineData(9.0, 3.75)]  // đúng mốc
    [InlineData(9.6, 3.87)]  // giữa 9.5→3.85 và 9.75→3.90
    [InlineData(8.5, 3.625)] // giữa 8.4→3.60 và 8.6→3.65, không làm tròn
    [InlineData(6.0, 2.50)]  // giữa 5→2.0 và 7→3.0
    [InlineData(4.0, 1.60)]  // giữa 0→0 và 5→2.0
    [InlineData(0, 0)]       // đúng mốc đầu
    public void Interpolate_VefAnchors_IsLinearBetweenAnchors(double score, double expected)
    {
        Assert.Equal((decimal)expected, GradeScale.Interpolate((decimal)score, VefScale()));
    }

    [Fact]
    public void Interpolate_UnsortedAnchors_SameResult()
    {
        var shuffled = VefScale() with { Rules = VefScale().Rules.AsEnumerable().Reverse().ToList() };

        Assert.Equal(3.625m, GradeScale.Interpolate(8.5m, shuffled));
    }

    [Fact]
    public void Interpolate_OutsideAnchors_ClampsToEnds()
    {
        // Bảng chỉ có 0→0 và 8→3.5: trên mốc cuối giữ GPA của mốc cuối
        var scale = WithRules(new GradeScaleRuleDto(0m, 0m, 0m, ""), new GradeScaleRuleDto(8m, 8m, 3.5m, ""));

        Assert.Equal(3.5m, GradeScale.Interpolate(9.5m, scale));
        Assert.Equal(0m, GradeScale.Interpolate(-1m, scale));
    }

    [Fact]
    public void Validate_DefaultTable_IsValid()
    {
        Assert.Empty(GradeScale.Validate(AcademicAnalysisService.GetDefaultScaleConfig()));
    }

    [Fact]
    public void Validate_NullOrEmpty_HasError()
    {
        Assert.NotEmpty(GradeScale.Validate(null));
        Assert.NotEmpty(GradeScale.Validate(WithRules()));
    }

    [Fact]
    public void Validate_LowestBandNotZero_HasError()
    {
        Assert.NotEmpty(GradeScale.Validate(WithRules(new GradeScaleRuleDto(4m, 10m, 2m, "C"))));
    }

    [Fact]
    public void Validate_DuplicateMinScore_HasError()
    {
        Assert.NotEmpty(GradeScale.Validate(WithRules(new GradeScaleRuleDto(0m, 5m, 1m, "D"), new GradeScaleRuleDto(5m, 7m, 2m, "C"), new GradeScaleRuleDto(5m, 10m, 3m, "B"))));
    }

    [Fact]
    public void Validate_GpaDecreasesAsScoreRises_HasError()
    {
        Assert.NotEmpty(GradeScale.Validate(WithRules(new GradeScaleRuleDto(0m, 7.99m, 3m, "B"), new GradeScaleRuleDto(8m, 10m, 2m, "C"))));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(11, 2)]
    [InlineData(5, 4.5)]
    [InlineData(5, -0.5)]
    public void Validate_OutOfRange_HasError(double minScore, double gpa4)
    {
        Assert.NotEmpty(GradeScale.Validate(WithRules(new GradeScaleRuleDto(0m, 4.99m, 0m, "F"), new GradeScaleRuleDto((decimal)minScore, 10m, (decimal)gpa4, "X"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    [InlineData("{\"rules\":[]}")]
    [InlineData("{\"rules\":[{\"minScore\":5,\"maxScore\":10,\"gpa4\":3,\"letterGrade\":\"B\"}]}")]
    public void Parse_MissingBrokenOrInvalid_IsNull(string? json)
    {
        Assert.Null(GradeScale.Parse(json));
    }

    [Fact]
    public void DefaultJson_RoundTripsToDefaultTable()
    {
        var parsed = GradeScale.Parse(GradeScale.DefaultJson());

        Assert.NotNull(parsed);
        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, parsed!.Rules);
    }

    [Fact]
    public void TranscriptGpa_UsesGivenScale_OtherwiseDefault()
    {
        var step2 = new FlowTranscriptDraftDto(ManualGpa10Scale: 8.2m, GpaMode: ProfileConstants.GpaModes.Overall);

        Assert.Equal(3.55m, TranscriptGpa.Gpa4(step2)); // bảng mặc định: đúng mốc VEF 8.2 → 3.55
        Assert.Equal(4.0m, TranscriptGpa.Gpa4(step2, CustomScale())); // trên mốc cuối 8.0 → 4.0
        Assert.Equal(3.975m, TranscriptGpa.To4(7.9m, CustomScale())); // nội suy giữa 0 → 2.0 và 8.0 → 4.0
    }

    [Fact]
    public async Task Provider_ValidTableInDb_IsUsed()
    {
        var repo = new FakeAcademicAnalysisRepository();
        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, JsonSerializer.Serialize(CustomScale()));
        var provider = new GradeScaleProvider(repo, new MemoryCache(new MemoryCacheOptions()));

        var scale = await provider.GetAsync();

        Assert.Equal(CustomScale().Rules, scale.Rules);
    }

    [Fact]
    public async Task Provider_InvalidOrMissingTable_FallsBackToDefault()
    {
        var repo = new FakeAcademicAnalysisRepository();
        var provider = new GradeScaleProvider(repo, new MemoryCache(new MemoryCacheOptions()));
        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, (await provider.GetAsync()).Rules);

        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, "{\"rules\":[{\"minScore\":5,\"maxScore\":10,\"gpa4\":3,\"letterGrade\":\"B\"}]}");
        provider.Invalidate();
        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, (await provider.GetAsync()).Rules);
    }

    [Fact]
    public async Task Provider_CachesUntilInvalidated()
    {
        var repo = new FakeAcademicAnalysisRepository();
        var provider = new GradeScaleProvider(repo, new MemoryCache(new MemoryCacheOptions()));
        await provider.GetAsync(); // cache bảng mặc định

        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, JsonSerializer.Serialize(CustomScale()));
        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, (await provider.GetAsync()).Rules);

        provider.Invalidate();
        Assert.Equal(CustomScale().Rules, (await provider.GetAsync()).Rules);
    }

    [Fact]
    public async Task AcademicAnalysisService_InvalidTableInDb_ReturnsDefault()
    {
        var repo = new FakeAcademicAnalysisRepository();
        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, "{\"rules\":[{\"minScore\":5,\"maxScore\":10,\"gpa4\":3,\"letterGrade\":\"B\"}]}");

        var config = await new AcademicAnalysisService(repo).GetGradeScaleConfigAsync();

        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, config.Rules);
    }
}
