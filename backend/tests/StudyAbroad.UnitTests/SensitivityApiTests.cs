using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Sensitivity;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Constants;
using static StudyAbroad.UnitTests.SensitivityRunServiceTests;

namespace StudyAbroad.UnitTests;

// [UC-85] Phần API: so sánh hai lần chạy, file CSV, danh mục tham số, và controller (chỉ Admin).
public class SensitivityApiTests
{
    private static readonly SensitivityProfileSet Profiles = SensitivityProfileSet.LoadV1();
    private static readonly Guid Admin = Guid.NewGuid();

    private static (SensitivityRunService Service, FakeRuns Runs, FakeAudits Audits) Setup()
    {
        var runs = new FakeRuns();
        var audits = new FakeAudits();
        return (new SensitivityRunService(runs, new FakeSchools(Schools, false), audits, Profiles), runs, audits);
    }

    // ------------------------------------------------------------ so sánh

    [Fact]
    public async Task Compare_WithoutAnEarlierRun_IsEmpty_NotAnError()
    {
        var (service, _, _) = Setup();
        var only = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));

        var comparison = await service.CompareAsync(only.Summary.Id, null);

        Assert.NotNull(comparison);
        Assert.False(comparison!.HasEarlierRun);
        Assert.Null(comparison.Other);
        Assert.Empty(comparison.Parameters);
    }

    [Fact]
    public async Task Compare_WithThePreviousRun_UnderTheSameConditions_HasNoWarnings_AndZeroDelta()
    {
        var (service, _, _) = Setup();
        var request = new SensitivityRequest(["upper_cut", "fit_quality"], [0.10]);
        var first = await service.RunAsync(Admin, request);
        var second = await service.RunAsync(Admin, request);

        var comparison = (await service.CompareAsync(second.Summary.Id, null))!;

        Assert.True(comparison.HasEarlierRun);
        Assert.Equal(first.Summary.Id, comparison.Base.Id);     // trước
        Assert.Equal(second.Summary.Id, comparison.Other!.Id);  // sau
        Assert.Empty(comparison.Warnings);
        Assert.Equal(["upper_cut", "fit_quality"], comparison.Parameters.Select(p => p.Key));
        Assert.All(comparison.Parameters, p =>
        {
            Assert.Equal(0, p.Delta);
            Assert.False(p.BaselineChanged);
            Assert.Equal([-0.10, 0.10], p.Steps.Select(s => s.Step));
        });
    }

    [Fact]
    public async Task Compare_CalledTheOtherWayRound_StillReadsOlderToNewer()
    {
        var (service, _, _) = Setup();
        var first = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));
        var second = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));

        var comparison = (await service.CompareAsync(first.Summary.Id, second.Summary.Id))!;

        Assert.Equal(first.Summary.Id, comparison.Base.Id);
        Assert.Equal(second.Summary.Id, comparison.Other!.Id);
    }

    [Fact]
    public async Task Compare_AnUnknownRun_IsNull_AndARunWithItself_IsRejected()
    {
        var (service, _, _) = Setup();
        var run = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));

        Assert.Null(await service.CompareAsync(Guid.NewGuid(), null));
        Assert.Null(await service.CompareAsync(run.Summary.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CompareAsync(run.Summary.Id, run.Summary.Id));
    }

    [Fact]
    public void Compare_WarnsWhenTheConditionsDiffer_AndKeepsParametersFromEitherRun()
    {
        var older = Detail(DateTime.UtcNow.AddHours(-1), "percentile-fit-1", "aaa", 0.02, 0.10, Result("upper_cut", 0.75, 0.03));
        var newer = Detail(DateTime.UtcNow, "percentile-fit-2", "bbb", 0.01, 0.10,
            Result("upper_cut", 0.70, 0.05), Result("lower_cut", 0.25, 0.01));

        var comparison = SensitivityComparer.Compare(newer, older);

        Assert.Equal(
            [SensitivityComparisonWarnings.AlgorithmVersionDiffers, SensitivityComparisonWarnings.SchoolDataDiffers,
             SensitivityComparisonWarnings.ThresholdsDiffer],
            comparison.Warnings);
        // Thứ tự theo danh mục: lower_cut đứng trước upper_cut
        Assert.Equal(["lower_cut", "upper_cut"], comparison.Parameters.Select(p => p.Key));

        var lower = comparison.Parameters[0];
        Assert.Null(lower.BaseRate);       // lần trước không chọn tham số này
        Assert.Equal(0.01, lower.OtherRate);
        Assert.Null(lower.Delta);

        var upper = comparison.Parameters[1];
        Assert.Equal(0.02, upper.Delta!.Value, 9);
        Assert.True(upper.BaselineChanged); // nhóm đã đổi 0,75 thành 0,70 giữa hai lần chạy
    }

    // ------------------------------------------------------------ CSV

    [Fact]
    public async Task Csv_HasOneRowPerStep_WithPlainNumbers_AndNoPersonalData()
    {
        var (service, runs, _) = Setup();
        var run = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut", "fit_quality"], [0.10, 0.20]));
        runs.Names[run.Summary.Id] = "Nguyễn Văn Admin";

        var bytes = SensitivityCsv.Build((await service.GetAsync(run.Summary.Id))!);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);    // BOM để Excel đọc đúng tiếng Việt
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(',', SensitivityCsv.Header), lines[0]);
        Assert.Equal(1 + 2 * 4, lines.Length);                     // 2 tham số × 4 mức nhích
        Assert.All(lines.Skip(1), l => Assert.Equal(SensitivityCsv.Header.Length, l.Split(',').Length));
        var text = string.Join('\n', lines);
        Assert.Contains(",upper_cut,Ngưỡng Vừa sức / An toàn,grouping,0.75,0.1,0.825,false,", text);   // không có đuôi 0.8250000000000001
        Assert.DoesNotContain("Nguyễn Văn Admin", text);
        Assert.DoesNotContain(Admin.ToString(), text);
    }

    [Fact]
    public void Csv_TextThatLooksLikeAFormula_IsDefused()
    {
        var run = Detail(DateTime.UtcNow, "=HYPERLINK(\"x\")", "aaa", 0.02, 0.10, Result("upper_cut", 0.75, 0.03));
        var text = Encoding.UTF8.GetString(SensitivityCsv.Build(run)[3..]);
        Assert.Contains(",\"'=HYPERLINK(\"\"x\"\")\",", text);
    }

    [Fact]
    public async Task ExportCsv_WritesTheAuditEntryFirst_AndAnUnknownRunGivesNothing()
    {
        var (service, _, audits) = Setup();
        var run = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));

        var file = await service.ExportCsvAsync(Admin, run.Summary.Id);
        var missing = await service.ExportCsvAsync(Admin, Guid.NewGuid());

        Assert.NotNull(file);
        Assert.EndsWith(".csv", file!.FileName);
        var entry = Assert.Single(audits.Rows);
        Assert.Equal(SensitivityRunService.ExportAction, entry.Action);
        Assert.Equal(run.Summary.Id.ToString(), entry.EntityId);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ExportCsv_WhenTheAuditFails_ReturnsNoFile()
    {
        var (service, _, audits) = Setup();
        var run = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));
        audits.Fail = new InvalidOperationException("DB lỗi");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportCsvAsync(Admin, run.Summary.Id));
    }

    // ------------------------------------------------------------ danh mục và JSON

    [Fact]
    public void Parameters_ListAll14_WithTheirRunningValues()
    {
        var (service, _, _) = Setup();
        var list = service.Parameters();
        Assert.Equal(14, list.Count);
        Assert.Equal(0.75, list.Single(p => p.Key == "upper_cut").Current);
        Assert.Equal(SensitivityKind.FitWeight, list.Single(p => p.Key == "fit_budget").Kind);
    }

    [Fact]
    public void ARequestBody_WithoutThresholds_UsesTheBaDefaults_AndKindIsWrittenAsText()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request = JsonSerializer.Deserialize<SensitivityRequest>("""{"parameterKeys":["upper_cut"],"steps":[0.1]}""", web)!;

        Assert.Equal(0.02, request.StableBelow);
        Assert.Equal(0.10, request.WatchUpTo);
        Assert.Equal("\"fit_weight\"", JsonSerializer.Serialize(SensitivityKind.FitWeight, web));
    }

    // ------------------------------------------------------------ controller

    [Fact]
    public void Controller_IsAdminOnly_AtTheSensitivityPath()
    {
        Assert.Equal(UserRoles.Admin, typeof(AdminSensitivityController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/v1/admin/sensitivity", typeof(AdminSensitivityController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task Controller_Run_Returns201_AndABadRequestIs400_WithNothingSaved()
    {
        var (service, runs, _) = Setup();
        var controller = ControllerFor(service, Admin);

        var created = await controller.Run(new SensitivityRequest(["upper_cut"], [0.10]), default);
        var bad = await controller.Run(new SensitivityRequest([], [0.10]), default);

        Assert.IsType<SensitivityRunDetail>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
        Assert.Single(runs.Saved);
    }

    [Fact]
    public async Task Controller_WithoutALoggedInUser_CannotRunOrExport()
    {
        var (service, runs, _) = Setup();
        var controller = ControllerFor(service, null);

        Assert.IsType<UnauthorizedResult>((await controller.Run(new SensitivityRequest(["upper_cut"], [0.10]), default)).Result);
        Assert.IsType<UnauthorizedResult>(await controller.ExportCsv(Guid.NewGuid(), default));
        Assert.Empty(runs.Saved);
    }

    [Fact]
    public async Task Controller_UnknownRun_Is404_AndCsvComesBackAsAFile()
    {
        var (service, _, _) = Setup();
        var controller = ControllerFor(service, Admin);
        var run = await service.RunAsync(Admin, new SensitivityRequest(["upper_cut"], [0.10]));

        Assert.IsType<NotFoundResult>((await controller.Get(Guid.NewGuid(), default)).Result);
        Assert.IsType<NotFoundResult>((await controller.Compare(Guid.NewGuid(), null, default)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Compare(run.Summary.Id, run.Summary.Id, default)).Result);
        var file = Assert.IsType<FileContentResult>(await controller.ExportCsv(run.Summary.Id, default));
        Assert.StartsWith("text/csv", file.ContentType);
    }

    // ------------------------------------------------------------ dữ liệu giả

    private static AdminSensitivityController ControllerFor(ISensitivityRunService service, Guid? userId)
    {
        var http = new DefaultHttpContext();
        if (userId is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        return new AdminSensitivityController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static SensitivityParameterResult Result(string key, double baseline, double rate) =>
        new(key, SensitivityParameters.Find(key)!.Name, SensitivityKind.Grouping, baseline,
            [new SensitivityPoint(0.10, baseline * 1.1, false, 1000, (int)(rate * 1000), 0, (int)(rate * 1000), 100, 0)],
            rate, SensitivityAnalyzer.Label(rate));

    private static SensitivityRunDetail Detail(
        DateTime at, string algorithm, string schoolHash, double stableBelow, double watchUpTo, params SensitivityParameterResult[] results)
    {
        var request = new SensitivityRequest(results.Select(r => r.Key).ToList(), [0.10], stableBelow, watchUpTo);
        var summary = new SensitivityRunListRow(Guid.NewGuid(), at, null, null, algorithm, "v1", Profiles.Hash, 324, 249, schoolHash,
            request.ParameterKeys, request.Steps, 0, 0, 0, 1);
        var report = new SensitivityReport(algorithm, "v1", Profiles.Hash, 324, 249, schoolHash, stableBelow, watchUpTo, results);
        return new SensitivityRunDetail(summary, request, new Dictionary<string, double>(), report);
    }
}
