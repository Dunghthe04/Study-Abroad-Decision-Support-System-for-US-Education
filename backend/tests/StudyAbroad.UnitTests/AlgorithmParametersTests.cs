using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Recommendations;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-89: danh mục tham số chỉ đọc, lấy từ code đang chạy
public class AlgorithmParametersTests
{
    [Fact]
    public void Catalog_HasUniqueKeysAndNoEmptyFields()
    {
        var all = AlgorithmParameters.All;
        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Select(p => p.Key).Distinct().Count());
        foreach (var p in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Key));
            Assert.False(string.IsNullOrWhiteSpace(p.Step));
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Value));
            Assert.False(string.IsNullOrWhiteSpace(p.Meaning));
        }
    }

    [Fact]
    public void Version_IsTheRunningAlgorithmVersion() =>
        Assert.Equal(RecommendationService.AlgorithmVersion, AlgorithmParameters.Version);

    // Giá trị phải đúng với hằng số đang chạy: đổi hằng số mà danh mục không đổi theo là lỗi
    [Theory]
    [InlineData("lower_cut", "0.25")]
    [InlineData("upper_cut", "0.75")]
    [InlineData("always_reach_rate", "15%")]
    [InlineData("safety_min_rate", "50%")]
    [InlineData("max_per_block", "4")]
    [InlineData("budget_tolerance", "10%")]
    [InlineData("gpa_sigma", "0.3")]
    [InlineData("sat_min_p75", "850")]
    [InlineData("goal_margin", "0.05")]
    public void Catalog_ShowsTheRunningValue(string key, string expected) =>
        Assert.Equal(expected, AlgorithmParameters.All.Single(p => p.Key == key).Value);

    [Fact]
    public void Catalog_FitWeightsMatchTheDefaults()
    {
        var w = new FitWeights();
        Assert.Equal(1.0, w.Position + w.Budget + w.Aid + w.Quality + w.English, 6);   // năm trọng số cộng lại bằng 1
        var value = AlgorithmParameters.All.Single(p => p.Key == "fit_weights").Value;
        Assert.Contains("Vừa tầm 0.3", value);
        Assert.Contains("Tiếng Anh 0.1", value);
    }

    [Fact]
    public void Catalog_FitWeightsAreListedInTheOrderOfTheSpec()
    {
        // UC-24: vừa tầm, ngân sách, chất lượng, học bổng, tiếng Anh (0,30; 0,25; 0,20; 0,15; 0,10)
        var value = AlgorithmParameters.All.Single(p => p.Key == "fit_weights").Value;

        Assert.Equal(
            "Vừa tầm 0.3 · Ngân sách 0.25 · Chất lượng 0.2 · Học bổng 0.15 · Tiếng Anh 0.1",
            value);
    }

    private sealed class StubCostParametersService(CostParameters current) : ICostParametersSettingService
    {
        public Task<CostParametersSettingDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new CostParametersSettingDto(current, null, true, CostParameters.Default));
        public Task<CostParameters> GetCurrentParametersAsync(CancellationToken ct = default) =>
            Task.FromResult(current);
        public Task<CostParametersUpdateResult> UpdateAsync(UpdateCostParametersRequest request, Guid actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Controller_ReturnsVersionAndLiveParameters()
    {
        var live = new CostParameters(300m, 0.05m, 0.02m, 5);
        var controller = new MethodologyController(new StubCostParametersService(live))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var response = (await controller.GetParameters()).Value;

        Assert.NotNull(response);
        Assert.Equal(AlgorithmParameters.Version, response.AlgorithmVersion);
        Assert.Equal(AlgorithmParameters.All.Count, response.Parameters.Count);
        Assert.Equal("$300", response.Parameters.Single(p => p.Key == "cost_visa_integrity_fee").Value);
        Assert.Equal("5%", response.Parameters.Single(p => p.Key == "cost_annual_escalation").Value);
        Assert.Equal("2%", response.Parameters.Single(p => p.Key == "cost_bank_sell_spread").Value);
        Assert.Equal("5 năm", response.Parameters.Single(p => p.Key == "cost_undergrad_years").Value);
    }

    [Fact]
    public void Controller_IsReadOnlyAndPublic()
    {
        var methods = typeof(MethodologyController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.All(methods, m => Assert.NotNull(m.GetCustomAttribute<HttpGetAttribute>()));   // không có thao tác ghi nào
        Assert.NotNull(typeof(MethodologyController).GetMethod(nameof(MethodologyController.GetParameters))!
            .GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
