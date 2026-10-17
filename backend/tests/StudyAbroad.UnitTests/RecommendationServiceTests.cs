using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class RecommendationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FakeRepository : IRecommendationRepository
    {
        public StudentProfile? Profile;
        public List<SchoolCandidate> Schools = [];
        public List<ProfileActivity> Activities = [];
        public Recommendation? Saved;

        public Task<StudentProfile?> GetProfileAsync(Guid studentId, CancellationToken ct = default) => Task.FromResult(Profile);
        public Task<IReadOnlyList<ProfileActivity>> GetActivitiesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProfileActivity>>(Activities);
        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<SchoolCandidate>> GetSchoolCandidatesAsync(string level, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchoolCandidate>>(Schools);
        public Task AddAsync(Recommendation r, CancellationToken ct = default) { Saved = r; return Task.CompletedTask; }
        public Task<Recommendation?> GetLatestRecommendationAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Saved);
    }

    //Advisor giả: đọc hoạt động ngoại khóa → bậc, và viết tư vấn từng trường
    private sealed class FakeAi : IRecommendationAi
    {
        public AiEcRequest? EcRequest;
        public AiExplainRequest? ExplainRequest;

        public Task<AiExplainResult> ExplainAsync(AiExplainRequest request, CancellationToken ct = default)
        {
            ExplainRequest = request;
            return Task.FromResult(new AiExplainResult("Tóm tắt tư vấn đủ dài.", ["Điểm mạnh"], ["Nên làm"], "1.1", "qwen3:8b"));
        }

        public int Tier = 4;

        public Task<AiEcResult> ReadExtracurricularAsync(AiEcRequest request, CancellationToken ct = default)
        {
            EcRequest = request;
            return Task.FromResult(new AiEcResult([], 1.2m, 0.15m, 0.34m, Tier, "2.0", "qwen3:8b"));
        }
    }

    private static StudentProfile Profile() => new()
    {
        UserId = UserId, TargetLevel = "undergraduate", OverallGpa = 3.6m, Sat = 1349.6m, Ielts = 7.0m,
        IntendedMajor = "Computer Science", TargetSchoolType = "all", BudgetMaxUsd = 50000m, ScholarshipNeed = "preferred",
    };

    private static SchoolCandidate School(string code, decimal tuition, decimal avgGpa, int sat25, int sat75,
        decimal? intlRate = null, string major = "Computer and information sciences") => new(
        Guid.NewGuid(), Guid.NewGuid(), code, $"School {code}", "NY", [major], avgGpa, sat25, sat75, tuition, 15000m, 0m,
        AcceptanceRate: 0.4m, MinIelts: 6.5m, Control: "private", HasIntlAid: true,
        C7GpaImportance: "very_important", C7TestImportance: "important", C7EcImportance: "important",
        IntlAcceptanceRate: intlRate, IntlAidAvgUsd: 20000m);

    private static (RecommendationService Service, FakeRepository Repo, FakeAi Ai) Make()
    {
        var repo = new FakeRepository
        {
            Profile = Profile(),
            Schools =
            [
                School("A", 30000m, 3.5m, 1300, 1450),                      // ngang sức → Vừa sức
                School("B", 20000m, 3.0m, 1000, 1150),                      // dễ → An toàn hơn
                School("C", 70000m, 3.9m, 1500, 1560, intlRate: 0.05m),     // khó, nhận ít du học sinh, vượt ngân sách → khối học bổng
                School("D", 20000m, 3.5m, 1300, 1450, major: "History"),    // khác ngành → bị lọc
            ],
            Activities = [new ProfileActivity { Kind = "extracurricular", Title = "CLB Robotics", Role = "leader", ImpactLevel = 1, DurationMonths = 24 },
                          new ProfileActivity { Kind = "award", Title = "Giải Nhì Tin học", ImpactLevel = 3 }],
        };
        var ai = new FakeAi();
        return (new RecommendationService(repo, ai), repo, ai);
    }

    [Fact]
    public async Task NoSchoolPassesFilter_ReturnsWarningWithoutCallingAdvisor()
    {
        var (service, repo, ai) = Make();
        repo.Profile!.IntendedMajor = "Psychology";
        var result = await service.CreateAsync(UserId);

        Assert.Empty(result!.Items);
        Assert.Null(ai.EcRequest);
        Assert.Contains(result.Warnings, w => w.Contains("nới điều kiện"));
    }

    [Fact]
    public async Task AllSchoolsOverBudget_WarnsAndShowsAidBlock()
    {
        var (service, repo, _) = Make();
        repo.Profile!.BudgetMaxUsd = 20000m;                                  // thấp hơn chi phí mọi trường
        var result = await service.CreateAsync(UserId);

        Assert.All(result!.Items, i => Assert.Equal("aid", i.Block));
        Assert.Contains(result.Warnings, w => w.Contains("thấp hơn chi phí của mọi trường"));
    }

    [Fact]
    public async Task NonUndergraduate_IsNotScoredYet()
    {
        var (service, repo, ai) = Make();
        repo.Profile!.TargetLevel = "master";
        var result = await service.CreateAsync(UserId);
        Assert.Empty(result!.Items);
        Assert.Null(ai.EcRequest);
    }

    [Fact]
    public async Task ExplainSchool_UnknownSchoolOrNoResult_ReturnsNull()
    {
        var (service, _, _) = Make();
        Assert.Null(await service.ExplainSchoolAsync(UserId, Guid.NewGuid()));   // chưa gợi ý lần nào
        await service.CreateAsync(UserId);
        Assert.Null(await service.ExplainSchoolAsync(UserId, Guid.NewGuid()));   // trường không có trong kết quả
    }

    [Fact]
    public async Task NoProfile_ReturnsNull()
    {
        var (service, repo, _) = Make();
        repo.Profile = null;
        Assert.Null(await service.CreateAsync(UserId));
    }
}
