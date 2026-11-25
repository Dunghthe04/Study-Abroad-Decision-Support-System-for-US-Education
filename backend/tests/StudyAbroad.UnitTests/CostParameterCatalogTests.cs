using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-83: tham số của ước tính chi phí trên trang Căn cứ tham số. Chỉ xem; đọc dữ liệu đang chạy; chưa áp dụng thì nói rõ.
public class CostParameterCatalogTests
{
    private const string Fees =
        """{"visaFeeUsd":185,"visaSourceUrl":"https://travel.state.gov/visa","sevisFeeUsd":350,"sevisSourceUrl":"https://www.ice.gov/sevis"}""";

    private const string Metadata =
        """{"budgetTiers":[{"id":"a","label":"Dưới 20.000 USD","minUsd":0,"maxUsd":20000},{"id":"b","label":"20.000 – 35.000 USD","minUsd":20000,"maxUsd":35000},{"id":"c","label":"Trên 70.000 USD","minUsd":70000,"maxUsd":null}]}""";

    private static readonly DateTimeOffset AsOf = new(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);

    private sealed class Repo(
        string? fees = Fees, string? metadata = Metadata, IReadOnlyList<StateLivingCostDto>? states = null, bool broken = false) : ICostRepository
    {
        public Task<IReadOnlyList<SchoolCostRow>> GetSchoolCostsAsync(IReadOnlyList<string> codes, string studyLevel, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default) =>
            broken
                ? throw new InvalidOperationException("DB lỗi")
                : Task.FromResult(key == CostConstants.ReferenceFeesAppSettingKey ? fees : key == ProfileConstants.ProfileMetadataAppSettingKey ? metadata : null);

        public Task<BudgetInput?> GetProfileBudgetAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default) =>
            broken ? throw new InvalidOperationException("DB lỗi") : Task.FromResult(states ?? []);
    }

    private sealed class Rates(ExchangeRateQuote? quote = null, bool broken = false) : IExchangeRateProvider
    {
        public Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default) => Task.FromResult(26_000m);

        public Task<ExchangeRateQuote> GetQuoteAsync(CancellationToken ct = default) =>
            broken ? throw new HttpRequestException("mạng lỗi") : Task.FromResult(quote ?? new ExchangeRateQuote(26_340.5m, AsOf, "open.er-api.com", false));
    }

    private static StateLivingCostDto State(string code, decimal? index, short year = 2024) =>
        new(code, code, "West", index, null, index is null ? null : year, null, null);

    private static async Task<Dictionary<string, AlgorithmParameter>> Catalog(Repo? repo = null, Rates? rates = null) =>
        (await new CostParameterCatalog(repo ?? new Repo(states: [State("CA", 130m), State("TX", 95m), State("ZZ", null)]), rates ?? new Rates()).ListAsync())
        .ToDictionary(p => p.Key);

    [Fact]
    public async Task EveryParameterHasAName_AValue_AMeaning_AndTheCostStep()
    {
        var all = (await Catalog()).Values.ToList();

        Assert.Equal(10, all.Count);
        Assert.Equal(all.Count, all.Select(p => p.Key).Distinct().Count());
        Assert.All(all, p =>
        {
            Assert.StartsWith("cost_", p.Key);
            Assert.Equal(CostParameterCatalog.Step, p.Step);
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Value));
            Assert.False(string.IsNullOrWhiteSpace(p.Meaning));
        });
    }

    [Fact]
    public async Task Catalog_KeysDoNotCollideWithTheAlgorithmParameters()
    {
        var cost = (await Catalog()).Keys;

        Assert.Empty(cost.Intersect(AlgorithmParameters.All.Select(p => p.Key)));
    }

    [Fact]
    public async Task OneTimeFees_ShowTheStoredAmountsAndTheirSources()
    {
        var c = await Catalog();

        Assert.Equal("185 USD", c["cost_visa_fee"].Value.Replace(" ", " "));
        Assert.Contains("https://travel.state.gov/visa", c["cost_visa_fee"].Meaning);
        Assert.Equal("350 USD", c["cost_sevis_fee"].Value.Replace(" ", " "));
        Assert.Contains("https://www.ice.gov/sevis", c["cost_sevis_fee"].Meaning);
    }

    [Fact]
    public async Task MissingFees_AreSaidToBeMissing_NotZero()
    {
        var c = await Catalog(new Repo(fees: null));

        Assert.Equal(CostParameterCatalog.NoData, c["cost_visa_fee"].Value);
        Assert.Equal(CostParameterCatalog.NoData, c["cost_sevis_fee"].Value);
    }

    [Fact]
    public async Task ParametersTheEstimateDoesNotUseYet_AreMarkedNotInUse_NeverGivenANumber()
    {
        var c = await Catalog();

        foreach (var key in new[] { "cost_visa_integrity_fee", "cost_cost_growth", "cost_exchange_margin" })
            Assert.Equal(CostParameterCatalog.NotInUse, c[key].Value);
    }

    [Fact]
    public async Task BudgetTiers_ListTheUpperBoundsInOrder_AndTheTopTierHasNoCap_WithoutTheLabelsThousandsDots()
    {
        var value = (await Catalog())["cost_budget_tiers"].Value;

        Assert.Equal("20 000 USD · 35 000 USD · Trên 70 000 USD: không có trần", value);
        Assert.DoesNotContain(".", value);   // trang đổi "20.000" thành "20,000", nên không dùng dấu chấm hàng nghìn
    }

    [Fact]
    public async Task StudyYears_ComeFromTheCostCalculator()
    {
        var value = (await Catalog())["cost_study_years"].Value;

        Assert.Contains($"Đại học {CostCalculator.YearsFor(StudyLevels.Undergraduate)} năm", value);
        Assert.Contains($"Cao đẳng cộng đồng {CostCalculator.YearsFor(StudyLevels.CommunityCollege)} năm", value);
        Assert.Contains($"Thạc sĩ {CostCalculator.YearsFor(StudyLevels.Master)} năm", value);
    }

    [Fact]
    public async Task ExchangeRate_ShowsTheCurrentRateSourceAndDate_AndTheFallbackRateIsPublished()
    {
        var c = await Catalog();

        Assert.Equal("26 341 VNĐ/USD", c["cost_exchange_rate"].Value.Replace(" ", " "));
        Assert.Contains("open.er-api.com", c["cost_exchange_rate"].Meaning);
        Assert.Contains("24/11/2026", c["cost_exchange_rate"].Meaning);
        Assert.Equal("25 000 VNĐ/USD", c["cost_exchange_fallback"].Value.Replace(" ", " "));
        Assert.Equal(25_000m, CostConstants.FallbackVndPerUsd);
    }

    [Fact]
    public async Task ExchangeRate_WhenOnlyTheFallbackIsAvailable_SaysSo()
    {
        var rates = new Rates(new ExchangeRateQuote(CostConstants.FallbackVndPerUsd, null, "open.er-api.com", true));

        var meaning = (await Catalog(rates: rates))["cost_exchange_rate"].Meaning;

        Assert.Contains("dự phòng", meaning);
    }

    [Fact]
    public async Task StateIndex_CountsOnlyStatesWithData_AndTheLatestYear()
    {
        var value = (await Catalog())["cost_state_cost_index"].Value;

        Assert.Equal("2 bang · năm 2024", value);
    }

    [Fact]
    public async Task WhenEveryDataSourceFails_TheListStillComesBack_WithNoDataInsteadOfAnError()
    {
        var c = await Catalog(new Repo(broken: true), new Rates(broken: true));

        Assert.Equal(10, c.Count);
        Assert.Equal(CostParameterCatalog.NoData, c["cost_visa_fee"].Value);
        Assert.Equal(CostParameterCatalog.NoData, c["cost_budget_tiers"].Value);
        Assert.Equal(CostParameterCatalog.NoData, c["cost_exchange_rate"].Value);
        Assert.Equal(CostParameterCatalog.NoData, c["cost_state_cost_index"].Value);
        // Tham số không phụ thuộc dữ liệu vẫn hiện đúng
        Assert.Equal(CostParameterCatalog.NotInUse, c["cost_cost_growth"].Value);
        Assert.Contains("25", c["cost_exchange_fallback"].Value);
    }
}
