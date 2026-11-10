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

    [Fact]
    public void Validate_DefaultTable_IsValid()
    {
        Assert.Empty(GradeScale.Validate(AcademicAnalysisService.GetDefaultWesScaleConfig()));
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
        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, parsed!.Rules);
    }

    [Fact]
    public void TranscriptGpa_UsesGivenScale_OtherwiseDefault()
    {
        var step2 = new FlowTranscriptDraftDto(ManualGpa10Scale: 8.2m, GpaMode: ProfileConstants.GpaModes.Overall);

        Assert.Equal(3.5m, TranscriptGpa.Gpa4(step2)); // bảng mặc định: 8.0 → 3.5
        Assert.Equal(4.0m, TranscriptGpa.Gpa4(step2, CustomScale()));
        Assert.Equal(2.0m, TranscriptGpa.To4(7.9m, CustomScale()));
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
        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, (await provider.GetAsync()).Rules);

        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, "{\"rules\":[{\"minScore\":5,\"maxScore\":10,\"gpa4\":3,\"letterGrade\":\"B\"}]}");
        provider.Invalidate();
        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, (await provider.GetAsync()).Rules);
    }

    [Fact]
    public async Task Provider_CachesUntilInvalidated()
    {
        var repo = new FakeAcademicAnalysisRepository();
        var provider = new GradeScaleProvider(repo, new MemoryCache(new MemoryCacheOptions()));
        await provider.GetAsync(); // cache bảng mặc định

        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, JsonSerializer.Serialize(CustomScale()));
        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, (await provider.GetAsync()).Rules);

        provider.Invalidate();
        Assert.Equal(CustomScale().Rules, (await provider.GetAsync()).Rules);
    }

    [Fact]
    public async Task AcademicAnalysisService_InvalidTableInDb_ReturnsDefault()
    {
        var repo = new FakeAcademicAnalysisRepository();
        repo.SetSetting(AcademicConstants.GradeScale10AppSettingKey, "{\"rules\":[{\"minScore\":5,\"maxScore\":10,\"gpa4\":3,\"letterGrade\":\"B\"}]}");

        var config = await new AcademicAnalysisService(repo).GetGradeScaleConfigAsync();

        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, config.Rules);
    }
}
