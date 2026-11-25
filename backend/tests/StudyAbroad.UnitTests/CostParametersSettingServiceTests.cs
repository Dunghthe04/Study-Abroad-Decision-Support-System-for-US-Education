using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Costs;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class CostParametersSettingServiceTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private sealed class FakeRepo : ICostParametersSettingRepository
    {
        public AppSetting? Setting { get; set; }
        public List<AuditLog> Audits { get; } = [];

        public Task<AppSetting?> GetAsync(CancellationToken ct = default) => Task.FromResult(Setting);

        public Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
        {
            if (isNew) setting.CreatedAt = DateTime.UtcNow;
            setting.UpdatedAt = DateTime.UtcNow;
            Setting = setting;
            Audits.Add(audit);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetAsync_WhenAbsent_ReturnsDefaults()
    {
        var svc = new CostParametersSettingService(new FakeRepo());

        var dto = await svc.GetAsync();

        Assert.True(dto.IsDefault);
        Assert.Null(dto.Version);
        Assert.Equal(CostParameters.Default, dto.Values);
        Assert.Equal(250m, dto.Values.VisaIntegrityFeeUsd);
        Assert.Equal(0.04m, dto.Values.AnnualEscalationRate);
        Assert.Equal(0.015m, dto.Values.BankSellRateSpread);
        Assert.Equal(4, dto.Values.UndergraduateYears);
    }

    [Fact]
    public async Task UpdateAsync_ValidValues_PersistsSettingAndAudit()
    {
        var repo = new FakeRepo();
        var svc = new CostParametersSettingService(repo);

        var saved = await svc.UpdateAsync(new UpdateCostParametersRequest(300m, 0.05m, 0.02m, 5, null), ActorId);

        Assert.Equal(CostParametersUpdateStatus.Ok, saved.Status);
        Assert.False(saved.Value!.IsDefault);
        Assert.Equal(new CostParameters(300m, 0.05m, 0.02m, 5), saved.Value.Values);
        Assert.Equal(CostConstants.CostParametersAppSettingKey, repo.Setting!.Key);
        Assert.Single(repo.Audits);
        Assert.Equal(CostParametersSettingService.AuditAction, repo.Audits[0].Action);

        var live = await svc.GetCurrentParametersAsync();
        Assert.Equal(new CostParameters(300m, 0.05m, 0.02m, 5), live);
    }

    [Fact]
    public async Task UpdateAsync_InvalidValues_ReturnsInvalidWithoutSaving()
    {
        var repo = new FakeRepo();
        var svc = new CostParametersSettingService(repo);

        var result = await svc.UpdateAsync(new UpdateCostParametersRequest(-1m, 1.5m, -0.01m, 0, null), ActorId);

        Assert.Equal(CostParametersUpdateStatus.Invalid, result.Status);
        Assert.Equal(4, result.Errors.Count);
        Assert.Null(repo.Setting);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ReturnsConflict()
    {
        var repo = new FakeRepo
        {
            Setting = new AppSetting
            {
                Key = CostConstants.CostParametersAppSettingKey,
                ValueJson = """{"visaIntegrityFeeUsd":250,"annualEscalationRate":0.04,"bankSellRateSpread":0.015,"undergraduateYears":4}""",
                CreatedAt = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        var svc = new CostParametersSettingService(repo);

        var result = await svc.UpdateAsync(new UpdateCostParametersRequest(300m, 0.05m, 0.02m, 4, null), ActorId);

        Assert.Equal(CostParametersUpdateStatus.Conflict, result.Status);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void Controller_AdminAndModeratorRead_OnlyModeratorUpdatesCostParameters()
    {
        var classAttr = typeof(AdminSettingsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", classAttr!.Roles);
        Assert.Null(typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.GetCostParameters))!
            .GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(UserRoles.Moderator, typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.UpdateCostParameters))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
}
