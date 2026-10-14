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

    //AI giả: ghi lại request 3 lượt, trả kết quả soạn sẵn (hoặc ném lỗi)
    private sealed class FakeAi : IRecommendationAi
    {
        public AiEcRequest? EcRequest;
        public AiScoreRequest? ScoreRequest;
        public AiPickRequest? PickRequest;
        public bool EcFails, ScoreFails, PickFails;
        public int Tier = 4;
        public string[] Failed = [];

        public Task<AiEcResult> ReadExtracurricularAsync(AiEcRequest request, CancellationToken ct = default)
        {
            EcRequest = request;
            if (EcFails) throw new RecommendationAiException("advisor tắt");
            return Task.FromResult(new AiEcResult([], 1.2m, 0.15m, 0.34m, Tier, "1.1", "qwen3:8b"));
        }

        public Task<AiScoreResult> ScoreAsync(AiScoreRequest request, CancellationToken ct = default)
        {
            ScoreRequest = request;
            if (ScoreFails) throw new RecommendationAiException("502");
            // Trường A Vừa sức, B An toàn hơn, C (vượt ngân sách) Khó vào xa
            var items = request.Schools.Where(s => !Failed.Contains(s.Code)).Select(s => s.Code switch
            {
                "A" => Item("A", 50m, "M", "M"),
                "B" => Item("B", 80m, "S", "S"),
                _ => Item(s.Code, 10m, "R", "RF", gap: 30000m),
            }).ToList();
            return Task.FromResult(new AiScoreResult("1.1", "qwen3:8b", items, Failed));
        }

        public Task<AiPickResult> PickAsync(AiPickRequest request, CancellationToken ct = default)
        {
            PickRequest = request;
            if (PickFails) throw new RecommendationAiException("502");
            var codes = request.Items.Select(i => i.Code).ToHashSet();
            AiPickEntry[] Pick(string code, decimal? x, params string[] why) =>
                codes.Contains(code) ? [new AiPickEntry(code, x, why)] : [];
            return Task.FromResult(new AiPickResult(
                Reach: [], Match: Pick("A", 0.5m, "GPA_MID", "BUDGET_OK"), Safety: Pick("B", 0.4m, "GPA_HIGH", "BUDGET_OK"),
                Aid: Pick("C", null, "GPA_LOW", "NEED_AID", "INTL_LOW"), Other: [], SkillVersion: "1.1", Model: "qwen3:8b"));
        }

        private static AiScoreItem Item(string code, decimal s, string g, string g2, decimal? gap = null) =>
            new(code, [0.6m, 0.4m], s, "H", s, s, 0m, s, g, null, g2, 0.2m, 0.1m, 0.9m, gap);
    }

    private static StudentProfile Profile() => new()
    {
        UserId = UserId, TargetLevel = "undergraduate", OverallGpa = 3.6m, Sat = 1349.6m, Ielts = 7.0m,
        IntendedMajor = "Computer Science", TargetSchoolType = "all", BudgetMaxUsd = 50000m, ScholarshipNeed = "preferred",
    };

    private static SchoolCandidate School(string code, decimal tuition, string major = "Computer and information sciences") => new(
        Guid.NewGuid(), Guid.NewGuid(), code, $"School {code}", "NY", [major], 3.5m, 1300, 1450, tuition, 15000m, 0m,
        AcceptanceRate: 0.4m, MinIelts: 6.5m, Control: "private", HasIntlAid: true,
        C7GpaImportance: "very_important", C7TestImportance: "important", C7EcImportance: "important");

    private static (RecommendationService Service, FakeRepository Repo, FakeAi Ai) Make()
    {
        var repo = new FakeRepository
        {
            Profile = Profile(),
            // A, B trong ngân sách; C vượt ngân sách (khối học bổng); D khác ngành (bị lọc)
            Schools = [School("A", 30000m), School("B", 20000m), School("C", 70000m), School("D", 20000m, "History")],
            Activities = [new ProfileActivity { Kind = "extracurricular", Title = "CLB Robotics", Role = "leader", ImpactLevel = 1, DurationMonths = 24 },
                          new ProfileActivity { Kind = "award", Title = "Giải Nhì Tin học", ImpactLevel = 3 }],
        };
        var ai = new FakeAi();
        return (new RecommendationService(repo, ai), repo, ai);
    }

    [Fact]
    public async Task FullFlow_FiltersThenCallsThreePassesInOrder()
    {
        var (service, repo, ai) = Make();
        var result = await service.CreateAsync(UserId);

        // Lọc cứng: D khác ngành bị loại trước khi gọi AI; "Computer Science" đổi sang nhóm CIP
        Assert.Equal(["A", "B", "C"], ai.ScoreRequest!.Schools.Select(s => s.Code));
        // Lượt ① nhận hoạt động và giải thưởng tách riêng
        Assert.Equal("CLB Robotics. vai trò: leader", Assert.Single(ai.EcRequest!.Activities).Text);
        Assert.Equal(3, Assert.Single(ai.EcRequest.Awards).Level);
        // Lượt ② nhận bậc ngoại khóa từ lượt ①, SAT làm tròn
        Assert.Equal(new AiStudent(3.6m, 1350, 50000m, 4), ai.ScoreRequest.Student);
        // Lượt ③: C vượt ngân sách → khối AID; mặc định nút Cân bằng
        Assert.Equal("AID", ai.PickRequest!.Items.Single(i => i.Code == "C").Block);
        Assert.Equal([2, 2, 2], ai.PickRequest.Pref);
        Assert.Equal("preferred", ai.PickRequest.NeedAid);

        Assert.NotNull(result);
        Assert.Equal(["A", "B", "C"], result!.Items.Select(i => i.Code));                 // match → safety → aid
        Assert.Equal(["match", "safety", "aid"], result.Items.Select(i => i.Block));
        Assert.Equal([1, 2, 3], result.Items.Select(i => i.Rank));
        Assert.Empty(result.Warnings);
        Assert.NotNull(repo.Saved);
    }

    [Fact]
    public async Task ResultItem_CarriesGroupReasonsAndSchoolInfo()
    {
        var (service, _, _) = Make();
        var result = await service.CreateAsync(UserId);

        var c = result!.Items.Single(i => i.Code == "C");
        Assert.Equal(("reach", true), (c.Category, c.FarReach));      // RF = Khó vào xa
        Assert.Equal(30000m, c.OverBudgetUsd);
        Assert.Equal(["GPA_LOW", "NEED_AID", "INTL_LOW"], c.Why);
        Assert.Equal("GPA thấp hơn phần lớn sinh viên của trường. Cần học bổng lớn. Trường nhận ít du học sinh so với tỉ lệ chung.", c.Reason);
        Assert.Contains(FilterFlags.NeedBigAid, c.Flags!);            // cờ từ lọc cứng
        Assert.Equal("met", c.English);
        Assert.Equal(85000m, c.TotalCostUsd);
        Assert.Equal("NY", c.School!.State);
    }

    [Fact]
    public async Task SavedRecordKeepsVersionsForReplay()
    {
        var (service, repo, _) = Make();
        await service.CreateAsync(UserId);

        Assert.Equal("ai3-ec1.1-score1.1-pick1.1", repo.Saved!.AlgorithmVersion);
        Assert.Contains("\"tier\":4", repo.Saved.CriteriaJson);
        Assert.Contains("\"model\":\"qwen3:8b\"", repo.Saved.CriteriaJson);
        var latest = await service.GetLatestAsync(UserId);
        Assert.Equal(3, latest!.Items.Count);                        // đọc lại được từ items_json
    }

    [Fact]
    public async Task ExtracurricularFails_UsesAverageTierAndWarns()
    {
        var (service, _, ai) = Make();
        ai.EcFails = true;
        var result = await service.CreateAsync(UserId);

        Assert.Equal(3, ai.ScoreRequest!.Student.Tier);
        Assert.Contains(result!.Warnings, w => w.Contains("bậc 3"));
        Assert.Equal(3, result.Items.Count);                         // vẫn có danh sách
    }

    [Fact]
    public async Task ScoringFails_ReturnsEmptyWithWarningAndDoesNotSave()
    {
        var (service, repo, ai) = Make();
        ai.ScoreFails = true;
        var result = await service.CreateAsync(UserId);

        Assert.Empty(result!.Items);
        Assert.Contains(result.Warnings, w => w.Contains("thử lại"));
        Assert.Null(ai.PickRequest);
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task PickFails_FallsBackToCodeAndStillShowsList()
    {
        var (service, repo, ai) = Make();
        ai.PickFails = true;
        var result = await service.CreateAsync(UserId);

        // Code tự chia khối theo nhóm lượt ②: A Vừa sức, B An toàn hơn, C khối học bổng
        Assert.Equal(["A", "B", "C"], result!.Items.Select(i => i.Code));
        Assert.Equal(["match", "safety", "aid"], result.Items.Select(i => i.Block));
        Assert.Contains(result.Warnings, w => w.Contains("tự sắp theo công thức"));
        Assert.Contains("pickcode", repo.Saved!.AlgorithmVersion);
    }

    [Fact]
    public async Task SchoolsAiCouldNotScore_AreLeftOutWithWarning()
    {
        var (service, _, ai) = Make();
        ai.Failed = ["B"];
        var result = await service.CreateAsync(UserId);

        Assert.DoesNotContain(result!.Items, i => i.Code == "B");
        Assert.DoesNotContain(ai.PickRequest!.Items, i => i.Code == "B");
        Assert.Contains(result.Warnings, w => w.Contains("1 trường"));
    }

    [Fact]
    public async Task NoSchoolPassesFilter_ReturnsWarningWithoutCallingAi()
    {
        var (service, repo, ai) = Make();
        repo.Profile!.IntendedMajor = "Psychology";                   // không trường nào có ngành này
        var result = await service.CreateAsync(UserId);

        Assert.Empty(result!.Items);
        Assert.Null(ai.EcRequest);
        Assert.Contains(result.Warnings, w => w.Contains("nới điều kiện"));
    }

    [Fact]
    public async Task UnknownMajorName_IsNotFilteredAndWarns()
    {
        var (service, repo, ai) = Make();
        repo.Profile!.IntendedMajor = "Underwater Basket Weaving";
        var result = await service.CreateAsync(UserId);

        Assert.Equal(4, ai.ScoreRequest!.Schools.Count);               // không lọc ngành: cả D cũng vào
        Assert.Contains(result!.Warnings, w => w.Contains("Underwater Basket Weaving"));
    }

    [Fact]
    public async Task NonUndergraduate_IsNotScoredYet()
    {
        var (service, repo, ai) = Make();
        repo.Profile!.TargetLevel = "master";
        var result = await service.CreateAsync(UserId);
        Assert.Empty(result!.Items);
        Assert.Null(ai.ScoreRequest);
    }

    [Fact]
    public async Task NoProfile_ReturnsNull()
    {
        var (service, repo, _) = Make();
        repo.Profile = null;
        Assert.Null(await service.CreateAsync(UserId));
    }
}
