using System.Text.Json;
using StudyAbroad.Application.Admin.Sensitivity;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [UC-85] Lưu lần phân tích: chỉ lưu khi chạy xong, lưu kèm nhật ký, đọc lại ra đúng kết quả đã tính.
public class SensitivityRunServiceTests
{
    private static readonly SensitivityProfileSet Profiles = SensitivityProfileSet.LoadV1();
    private static readonly Guid Admin = Guid.NewGuid();

    private static readonly IReadOnlyList<RecommendSchoolInput> Schools = Enumerable.Range(0, 12).Select(i =>
    {
        var university = new University
        {
            Code = $"S{i:00}", Name = $"S{i:00}", State = "NY", Control = "private", IsActive = true,
            AcceptanceRate = Math.Round(0.90m - i * 0.06m, 2)
        };
        var offering = new UniversityOffering
        {
            UniversityId = university.Id, StudyLevel = "undergraduate", TuitionUsd = 20_000m + i * 5_000m,
            IntlAidPolicy = "need_and_merit", Sat25 = 1000 + i * 40, Sat75 = 1160 + i * 40, AvgGpa4 = 3.0m + i * 0.07m
        };
        return new RecommendSchoolInput(university, offering, [Profiles.Major], [new RankPosition(i + 1, 50)]);
    }).ToList();

    private static readonly SensitivityRequest Request = new(["upper_cut", "fit_budget"], [0.10, 0.20]);

    private static (SensitivityRunService Service, FakeRuns Runs) Setup(IReadOnlyList<RecommendSchoolInput>? schools = null, bool failSchools = false)
    {
        var runs = new FakeRuns();
        return (new SensitivityRunService(runs, new FakeSchools(schools ?? Schools, failSchools), Profiles), runs);
    }

    [Fact]
    public async Task Run_SavesOneRun_WithItsConditions_AndOneAuditEntry_InTheSameWrite()
    {
        var (service, runs) = Setup();
        var detail = await service.RunAsync(Admin, Request);

        var (run, audit) = Assert.Single(runs.Saved);
        Assert.Equal(detail.Summary.Id, run.Id);
        Assert.Equal(Admin, run.CreatedByUserId);
        Assert.Equal(RecommendationService.AlgorithmVersion, run.AlgorithmVersion);
        Assert.Equal("v1", run.ProfileSetVersion);
        Assert.Equal(85, run.ProfileSetSeed);
        Assert.Equal(324, run.ProfileCount);
        Assert.Equal(Profiles.Hash, run.ProfileSetHash);
        Assert.Equal(12, run.SchoolCount);
        Assert.Equal(RecommendationService.SchoolDataFingerprint(Schools), run.SchoolDataHash);
        Assert.Equal(2, run.StableCount + run.WatchCount + run.SensitiveCount);

        // Nhật ký: ai, làm gì, trên dòng nào; chỉ mã tham số và số đếm
        Assert.Equal(Admin, audit.ActorUserId);
        Assert.Equal(SensitivityRunService.RunAction, audit.Action);
        Assert.Equal(SensitivityRunService.EntityType, audit.EntityType);
        Assert.Equal(run.Id.ToString(), audit.EntityId);
        using var detailJson = JsonDocument.Parse(audit.DetailJson!);
        Assert.Equal(["upper_cut", "fit_budget"], detailJson.RootElement.GetProperty("parameterKeys").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(324, Profiles.Profiles.Count);
    }

    [Fact]
    public async Task Run_StoresTheValueOfEveryParameter_AtTheTimeOfTheRun()
    {
        var (service, runs) = Setup();
        var detail = await service.RunAsync(Admin, Request);

        Assert.Equal(SensitivityParameters.All.Count, detail.Parameters.Count);
        Assert.Equal(0.75, detail.Parameters["upper_cut"]);
        Assert.Equal(0.25, detail.Parameters["fit_budget"]);
        Assert.Contains("\"upper_cut\":0.75", runs.Saved[0].Run.ParametersJson);
    }

    [Fact]
    public async Task ARunReadBack_IsExactlyWhatWasComputed()
    {
        var (service, runs) = Setup();
        var computed = await service.RunAsync(Admin, Request);
        runs.Names[computed.Summary.Id] = "Admin A";

        var read = await service.GetAsync(computed.Summary.Id);

        Assert.NotNull(read);
        Assert.Equal("Admin A", read!.Summary.CreatedByName);
        Assert.Equal(computed.Request.ParameterKeys, read.Request.ParameterKeys);
        Assert.Equal(computed.Request.Steps, read.Request.Steps);
        Assert.Equal(computed.Parameters, read.Parameters);
        Assert.Equal(computed.Report.SchoolDataHash, read.Report.SchoolDataHash);
        Assert.Equal(computed.Report.Parameters.Count, read.Report.Parameters.Count);
        for (var i = 0; i < computed.Report.Parameters.Count; i++)
        {
            var a = computed.Report.Parameters[i];
            var b = read.Report.Parameters[i];
            Assert.Equal((a.Key, a.Kind, a.Baseline, a.Rate, a.Label), (b.Key, b.Kind, b.Baseline, b.Rate, b.Label));
            Assert.Equal(a.Points, b.Points);
        }
    }

    [Fact]
    public async Task Get_AnUnknownRun_IsNull()
    {
        var (service, _) = Setup();
        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_ShowsTheChosenParametersAndLabelCounts_NewestFirst()
    {
        var (service, runs) = Setup();
        await service.RunAsync(Admin, new SensitivityRequest(["lower_cut"], [0.10]));
        await service.RunAsync(Admin, Request);

        var list = await service.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal(["upper_cut", "fit_budget"], list[0].ParameterKeys);
        Assert.Equal(["lower_cut"], list[1].ParameterKeys);
        Assert.Equal(1, list[1].StableCount + list[1].WatchCount + list[1].SensitiveCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10_000, SensitivityRunService.MaxListLimit)]
    public async Task List_KeepsTheLimitInRange(int asked, int used)
    {
        var (service, runs) = Setup();
        await service.ListAsync(asked);
        Assert.Equal(used, runs.LastLimit);
    }

    // ------------------------------------------------------------ AF-1, AF-3: lỗi thì không lưu gì

    [Fact]
    public async Task ARequestWithoutParameters_IsRejected_AndNothingIsSaved()
    {
        var (service, runs) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(Admin, new SensitivityRequest([], [0.10])));
        Assert.Empty(runs.Saved);
    }

    [Fact]
    public async Task AFailureWhileLoadingSchools_SavesNothing()
    {
        var (service, runs) = Setup(failSchools: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(Admin, Request));
        Assert.Empty(runs.Saved);
    }

    [Fact]
    public async Task ACancelledRun_SavesNothing()
    {
        var (service, runs) = Setup();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(Admin, Request, cancelled.Token));
        Assert.Empty(runs.Saved);
    }

    // ------------------------------------------------------------ giả lập

    private sealed class FakeRuns : ISensitivityRunRepository
    {
        public List<(SensitivityRun Run, AuditLog Audit)> Saved { get; } = [];
        public Dictionary<Guid, string> Names { get; } = [];
        public int LastLimit { get; private set; }

        public Task AddAsync(SensitivityRun run, AuditLog audit, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Thời điểm tạo tăng dần để "mới nhất trước" xác định được
            run.CreatedAt = DateTime.UtcNow.AddSeconds(Saved.Count);
            Saved.Add((run, audit));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SensitivityRunRecord>> ListAsync(int limit, CancellationToken ct = default)
        {
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<SensitivityRunRecord>>(Saved
                .Select(s => s.Run).OrderByDescending(r => r.CreatedAt).Take(limit)
                .Select(r => new SensitivityRunRecord(r, Names.GetValueOrDefault(r.Id))).ToList());
        }

        public Task<SensitivityRunRecord?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Saved.Select(s => s.Run).FirstOrDefault(r => r.Id == id) is { } r
                ? new SensitivityRunRecord(r, Names.GetValueOrDefault(r.Id))
                : null);
    }

    private sealed class FakeSchools(IReadOnlyList<RecommendSchoolInput> schools, bool fail) : IRecommendationRepository
    {
        public Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return fail ? throw new InvalidOperationException("DB lỗi") : Task.FromResult(schools);
        }

        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<SchoolRanking>> ListSchoolRankingsAsync(Guid universityId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
