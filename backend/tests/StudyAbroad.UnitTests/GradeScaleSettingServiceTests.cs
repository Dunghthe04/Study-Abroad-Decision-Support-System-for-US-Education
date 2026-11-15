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

    // Bảng WES kiểu cũ (Method "table") mà DB đã seed trước khi đổi sang bảng nội suy
    private static AppSetting LegacyWes() => new()
    {
        Key = AcademicConstants.GradeScale10AppSettingKey,
        ValueJson = JsonSerializer.Serialize(new GradeScaleConfigDto(
            AcademicConstants.GradeScale10AppSettingKey, "WES", GradeScale.LegacyTableMethod,
            [
                new(9.0m, 10.0m, 4.0m, "A"), new(8.5m, 8.99m, 3.7m, "A-"), new(8.0m, 8.49m, 3.5m, "B+"),
                new(7.0m, 7.99m, 3.0m, "B"), new(6.5m, 6.99m, 2.5m, "C+"), new(5.5m, 6.49m, 2.0m, "C"),
                new(5.0m, 5.49m, 1.5m, "D+"), new(4.0m, 4.99m, 1.0m, "D"), new(0.0m, 3.99m, 0.0m, "F"),
            ])),
        CreatedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Get_SeededTable_ReturnsItWithVersion()
    {
        var (service, _, _) = Create(Seeded());

        var dto = await service.GetAsync();

        Assert.False(dto.IsDefault);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), dto.Version);
        Assert.Equal(6, dto.Config.Rules.Count); // 6 mốc THPT EducationUSA
    }

    // Nút "Dùng bảng mặc định" lấy mốc từ DefaultConfig, kể cả khi DB đang lưu bảng khác
    [Fact]
    public async Task Get_AlwaysSendsDefaultForTheResetButton()
    {
        var (service, _, _) = Create(LegacyWes());

        var dto = await service.GetAsync();

        Assert.Equal(9, dto.Config.Rules.Count);
        Assert.Equal(AcademicAnalysisService.GetDefaultScaleConfig().Rules, dto.DefaultConfig.Rules);
        Assert.Equal(GradeScale.LinearMethod, dto.DefaultConfig.Method);
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
        Assert.Equal(GradeScale.LinearMethod, saved.Method);
        Assert.False(repo.SavedAsNew);
        Assert.Equal(1, provider.InvalidateCount);

        var audit = Assert.Single(repo.Audits);
        Assert.Equal(GradeScaleSettingService.AuditAction, audit.Action);
        Assert.Equal(ModeratorId, audit.ActorUserId);
        using var detail = JsonDocument.Parse(audit.DetailJson!);
        Assert.Equal(6, detail.RootElement.GetProperty("old").GetProperty("rules").GetArrayLength());
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
        var rules = new List<GradeScaleRuleInput> { new(0m, 3m, "B"), new(8m, 2m, "ABCDE"), new(9m, 5m, "A+") };

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(rules, null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Invalid, result.Status);
        Assert.True(result.Errors.Count >= 3); // điểm chữ quá 4 ký tự, GPA giảm, GPA > 4
        Assert.Empty(repo.Audits);
    }

    // Bảng mod tự nhập có thể không có điểm chữ: bỏ trống vẫn lưu được
    [Fact]
    public async Task Update_NoLetterGrades_IsValid()
    {
        var seeded = Seeded();
        var (service, repo, _) = Create(seeded);
        var rules = new List<GradeScaleRuleInput> { new(0m, 0m, null), new(5m, 2m, ""), new(10m, 4m, "  ") };

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(rules, null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
        Assert.All(GradeScale.Parse(repo.Setting!.ValueJson)!.Rules, r => Assert.Equal("", r.LetterGrade));
    }

    [Fact]
    public async Task History_ReadsOldAndNewFromAudit()
    {
        var (service, repo, _) = Create(Seeded());
        await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, Seeded().CreatedAt), ModeratorId);

        var item = Assert.Single((await service.GetHistoryAsync(1, 10)).Items);

        Assert.Equal(ModeratorId, item.ActorUserId);
        Assert.Equal(6, item.Old!.Rules.Count);
        Assert.Equal(3, item.New!.Rules.Count);
    }

    // 12 lần sửa, mỗi trang 5: trang 3 còn 2 lần cũ nhất; trang 0 về trang 1; pageSize quá lớn bị chặn ở 50
    [Fact]
    public async Task History_PagesNewestFirst()
    {
        var (service, repo, _) = Create(Seeded());
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 12; i++)
            repo.Audits.Add(new AuditLog { Action = GradeScaleSettingService.AuditAction, CreatedAt = start.AddMinutes(i) });

        var last = await service.GetHistoryAsync(3, 5);
        Assert.Equal(12, last.TotalCount);
        Assert.Equal(3, last.TotalPages);
        Assert.Equal([start.AddMinutes(1), start], last.Items.Select(x => x.ChangedAt));

        var first = await service.GetHistoryAsync(0, 500);
        Assert.Equal(1, first.Page);
        Assert.Equal(GradeScaleSettingService.MaxHistoryPageSize, first.PageSize);
        Assert.Equal(start.AddMinutes(11), first.Items[0].ChangedAt);
    }

    // TB học bạ 8.22: bảng mặc định ra 3.573, DB lưu numeric(5,2) nên là 3.57; bảng mới (0 → 0, 5 → 2, 8 → 4) ra 4.0.
    // Hồ sơ GPA 3.57 khớp bảng cũ → GPA lấy từ học bạ → đổi thành 4.0; hồ sơ GPA 3.2 là tự nhập → giữ nguyên
    [Fact]
    public async Task Update_RecomputesOnlyProfilesWhoseGpaCameFromTranscript()
    {
        var seeded = Seeded();
        var (service, repo, _) = Create(seeded);
        var fromTranscript = new StudentProfile { OverallGpa = 3.57m, GradeScale = GradeScales.Scale10 };
        var typedByStudent = new StudentProfile { OverallGpa = 3.2m, GradeScale = GradeScales.Scale10 };
        repo.TranscriptProfiles.Add((fromTranscript, Scores822()));
        repo.TranscriptProfiles.Add((typedByStudent, Scores822()));

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(ValidRules(), null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
        Assert.Equal(1, result.RecomputedProfiles);
        Assert.Equal(4m, fromTranscript.OverallGpa);
        Assert.Equal(3.2m, typedByStudent.OverallGpa);
        using var detail = JsonDocument.Parse(Assert.Single(repo.Audits).DetailJson!);
        Assert.Equal(1, detail.RootElement.GetProperty("recomputedProfiles").GetInt32());
    }

    // DB còn bảng WES kiểu "table", mod bấm "Dùng bảng mặc định" rồi lưu. TB học bạ 8.22:
    // - hồ sơ tính trước khi đổi code (tra bậc thang WES) là 3.5
    // - hồ sơ tính sau khi đổi code nhưng trước khi mod lưu (nội suy trên mốc WES 8.0 → 3.5, 8.5 → 3.7) là 3.588 → 3.59
    // Cả hai đều từ học bạ → đổi thành 3.57 (bảng mặc định); hồ sơ tự nhập 3.2 giữ nguyên
    [Fact]
    public async Task Update_LegacyWesTableToDefault_RecomputesStepAndInterpolatedProfiles()
    {
        var legacy = LegacyWes();
        var (service, repo, _) = Create(legacy);
        var steppedBefore = new StudentProfile { OverallGpa = 3.5m, GradeScale = GradeScales.Scale10 };
        var interpolatedBefore = new StudentProfile { OverallGpa = 3.59m, GradeScale = GradeScales.Scale10 };
        var typedByStudent = new StudentProfile { OverallGpa = 3.2m, GradeScale = GradeScales.Scale10 };
        repo.TranscriptProfiles.Add((steppedBefore, Scores822()));
        repo.TranscriptProfiles.Add((interpolatedBefore, Scores822()));
        repo.TranscriptProfiles.Add((typedByStudent, Scores822()));
        var defaults = AcademicAnalysisService.GetDefaultScaleConfig().Rules
            .Select(r => new GradeScaleRuleInput(r.MinScore, r.Gpa4, r.LetterGrade)).ToList();

        var result = await service.UpdateAsync(new UpdateGradeScaleRequest(defaults, null, legacy.CreatedAt), ModeratorId);

        Assert.Equal(GradeScaleUpdateStatus.Ok, result.Status);
        Assert.Equal(2, result.RecomputedProfiles);
        Assert.Equal(3.57m, steppedBefore.OverallGpa);
        Assert.Equal(3.57m, interpolatedBefore.OverallGpa);
        Assert.Equal(3.2m, typedByStudent.OverallGpa);
    }

    [Fact]
    public async Task Update_InvalidTable_DoesNotTouchProfiles()
    {
        var seeded = Seeded();
        var (service, repo, _) = Create(seeded);
        var profile = new StudentProfile { OverallGpa = 3.5m, GradeScale = GradeScales.Scale10 };
        repo.TranscriptProfiles.Add((profile, Scores822()));

        await service.UpdateAsync(new UpdateGradeScaleRequest([new(0m, 5m, "A")], null, seeded.CreatedAt), ModeratorId);

        Assert.Equal(3.5m, profile.OverallGpa);
    }

    private static IReadOnlyList<TranscriptScore> Scores822() =>
    [
        new() { TermOrder = 1, Semester = 1, Subject = "Toán", Score = 8m },
        new() { TermOrder = 2, Semester = 2, Subject = "Toán", Score = 9m },
        new() { TermOrder = 1, Semester = 1, Subject = "Văn", Score = 7m },
        new() { TermOrder = 4, Semester = 2, Subject = "Toán", Score = 9m },
    ];

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
        public List<(StudentProfile, IReadOnlyList<TranscriptScore>)> TranscriptProfiles { get; } = [];

        public Task<AppSetting?> GetAsync(CancellationToken ct = default) => Task.FromResult(Setting);

        public Task<IReadOnlyList<(StudentProfile Profile, IReadOnlyList<TranscriptScore> Scores)>> GetTranscriptGpaProfilesAsync(
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<(StudentProfile, IReadOnlyList<TranscriptScore>)>>(TranscriptProfiles);

        public Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
        {
            SavedAsNew = isNew;
            Setting = setting;
            Audits.Add(audit);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<(AuditLog Log, string? ActorName)> Rows, int TotalCount)> GetHistoryAsync(
            int page, int pageSize, CancellationToken ct = default)
        {
            IReadOnlyList<(AuditLog, string?)> rows = Audits.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(a => (a, (string?)"Mod")).ToList();
            return Task.FromResult((rows, Audits.Count));
        }
    }

    private sealed class FakeProvider : IGradeScaleProvider
    {
        public int InvalidateCount { get; private set; }
        public Task<GradeScaleConfigDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(AcademicAnalysisService.GetDefaultScaleConfig());
        public void Invalidate() => InvalidateCount++;
    }
}
