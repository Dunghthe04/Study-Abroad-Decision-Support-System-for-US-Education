using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// P2: moderator lưu bảng quy đổi: kiểm tra luật, chặn ghi đè (409), ghi audit_logs, xoá cache
public class GradeScaleSettingServiceTests
{
    private static readonly Guid ModeratorId = Guid.NewGuid();

    private static List<GradeScaleRuleInput> ValidRules() =>
    [
        new(8m, 4m, "A"),   // gửi lộn thứ tự: server tự sắp
        new(0m, 0m, "F"),
        new(5m, 2m, "C"),
    ];

    private static (GradeScaleSettingService Service, FakeRepo Repo, FakeProvider Provider) Create(AppSetting? existing = null)
    {
        var repo = new FakeRepo { Setting = existing };
        var provider = new FakeProvider();
        return (new GradeScaleSettingService(repo, provider), repo, provider);
    }

    private static AppSetting Seeded() => new()
    {
        Key = AcademicConstants.GradeScale10AppSettingKey,
        ValueJson = GradeScale.DefaultJson(),
        CreatedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Get_SeededTable_ReturnsItWithVersion()
    {
        var (service, _, _) = Create(Seeded());

        var dto = await service.GetAsync();

        Assert.False(dto.IsDefault);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), dto.Version);
        Assert.Equal(9, dto.Config.Rules.Count);
    }

    [Fact]
    public async Task Get_NoRow_ReturnsDefaultFlagged()
    {
        var (service, _, _) = Create();

        var dto = await service.GetAsync();

        Assert.True(dto.IsDefault);
        Assert.Null(dto.Version);
    }

    [Fact]
    public async Task Update_Valid_SavesSortedWithMaxScore_AuditsAndInvalidatesCache()
    {
        var seeded = Seeded();
        var (service, repo, provider) = Create(seeded);

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), "Bảng thử", seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
        var saved = GradeScale.Parse(repo.Setting!.ValueJson)!;
        Assert.Equal(
            [new GradeScaleRuleDto(0m, 4.99m, 0m, "F"), new GradeScaleRuleDto(5m, 7.99m, 2m, "C"), new GradeScaleRuleDto(8m, 10m, 4m, "A")],
            saved.Rules);
        Assert.Equal("Bảng thử", saved.SourceName);
        Assert.False(repo.SavedAsNew);
        Assert.Equal(1, provider.InvalidateCount);

        var audit = Assert.Single(repo.Audits);
        Assert.Equal(GradeScaleSettingService.AuditAction, audit.Action);
        Assert.Equal(ModeratorId, audit.ActorUserId);
        using var detail = JsonDocument.Parse(audit.DetailJson!);
        Assert.Equal(9, detail.RootElement.GetProperty("old").GetProperty("rules").GetArrayLength());
        Assert.Equal(3, detail.RootElement.GetProperty("new").GetProperty("rules").GetArrayLength());
    }

    [Fact]
    public async Task Update_NoRowYet_CreatesIt()
    {
        var (service, repo, _) = Create();

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, null), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
        Assert.True(repo.SavedAsNew);
        Assert.Equal(AcademicConstants.GradeScale10AppSettingKey, repo.Setting!.Key);
    }

    [Fact]
    public async Task Update_StaleVersion_ConflictsAndSavesNothing()
    {
        var seeded = Seeded();
        seeded.UpdatedAt = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc); // người khác vừa sửa
        var (service, repo, provider) = Create(seeded);

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Conflict, result.Status);
        Assert.Equal(seeded.UpdatedAt, result.Value!.Version);
        Assert.Empty(repo.Audits);
        Assert.Equal(0, provider.InvalidateCount);
    }

    [Fact]
    public async Task Update_VersionDiffersBelowMicrosecond_IsNotConflict()
    {
        var seeded = Seeded();
        seeded.UpdatedAt = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var (service, _, _) = Create(seeded);
        var sent = seeded.UpdatedAt.Value.AddTicks(-7); // Postgres làm tròn xuống micro giây

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, sent), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
    }

    [Fact]
    public async Task Update_InvalidTable_ReturnsErrorsAndSavesNothing()
    {
        var seeded = Seeded();
        var (service, repo, _) = Create(seeded);
        var rules = new List<GradeScaleRuleInput> { new(0m, 3m, "B"), new(8m, 2m, ""), new(9m, 5m, "A+") };

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(rules, null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Invalid, result.Status);
        Assert.True(result.Errors.Count >= 3); // điểm chữ rỗng, GPA giảm, GPA > 4
        Assert.Empty(repo.Audits);
    }

    [Fact]
    public async Task History_ReadsOldAndNewFromAudit()
    {
        var (service, repo, _) = Create(Seeded());
        await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, Seeded().CreatedAt), ModeratorId);

        var item = Assert.Single(await service.GetHistoryAsync(20));

        Assert.Equal(ModeratorId, item.ActorUserId);
        Assert.Equal(9, item.Old!.Rules.Count);
        Assert.Equal(3, item.New!.Rules.Count);
    }

    [Fact]
    public void Controller_AdminAndModeratorRead_OnlyModeratorWrites()
    {
        var classAttr = typeof(AdminSettingsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", classAttr!.Roles);

        var putAttr = typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.UpdateGradeScale))!
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(UserRoles.Moderator, putAttr!.Roles);

        Assert.Null(typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.GetGradeScale))!
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    private sealed class FakeRepo : IGradeScaleSettingRepository
    {
        public AppSetting? Setting { get; set; }
        public bool SavedAsNew { get; private set; }
        public List<AuditLog> Audits { get; } = [];

        public Task<AppSetting?> GetAsync(CancellationToken ct = default) => Task.FromResult(Setting);

        public Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
        {
            SavedAsNew = isNew;
            Setting = setting;
            Audits.Add(audit);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<(AuditLog Log, string? ActorName)>> GetHistoryAsync(int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<(AuditLog, string?)>>(
                Audits.OrderByDescending(a => a.CreatedAt).Take(limit).Select(a => (a, (string?)"Mod")).ToList());
    }

    private sealed class FakeProvider : IGradeScaleProvider
    {
        public int InvalidateCount { get; private set; }
        public Task<GradeScaleConfigDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(AcademicAnalysisService.GetDefaultWesScaleConfig());
        public void Invalidate() => InvalidateCount++;
    }
}
