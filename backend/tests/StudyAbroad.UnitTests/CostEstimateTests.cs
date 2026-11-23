using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Profile.Flow;
using Xunit;

namespace StudyAbroad.UnitTests;

public class CostEstimateTests
{
    private static SchoolCostRow Row(string code = "MIT", decimal? tuition = 50_000m, decimal? living = 20_000m, decimal? insurance = 3_000m,
        string level = "undergraduate", string? year = "2026-27") =>
        new(code, code, "MA", level, year, tuition, living, 2_000m, null, insurance, null, null);

    private static readonly ReferenceFees Fees = new(185m, 350m);

    [Fact]
    public void Build_OneTimeFeesCountedOnceInTotal()
    {
        var s = CostCalculator.Build(Row(), Fees, 0m, null);

        Assert.Equal(75_000m, s.AnnualUsd);          // 50k + 20k + 2k + 3k
        Assert.Equal(75_535m, s.FirstYearUsd);       // + 185 + 350
        Assert.Equal(75_000m * 4 + 535m, s.TotalUsd);
        Assert.True(s.IsComplete);
    }

    [Fact]
    public void Build_MissingItemIsNotZero_AndReportedMissing()
    {
        var s = CostCalculator.Build(Row(insurance: null), new ReferenceFees(null, null), 0m, null);

        Assert.False(s.IsComplete);
        Assert.Equal(["insurance", "visa", "sevis"], s.MissingKeys);
        Assert.Null(s.Lines.Single(l => l.Key == "visa").AmountUsd);
        Assert.Equal(72_000m, s.AnnualUsd);          // chỉ cộng khoản đã có
    }

    [Fact]
    public void Build_ScholarshipAssumption_ReducesAnnualAndTotal()
    {
        var s = CostCalculator.Build(Row(), Fees, 10_000m, null);

        Assert.Equal(65_000m, s.AnnualAfterScholarshipUsd);
        Assert.Equal(75_000m * 4 + 535m - 40_000m, s.TotalAfterScholarshipUsd);
    }

    [Fact]
    public void Build_BudgetCap_ReportsShortfallAndSurplus()
    {
        var short1 = CostCalculator.Build(Row(), Fees, 0m, new BudgetInput("profile", 60_000m, false));
        var surplus = CostCalculator.Build(Row(), Fees, 0m, new BudgetInput("profile", 80_000m, false));

        Assert.Equal(15_000m, short1.Budget!.AnnualGapUsd);
        Assert.Equal(-5_000m, surplus.Budget!.AnnualGapUsd);
        Assert.Equal(75_000m * 4 + 535m - 60_000m * 4, short1.Budget.TotalGapUsd);
    }

    [Fact]
    public void Build_UnlimitedBudget_HasNoGap()
    {
        var s = CostCalculator.Build(Row(), Fees, 0m, new BudgetInput("profile", null, true));

        Assert.True(s.Budget!.Unlimited);
        Assert.Null(s.Budget.AnnualGapUsd);
    }

    [Fact]
    public void Build_NoBudget_OmitsComparison() =>
        Assert.Null(CostCalculator.Build(Row(), Fees, 0m, null).Budget);

    [Fact]
    public void Build_TuitionMissing_NoTotalsAndNoBudgetGap_KeepsLinesAndMissingKeys()
    {
        var s = CostCalculator.Build(Row(tuition: null), Fees, 10_000m, new BudgetInput("profile", 60_000m, false));

        Assert.Null(s.AnnualUsd);
        Assert.Null(s.FirstYearUsd);
        Assert.Null(s.TotalUsd);
        Assert.Null(s.AnnualAfterScholarshipUsd);
        Assert.Null(s.TotalAfterScholarshipUsd);
        Assert.Null(s.Budget!.AnnualGapUsd);
        Assert.Null(s.Budget.TotalGapUsd);
        Assert.Equal(60_000m, s.Budget.AnnualBudgetUsd);
        Assert.False(s.IsComplete);
        Assert.Contains("tuition", s.MissingKeys);
        Assert.Equal(7, s.Lines.Count);
    }

    [Fact]
    public void Build_PhdHasNoMultiYearTotal()
    {
        var s = CostCalculator.Build(Row(level: "phd"), Fees, 0m, null);
        Assert.Null(s.Years);
        Assert.Null(s.TotalUsd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    [InlineData("""{"visaFeeUsd":"abc","sevisFeeUsd":-5}""")]
    public void ParseFees_BadOrMissing_GivesNulls(string? json)
    {
        var f = CostService.ParseFees(json);
        Assert.Null(f.VisaFeeUsd);
        Assert.Null(f.SevisFeeUsd);
    }

    [Fact]
    public void ParseFees_ReadsAmounts()
    {
        var f = CostService.ParseFees("""{"visaFeeUsd":185,"sevisFeeUsd":350}""");
        Assert.Equal(185m, f.VisaFeeUsd);
        Assert.Equal(350m, f.SevisFeeUsd);
    }

    private sealed class FakeRepo(params SchoolCostRow[] rows) : ICostRepository
    {
        public Guid? ProfileLookup;
        public Task<IReadOnlyList<SchoolCostRow>> GetSchoolCostsAsync(IReadOnlyList<string> codes, string level, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchoolCostRow>>(rows.Where(r => codes.Contains(r.Code, StringComparer.OrdinalIgnoreCase)).ToList());
        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<BudgetInput?> GetProfileBudgetAsync(Guid userId, CancellationToken ct = default)
        {
            ProfileLookup = userId;
            return Task.FromResult<BudgetInput?>(new BudgetInput("profile", 40_000m, false));
        }
        public IReadOnlyList<StateLivingCostDto> States { get; init; } = [];
        public Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default) =>
            Task.FromResult(States);
    }

    private sealed class FakeRates : IExchangeRateProvider
    {
        public Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default) => Task.FromResult(25_000m);
    }

    [Fact]
    public async Task Estimate_Guest_HasNoBudget_AndReportsUnknownCodeAndYearMismatch()
    {
        var repo = new FakeRepo(Row("A", year: "2026-27"), Row("B", year: "2025-26"));
        var r = await new CostService(repo, new FakeRates()).EstimateAsync(["A", "B", "ZZZ"], null, null, null, null);

        Assert.All(r.Schools, s => Assert.Null(s.Budget));
        Assert.Equal(["ZZZ"], r.MissingCodes);
        Assert.True(r.AcademicYearsDiffer);
        Assert.Equal(25_000m, r.Rate.VndPerUsd);
    }

    [Fact]
    public async Task Estimate_UsesProfileBudget_ButTypedBudgetWins()
    {
        var repo = new FakeRepo(Row("A"));
        var service = new CostService(repo, new FakeRates());
        var user = Guid.NewGuid();

        var fromProfile = await service.EstimateAsync(["A"], null, null, null, user);
        var typed = await service.EstimateAsync(["A"], null, null, 90_000m, user);

        Assert.Equal("profile", fromProfile.Schools[0].Budget!.Source);
        Assert.Equal(40_000m, fromProfile.Schools[0].Budget!.AnnualBudgetUsd);
        Assert.Equal("input", typed.Schools[0].Budget!.Source);
        Assert.Equal(90_000m, typed.Schools[0].Budget!.AnnualBudgetUsd);
    }

    [Fact]
    public async Task UnsafeSourceUrls_AreStrippedInCostAndStateOutputs()
    {
        var row = Row("A") with { SourceUrl = "javascript:alert(1)" };
        var repo = new FakeRepo(row)
        {
            States = [
                new StateLivingCostDto("CA", "California", "West", 112m, 120m, 2025, "javascript:alert(1)", null),
                new StateLivingCostDto("TX", "Texas", "South", 97m, 95m, 2025, "https://bea.gov/rpp", null),
            ],
        };
        var service = new CostService(repo, new FakeRates());

        var estimate = await service.EstimateAsync(["A"], null, null, null, null);
        var states = await service.GetStateLivingCostsAsync();

        Assert.Null(estimate.Schools[0].SourceUrl);
        Assert.Null(states[0].SourceUrl);
        Assert.Equal("https://bea.gov/rpp", states[1].SourceUrl);
    }
}
