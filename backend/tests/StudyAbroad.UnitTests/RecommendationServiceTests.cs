using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Luồng gợi ý trường: lọc cứng → advisor đọc ngoại khóa → code chấm nhóm và fit score → lưu theo khuôn JSON cũ.
public class RecommendationServiceTests
{
    private const string Major = "Computer and information sciences";

    private static StudentProfile Profile(string? need = ProfileConstants.ScholarshipNeeds.Preferred, string? major = Major) => new()
    {
        UserId = Guid.NewGuid(),
        TargetLevel = "undergraduate",
        IntendedMajor = major,
        BudgetMaxUsd = 50_000m,
        ScholarshipNeed = need,
        OverallGpa = 3.6m,
        Sat = 1350m,
        Ielts = 6.5m,
        TargetSchoolType = "all"
    };

    private static (RecommendationService Service, FakeAdvisor Advisor, FakeRecommendationRepository Store) Setup(
        StudentProfile? profile = null, IReadOnlyList<RecommendSchoolInput>? schools = null,
        IReadOnlyList<ProfileActivity>? activities = null)
    {
        var advisor = new FakeAdvisor();
        var store = new FakeRecommendationRepository(schools ?? []);
        return (new RecommendationService(advisor, store, new FakeProfileFlow(profile, activities)), advisor, store);
    }

    // Bốn hoạt động sáng lập cấp quốc tế 24 tháng: điểm gộp tối đa nên bậc 5
    private static List<ProfileActivity> StrongActivities() => Enumerable.Range(0, 4)
        .Select(i => new ProfileActivity
        {
            Kind = "extracurricular", Title = $"Hoạt động {i}", Role = "founder", ImpactLevel = 5, DurationMonths = 24
        })
        .ToList();

    // ---------------------------------------------------------------- luồng chính

    [Fact]
    public async Task Recommend_FiltersFirst_GradesByCode_AndNothingGoesToTheAdvisor()
    {
        var (service, advisor, store) = Setup();
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("Colgate", 95_610m, "need_and_merit", Major, sat: (1450, 1550), accept: 0.08m),   // vượt ngân sách, rất chọn lọc
            School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m),     // vừa sức
            School("D", 20_000m, "none", "History", sat: (1200, 1380), accept: 0.60m)                 // sai ngành
        };

        await service.RecommendAsync(profile, [], schools);

        Assert.Empty(advisor.Calls);   // gợi ý trường chạy hoàn toàn bằng code, không gọi advisor

        var saved = Assert.Single(store.Rows);
        Assert.Equal(RecommendationService.AlgorithmVersion, saved.AlgorithmVersion);
        using var criteria = JsonDocument.Parse(saved.CriteriaJson);
        using var items = JsonDocument.Parse(saved.ItemsJson);
        var root = items.RootElement;

        var scored = root.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.False(scored.ContainsKey("D"));
        Assert.Equal("AidBlock", scored["Colgate"].GetProperty("outcome").GetString());
        Assert.Equal("R", scored["Colgate"].GetProperty("g2").GetString());     // tỉ lệ nhận 8% nên luôn Khó vào
        Assert.Equal("M", scored["B"].GetProperty("g2").GetString());

        Assert.Equal("B", root.GetProperty("match")[0].GetProperty("code").GetString());
        Assert.Equal(0, root.GetProperty("reach").GetArrayLength());           // Colgate cần học bổng lớn nên ở khối riêng
        Assert.Equal("Colgate", root.GetProperty("aid")[0].GetProperty("code").GetString());
        var why = root.GetProperty("match")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString()).ToList();
        Assert.Contains("GPA_MID", why);
        Assert.Contains("BUDGET_OK", why);
        Assert.Contains("NEED_AID", root.GetProperty("aid")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString()));

        Assert.Equal(1, criteria.RootElement.GetProperty("tier").GetInt32());   // chưa khai hoạt động nào
        Assert.Equal("OK", criteria.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Recommend_SavedJsonKeepsTheShapeTheFrontendReads()
    {
        var (service, _, store) = Setup();
        await service.RecommendAsync(Profile(), [], [School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];

        foreach (var key in new[] { "universityId", "code", "name", "state", "control", "cost", "outcome", "flags",
                     "g2", "s", "f", "h", "q", "gap", "pg", "ps", "adj", "r" })
            Assert.True(row.TryGetProperty(key, out _), $"thiếu khóa {key}");
        foreach (var key in new[] { "reach", "match", "safety", "aid", "other" })
            Assert.Equal(JsonValueKind.Array, items.RootElement.GetProperty(key).ValueKind);
        var x = items.RootElement.GetProperty("match")[0].GetProperty("x").GetDouble();
        Assert.InRange(x, 0.0, 1.0);
        Assert.InRange(row.GetProperty("pg").GetDouble(), 0.0, 100.0);   // thang 0–100 như bản cũ
    }

    [Fact]
    public async Task Recommend_SameInput_GivesTheSameJson()
    {
        var schools = new List<RecommendSchoolInput>
        {
            School("A", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
            School("B", 40_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
        };
        var (service1, _, store1) = Setup();
        var (service2, _, store2) = Setup();

        await service1.RecommendAsync(Profile(), [], schools);
        await service2.RecommendAsync(Profile(), [], schools.AsEnumerable().Reverse().ToList());

        Assert.Equal(store1.Rows.Single().ItemsJson, store2.Rows.Single().ItemsJson);
    }

    [Fact]
    public async Task Recommend_AtMostFourSchoolsPerBlock()
    {
        var (service, _, store) = Setup();
        var schools = Enumerable.Range(1, 9)
            .Select(i => School($"S{i}", 30_000m + i * 100, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m))
            .ToList();

        await service.RecommendAsync(Profile(), [], schools);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(4, items.RootElement.GetProperty("match").GetArrayLength());
        Assert.Equal(9, items.RootElement.GetProperty("scored").GetArrayLength());   // danh sách đầy đủ vẫn được lưu
    }

    // ---------------------------------------------------------------- công thức đi qua service

    [Fact]
    public async Task Recommend_SchoolWithoutAcademicData_GoesToTheOtherBlock()
    {
        var (service, _, store) = Setup();

        await service.RecommendAsync(Profile(), [], [School("Bare", 30_000m, "none", Major)]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal("NA", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal("Bare", items.RootElement.GetProperty("other")[0].GetProperty("code").GetString());
        Assert.Equal(0, items.RootElement.GetProperty("match").GetArrayLength());
    }

    [Fact]
    public async Task Recommend_AcceptanceRateIsTakenFromAdmittedOverApplicants_WhenTheColumnIsMissing()
    {
        var (service, _, store) = Setup();
        // 100 / 1.000 = 10% dưới 15%: luôn Khó vào dù điểm cao
        var school = School("Picky", 30_000m, "none", Major, sat: (1000, 1100), avgGpa: 3.0m, admitted: 100, applicants: 1_000);

        await service.RecommendAsync(Profile(), [], [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal("R", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal("Picky", items.RootElement.GetProperty("reach")[0].GetProperty("code").GetString());
        Assert.Equal(0.1, items.RootElement.GetProperty("scored")[0].GetProperty("acceptRate").GetDouble());
        // nhóm do tỷ lệ nhận quyết định thì phải nói lý do này, không chỉ các lý do về điểm
        var why = items.RootElement.GetProperty("reach")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString());
        Assert.Equal("LOW_RATE", why.First());
    }

    [Fact]
    public async Task Recommend_English_BelowTheMinimum_IsFlaggedNotExcluded()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 5.5m;
        var school = School("Strict", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 7.0m);

        await service.RecommendAsync(profile, [], [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];
        Assert.Contains("ENGLISH_BELOW_MIN", row.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(0.0, row.GetProperty("e").GetDouble());   // tiếng Anh chỉ làm giảm fit score
    }

    [Fact]
    public async Task Recommend_QualityComesFromTheRankingPosition()
    {
        var (service, _, store) = Setup();
        var ranked = School("Top", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (10, 100));
        var unranked = School("None", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);

        await service.RecommendAsync(Profile(), [], [ranked, unranked]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var rows = items.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.Equal(0.91, rows["Top"].GetProperty("q").GetDouble());               // 1 − (10 − 1) / 100
        Assert.Equal(JsonValueKind.Null, rows["None"].GetProperty("q").ValueKind);   // chưa có hạng: bỏ thành phần Q
    }

    [Fact]
    public async Task Recommend_WithNothingLeftAfterTheFilter_SavesAnEmptyRunWithoutCallingTheAdvisor()
    {
        var (service, advisor, store) = Setup();

        await service.RecommendAsync(Profile(), [], [School("D", 20_000m, "none", "History", sat: (1200, 1380))]);

        Assert.Empty(advisor.Calls);
        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(JsonValueKind.Null, criteria.RootElement.GetProperty("status").ValueKind);
        Assert.Equal(0, items.RootElement.GetProperty("scored").GetArrayLength());
    }

    [Fact]
    public async Task Recommend_TheEcTierComesFromTheActivitiesInTheProfile()
    {
        var (service, _, store) = Setup();
        var school = School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);

        await service.RecommendAsync(Profile(), StrongActivities(), [school]);
        await service.RecommendAsync(Profile(), [], [school]);

        using var strong = JsonDocument.Parse(store.Rows[0].CriteriaJson);
        using var none = JsonDocument.Parse(store.Rows[1].CriteriaJson);
        Assert.Equal(5, strong.RootElement.GetProperty("tier").GetInt32());
        Assert.Equal(1, none.RootElement.GetProperty("tier").GetInt32());
    }

    [Fact]
    public async Task Recommend_ActivitiesChangeTheGroupOnlyAtSchoolsThatCareAboutThem()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.OverallGpa = null;
        profile.Sat = 985m;   // p ≈ 0,22
        var cares = School("Cares", 38_000m, "none", Major, sat: (1000, 1200), accept: 0.6m, ec: "very_important");
        var ignores = School("Ignores", 38_000m, "none", Major, sat: (1000, 1200), accept: 0.6m, ec: "not_considered");

        await service.RecommendAsync(profile, StrongActivities(), [cares, ignores]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var groups = items.RootElement.GetProperty("scored").EnumerateArray()
            .ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("g2").GetString());
        Assert.Equal("M", groups["Cares"]);     // ngoại khóa bậc 5 đẩy từ Khó vào lên Vừa sức
        Assert.Equal("R", groups["Ignores"]);   // trường không xét ngoại khóa thì giữ nguyên
    }

    // ---------------------------------------------------------------- điều kiện hồ sơ

    // C7: học bổng trống thì không gợi ý trường, không gọi advisor, không lưu lượt nào
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Recommend_WithoutScholarshipNeed_IsBlockedBeforeAnyCall(string? need)
    {
        var (service, advisor, store) = Setup();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecommendAsync(Profile(need), [], [School("B", 38_000m, "none", Major)]));

        Assert.Equal(RecommendationService.MissingScholarshipMessage, ex.Message);
        Assert.StartsWith("Hồ sơ", ex.Message); // controller trả 400, không phải 502
        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Recommend_WithoutMajor_IsBlockedBeforeAnyCall(string? major)
    {
        var (service, advisor, store) = Setup();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecommendAsync(Profile(major: major), [], [School("B", 38_000m, "none", Major)]));

        Assert.Equal(RecommendationService.MissingMajorMessage, ex.Message);
        Assert.StartsWith("Hồ sơ", ex.Message);
        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Recommend_WithOptionalScholarship_StillRuns()
    {
        var (service, advisor, _) = Setup();

        await service.RecommendAsync(Profile(ProfileConstants.ScholarshipNeeds.Optional), [], [School("B", 38_000m, "none", Major)]);

        Assert.Empty(advisor.Calls); // "không cần" là lựa chọn thật, vẫn chạy
    }

    // ---------------------------------------------------------------- đổi nút ưu tiên

    [Fact]
    public async Task Repick_RecomputesWithNewPriorities_WithoutCallingTheAdvisorAgain()
    {
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("Cheap", 20_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (166, 332)),
            School("Famous", 49_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (17, 332)),
        };
        var (service, advisor, store) = Setup(profile, schools);

        await service.RecommendAsync(profile, [], schools, [4, 2, 0]);
        var costFirst = MatchOrder(store.Rows.Last());
        var again = await service.RepickAsync(profile.UserId, [0, 2, 4], default);
        var qualityFirst = MatchOrder(again);

        Assert.Empty(advisor.Calls);   // đổi ưu tiên không gọi advisor
        Assert.Equal(2, store.Rows.Count);
        Assert.Equal(["Cheap", "Famous"], costFirst);
        Assert.Equal(["Famous", "Cheap"], qualityFirst);
        using var criteria = JsonDocument.Parse(again.CriteriaJson);
        Assert.Equal(0, criteria.RootElement.GetProperty("pref")[0].GetInt32());
        Assert.Equal(1, criteria.RootElement.GetProperty("tier").GetInt32());   // bậc tính từ hoạt động trong hồ sơ (chưa có)
    }

    [Fact]
    public async Task Repick_DoesNotMoveASchoolBetweenGroups()
    {
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("A", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
            School("B", 40_000m, "none", Major, sat: (1450, 1550), accept: 0.08m),
        };
        var (service, _, store) = Setup(profile, schools);

        await service.RecommendAsync(profile, [], schools);
        var again = await service.RepickAsync(profile.UserId, [4, 0, 0], default);

        using var a = JsonDocument.Parse(store.Rows[0].ItemsJson);
        using var b = JsonDocument.Parse(again.ItemsJson);
        var before = a.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("g2").GetString());
        var after = b.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("g2").GetString());
        Assert.Equal(before, after);   // ưu tiên chỉ đổi thứ tự, không đổi nhóm
    }

    [Fact]
    public async Task Repick_WithoutAPreviousRun_ThrowsBeforeDoingAnything()
    {
        var (service, advisor, store) = Setup(Profile());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RepickAsync(Guid.NewGuid(), [2, 2, 2], default));

        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    private static List<string> MatchOrder(Recommendation recommendation)
    {
        using var items = JsonDocument.Parse(recommendation.ItemsJson);
        return items.RootElement.GetProperty("match").EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToList();
    }

    // ---------------------------------------------------------------- trình mô phỏng hồ sơ

    private static readonly SimulationOverrides NoChange = new(null, null, null, null, null, null, null);

    // Hồ sơ thật: GPA 3,5, SAT 1290 (đúng giữa trường S nên p = 0,5, Vừa sức). Trường S: SAT 1200–1380, GPA trung bình 3,5, nhận 60%.
    private static (RecommendationService Service, FakeAdvisor Advisor, FakeRecommendationRepository Store, StudentProfile Profile) SimSetup(
        IReadOnlyList<RecommendSchoolInput>? schools = null, IReadOnlyList<ProfileActivity>? activities = null)
    {
        var profile = Profile();
        profile.OverallGpa = 3.5m;
        profile.Sat = 1290m;
        var list = schools ?? [School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)];
        var (service, advisor, store) = Setup(profile, list, activities);
        return (service, advisor, store, profile);
    }

    [Fact]
    public async Task Simulate_ReturnsTheRealProfileValuesForTheSliders()
    {
        var (service, _, _, profile) = SimSetup(activities: StrongActivities());
        profile.Act = 28m;

        // đổi giá trị giả định không làm đổi "hồ sơ thật" trong kết quả
        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 2.0m }, null, default);

        Assert.Equal(new SimulationProfile(3.5m, 1290, 28, 6.5m, 50_000m, ProfileConstants.ScholarshipNeeds.Preferred, 5), result.Profile);
    }

    [Fact]
    public async Task Simulate_WithNoOverrides_ChangesNothing()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        Assert.Empty(result.Changes);
        Assert.Equal(result.Baseline, result.Simulated);
    }

    [Fact]
    public async Task Simulate_LoweringGpaAndSat_MovesTheSchoolFromMatchToReach()
    {
        var (service, _, _, profile) = SimSetup();

        // GPA 2,0 (p_g gần 0) và SAT 1100 (p_s 0,08): p dưới 0,25
        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 2.0m, Sat = 1100m }, null, default);

        Assert.Equal(new SchoolChange("S", "S", "M", "R"), Assert.Single(result.Changes));
    }

    [Fact]
    public async Task Simulate_RaisingGpaAndSat_MovesTheSchoolUp()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 4.0m, Sat = 1500m }, null, default);

        var change = Assert.Single(result.Changes);
        Assert.Equal("M", change.From);
        Assert.Equal("S", change.To);
    }

    [Fact]
    public async Task Simulate_ReturnsTheGroupCountsBeforeAndAfter()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 2.0m, Sat = 1100m }, null, default);

        Assert.Equal(1, result.Baseline.Match);
        Assert.Equal(0, result.Baseline.Reach);
        Assert.Equal(1, result.Simulated.Reach);
        Assert.Equal(0, result.Simulated.Match);
        Assert.Equal(1, result.Simulated.Kept);
    }

    [Fact]
    public async Task Simulate_LoweringTheBudget_DropsSchoolsOverIt()
    {
        var (service, _, _, profile) = SimSetup();

        // chi phí 38.000 không còn nằm trong ngân sách 20.000 (trần 22.000) và học sinh không cần học bổng
        var result = await service.SimulateAsync(
            profile.UserId, NoChange with { BudgetUsd = 20_000m, ScholarshipNeed = ProfileConstants.ScholarshipNeeds.Optional }, null, default);

        Assert.Equal(new SchoolChange("S", "S", "M", "OUT"), Assert.Single(result.Changes));
        Assert.Equal(0, result.Simulated.Kept);
    }

    [Fact]
    public async Task Simulate_LoweringTheBudget_WithAScholarshipNeed_MovesTheSchoolToTheAidBlock()
    {
        var school = School("S", 38_000m, "need_and_merit", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var (service, _, _, profile) = SimSetup([school]);

        var result = await service.SimulateAsync(
            profile.UserId, NoChange with { BudgetUsd = 20_000m, ScholarshipNeed = ProfileConstants.ScholarshipNeeds.Mandatory }, null, default);

        Assert.Equal(new SchoolChange("S", "S", "M", "AID"), Assert.Single(result.Changes));
        Assert.Equal(1, result.Simulated.AidBlock);
    }

    [Fact]
    public async Task Simulate_TheEcTierOverride_CanPushABorderlineStudentAcrossTheLine()
    {
        // SAT 985 ở trường 1000–1200: p ≈ 0,22, dưới 0,25; trường rất coi trọng ngoại khóa
        var school = School("S", 38_000m, "none", Major, sat: (1000, 1200), accept: 0.60m, ec: "very_important");
        var profile = Profile();
        profile.OverallGpa = null;
        profile.Sat = 985m;
        var (service, _, _) = Setup(profile, [school]);

        var result = await service.SimulateAsync(profile.UserId, NoChange with { EcTier = 5 }, null, default);

        var change = Assert.Single(result.Changes);
        Assert.Equal("R", change.From);
        Assert.Equal("M", change.To);
    }

    [Fact]
    public async Task Simulate_DoesNotSaveAnythingAndDoesNotCallTheAdvisor()
    {
        var (service, advisor, store, profile) = SimSetup();

        await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 3.9m, Sat = 1500m }, null, default);

        Assert.Empty(store.Rows);
        Assert.Empty(advisor.Calls);
    }

    [Fact]
    public async Task Simulate_UsesTheEcTierComputedFromTheSavedActivities()
    {
        var (service, _, store, profile) = SimSetup(activities: StrongActivities());

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        using var criteria = JsonDocument.Parse(result.CriteriaJson);
        Assert.Equal(5, criteria.RootElement.GetProperty("tier").GetInt32());
        Assert.Empty(store.Rows);   // mô phỏng không lưu gì
    }

    [Fact]
    public async Task Simulate_WithoutAnyActivity_UsesTierOne()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        using var criteria = JsonDocument.Parse(result.CriteriaJson);
        Assert.Equal(1, criteria.RootElement.GetProperty("tier").GetInt32());
    }

    [Fact]
    public async Task Simulate_ReturnsTheSameJsonShapeAsASavedRun()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 3.8m }, null, default);

        using var items = JsonDocument.Parse(result.ItemsJson);
        foreach (var key in new[] { "scored", "failed", "reach", "match", "safety", "aid", "other" })
            Assert.True(items.RootElement.TryGetProperty(key, out _), $"thiếu khóa {key}");
        using var criteria = JsonDocument.Parse(result.CriteriaJson);
        Assert.Equal("OK", criteria.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(5.0, null, null, null, null)]      // GPA vượt 4
    [InlineData(null, 300.0, null, null, null)]    // SAT dưới 400
    [InlineData(null, null, 40.0, null, null)]     // ACT vượt 36
    [InlineData(null, null, null, 10.0, null)]     // IELTS vượt 9
    [InlineData(null, null, null, null, -1.0)]     // ngân sách âm
    public async Task Simulate_RejectsValuesOutOfRange(double? gpa, double? sat, double? act, double? ielts, double? budget)
    {
        var (service, _, _, profile) = SimSetup();
        var overrides = new SimulationOverrides((decimal?)gpa, (decimal?)sat, (decimal?)act, (decimal?)ielts, (decimal?)budget, null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SimulateAsync(profile.UserId, overrides, null, default));
    }

    [Theory]
    [InlineData("abc", null)]
    [InlineData(null, 9)]
    [InlineData(null, 0)]
    public async Task Simulate_RejectsAnInvalidScholarshipNeedOrEcTier(string? need, int? tier)
    {
        var (service, _, _, profile) = SimSetup();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SimulateAsync(profile.UserId, NoChange with { ScholarshipNeed = need, EcTier = tier }, null, default));
    }

    [Fact]
    public async Task Simulate_WithTheSamePriorities_GivesTheSameBlocksAsTheSavedRun()
    {
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("A", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
            School("B", 40_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
        };
        var (service, _, store) = Setup(profile, schools);
        await service.RecommendAsync(profile, [], schools, [3, 1, 1]);

        var result = await service.SimulateAsync(profile.UserId, NoChange, [3, 1, 1], default);

        using var saved = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        using var simulated = JsonDocument.Parse(result.ItemsJson);
        Assert.Equal(saved.RootElement.GetRawText(), simulated.RootElement.GetRawText());
    }

    // ---------------------------------------------------------------- dữ liệu giả

    private static RecommendSchoolInput School(
        string code, decimal tuition, string aidPolicy, string major,
        (int Low, int High)? sat = null, decimal? accept = null, decimal? avgGpa = null,
        decimal? minIelts = null, (int Rank, int Total)? rank = null, int? admitted = null, int? applicants = null,
        string? ec = null)
    {
        var university = new University
        {
            Code = code, Name = code, State = "NY", Control = "private", IsActive = true, AcceptanceRate = accept
        };
        var offering = new UniversityOffering
        {
            UniversityId = university.Id,
            StudyLevel = "undergraduate",
            TuitionUsd = tuition,
            IntlAidPolicy = aidPolicy,
            Sat25 = sat?.Low,
            Sat75 = sat?.High,
            AvgGpa4 = avgGpa,
            MinIelts = minIelts,
            AdmittedTotal = admitted,
            ApplicantsTotal = applicants,
            C7EcImportance = ec
        };
        return new RecommendSchoolInput(university, offering, [major], rank is { } r ? [new RankPosition(r.Rank, r.Total)] : null);
    }

    // ---------------------------------------------------------------- goal-seeking

    // Hồ sơ yếu: GPA 3,0, SAT 1000, ngoại khóa bậc 1 (chưa khai hoạt động), ngân sách 50.000 (trần 55.000 sau dung sai 10%).
    // Trường S: SAT 1200–1380, GPA trung bình 3,5, nhận 60%, coi trọng ngoại khóa: với hồ sơ này là Khó vào.
    private static (RecommendationService Service, StudentProfile Profile) GoalSetup(params RecommendSchoolInput[] schools)
    {
        var profile = Profile();
        profile.OverallGpa = 3.0m;
        profile.Sat = 1000m;
        var list = schools.Length > 0
            ? schools
            : [School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m, ec: "very_important")];
        var (service, _, _) = Setup(profile, list);
        return (service, profile);
    }

    // Trường mà hồ sơ yếu ở giữa khoảng điểm (SAT 900–1100, GPA 3,0): Vừa sức.
    private static RecommendSchoolInput MatchSchool(string code, decimal tuition, string control = "private")
    {
        var school = School(code, tuition, "none", Major, sat: (900, 1100), accept: 0.60m, avgGpa: 3.0m);
        school.University.Control = control;
        return school;
    }

    [Fact]
    public async Task GoalSeek_WithoutATarget_AimsOneGroupUp()
    {
        var (service, profile) = GoalSetup();

        var result = await service.GoalSeekAsync(profile.UserId, "S", null, default);

        Assert.Equal(("R", "M", "reachable"), (result.Current, result.Target, result.Outcome));
        Assert.NotNull(result.Cheapest);
        Assert.Contains(result.Alone, p => p.Changes.Single().Factor == "sat");
        Assert.Equal("ok", result.Budget.Status);   // 38.000 trong ngân sách
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public async Task GoalSeek_AnExplicitTargetWins()
    {
        var (service, profile) = GoalSetup();

        var result = await service.GoalSeekAsync(profile.UserId, "S", "SAFETY", default);

        Assert.Equal("S", result.Target);
    }

    [Fact]
    public async Task GoalSeek_RecommendedIsAHigherBarThanTheMinimum_AndStillReachesTheTarget()
    {
        var (service, profile) = GoalSetup();

        var result = await service.GoalSeekAsync(profile.UserId, "S", "match", default);

        Assert.NotNull(result.Recommended);
        Assert.True(result.Recommended!.Effort >= result.Cheapest!.Effort);
        // Áp mức nên đặt mục tiêu vào hồ sơ: vẫn là Vừa sức dù ngưỡng nhóm đã nâng thêm khoảng đệm
        var school = new SchoolAcademics(3.5m, null, 1200, 1380, null, null, null, null, null, null, "very_important", 0.60m, null);
        var student = new StudentAcademics(3.0m, 1000, null, 1);
        foreach (var change in result.Recommended.Changes)
        {
            student = change.Factor switch
            {
                "sat" => student with { Sat = (int)change.To },
                "gpa" => student with { Gpa4 = change.To },
                _ => student with { EcTier = (int)change.To },
            };
        }
        var d = new GroupingOptions();
        var padded = d with { LowerCut = d.LowerCut + RecommendationService.GoalMargin, UpperCut = d.UpperCut + RecommendationService.GoalMargin };
        Assert.True(SchoolGrouper.Grade(student, school, padded).Group >= SchoolGroup.Match);
    }

    [Fact]
    public async Task GoalSeek_AStudentAlreadyInTheTargetGroup_NeedsNothing()
    {
        var (service, profile) = GoalSetup();
        profile.OverallGpa = 3.5m;
        profile.Sat = 1290m;

        var result = await service.GoalSeekAsync(profile.UserId, "S", "match", default);

        Assert.Equal("already_reached", result.Outcome);
        Assert.Null(result.Cheapest);
        Assert.Empty(result.Alone);
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public async Task GoalSeek_ASchoolThatCannotBeReached_SuggestsSimilarSchoolsAlreadyInTheTargetGroup()
    {
        var hard = School("T", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.10m, avgGpa: 3.5m);
        var (service, profile) = GoalSetup(
            hard,
            MatchSchool("M2", 20_000m, "public"),
            MatchSchool("M1", 40_000m),
            MatchSchool("M3", 60_000m));   // vượt ngân sách và không có học bổng: không phải lựa chọn

        var result = await service.GoalSeekAsync(profile.UserId, "T", "match", default);

        Assert.Equal(("blocked", "R"), (result.Outcome, result.Best));
        Assert.Contains(GroupReasons.LowAcceptRate, result.Reasons);
        Assert.Null(result.Cheapest);
        // Cùng loại (tư thục) và chi phí gần nhất trước; trường quá ngân sách không được gợi ý
        Assert.Equal(["M1", "M2"], result.Alternatives.Select(a => a.Code));
        Assert.All(result.Alternatives, a => Assert.Equal("M", a.Group));
    }

    [Fact]
    public async Task GoalSeek_ASchoolWithoutData_IsNoData_AndAlsoSuggestsAlternatives()
    {
        var empty = School("E", 38_000m, "none", Major, accept: 0.60m);
        var (service, profile) = GoalSetup(empty, MatchSchool("M1", 40_000m));

        var result = await service.GoalSeekAsync(profile.UserId, "E", "match", default);

        Assert.Equal(("no_data", "NA"), (result.Outcome, result.Current));
        Assert.Equal(["M1"], result.Alternatives.Select(a => a.Code));
    }

    // ---- tài chính

    [Fact]
    public async Task GoalSeek_ASchoolAboveTheBudgetButWithAid_NeedsAid_AndSaysTheMinimumBudget()
    {
        var pricey = School("P", 62_000m, "preferred", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var (service, profile) = GoalSetup(pricey);

        var budget = (await service.GoalSeekAsync(profile.UserId, "P", "match", default)).Budget;

        Assert.Equal("need_aid", budget.Status);
        Assert.Equal((62_000m, 50_000m), (budget.CostUsd, budget.BudgetUsd));
        Assert.Equal(56_400m, budget.MinBudgetUsd);   // 62.000 / 1,1 = 56.364, làm tròn lên trăm
        Assert.Equal(7_000m, budget.ShortfallUsd);    // 62.000 - 55.000
    }

    [Fact]
    public async Task GoalSeek_ASchoolAboveTheBudgetWithoutAid_IsOverBudget()
    {
        var pricey = School("P", 62_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var (service, profile) = GoalSetup(pricey);

        var result = await service.GoalSeekAsync(profile.UserId, "P", "match", default);

        Assert.Equal("over_budget", result.Budget.Status);
        Assert.Null(result.FilterReason);   // ngân sách là lý do riêng, không nằm ở FilterReason
    }

    [Fact]
    public async Task GoalSeek_WithoutACostOrABudget_SaysSo()
    {
        var noCost = School("N", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        noCost.Offering.TuitionUsd = null;
        var (service, profile) = GoalSetup(noCost);
        Assert.Equal("unknown_cost", (await service.GoalSeekAsync(profile.UserId, "N", "match", default)).Budget.Status);

        var (service2, profile2) = GoalSetup();
        profile2.BudgetMaxUsd = null;
        var result = await service2.GoalSeekAsync(profile2.UserId, "S", "match", default);
        Assert.Equal("no_budget", result.Budget.Status);
        Assert.Equal(34_600m, result.Budget.MinBudgetUsd);   // 38.000 / 1,1 = 34.545, làm tròn lên trăm
    }

    [Fact]
    public async Task GoalSeek_ASchoolOutsideTheStudentsFilters_SaysWhy()
    {
        var other = School("O", 38_000m, "none", "Music", sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var (service, profile) = GoalSetup(other);

        var result = await service.GoalSeekAsync(profile.UserId, "O", "match", default);

        Assert.Equal("MAJOR", result.FilterReason);
        Assert.Equal("ok", result.Budget.Status);
    }

    // ---- đầu vào

    [Fact]
    public async Task GoalSeek_RejectsAnUnknownSchool_AndABadTarget_AndAnIncompleteProfile()
    {
        var (service, profile) = GoalSetup();

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GoalSeekAsync(profile.UserId, "ZZZ", null, default));
        Assert.StartsWith("Chưa có trường", unknown.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GoalSeekAsync(profile.UserId, "S", "easy", default));

        profile.ScholarshipNeed = null;
        var incomplete = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GoalSeekAsync(profile.UserId, "S", null, default));
        Assert.StartsWith("Hồ sơ", incomplete.Message);
    }

    [Fact]
    public async Task ListSchoolOptions_ReturnsEverySchoolSortedByName_ForThePicker()
    {
        var z = School("1", 40_000m, "none", Major);
        z.University.Name = "Zeta College";
        var a = School("2", 40_000m, "none", Major);
        a.University.Name = "Alpha University";
        var (service, profile) = GoalSetup(z, a);

        var options = await service.ListSchoolOptionsAsync(profile.UserId, default);

        Assert.Equal(["Alpha University", "Zeta College"], options.Select(o => o.Name));
        Assert.Equal(("2", "NY"), (options[0].Code, options[0].State));
    }

    [Fact]
    public async Task FindSchools_MatchesTheTypedNameAgainstTheStudentsSchoolList()
    {
        var bu = School("1", 40_000m, "none", Major);
        bu.University.Name = "Boston University";
        var baylor = School("2", 40_000m, "none", Major);
        baylor.University.Name = "Baylor University";
        var (service, profile) = GoalSetup(bu, baylor);

        var exact = await service.FindSchoolsAsync(profile.UserId, "boston university", default);
        var acronym = await service.FindSchoolsAsync(profile.UserId, "BU", default);
        var none = await service.FindSchoolsAsync(profile.UserId, "Yale", default);

        Assert.Equal(NameMatchKind.Found, exact.Kind);
        Assert.Equal("1", Assert.Single(exact.Schools).Code);
        Assert.Equal(NameMatchKind.Ambiguous, acronym.Kind);                 // Boston University hoặc Baylor University: hỏi lại
        Assert.Equal(["1", "2"], acronym.Schools.Select(s => s.Code).OrderBy(c => c));
        Assert.Equal(NameMatchKind.NotFound, none.Kind);
    }

    [Fact]
    public async Task GoalSeek_FindsTheSchoolByCodeIgnoringCase()
    {
        var (service, profile) = GoalSetup();

        var result = await service.GoalSeekAsync(profile.UserId, "s", "match", default);

        Assert.Equal("S", result.Code);
    }

    private sealed class FakeAdvisor : IAdvisorClient
    {
        public List<string> Calls { get; } = [];

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRecommendationRepository(IReadOnlyList<RecommendSchoolInput> schools) : IRecommendationRepository
    {
        public List<Recommendation> Rows { get; } = [];

        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        {
            Rows.Add(recommendation);
            return Task.CompletedTask;
        }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.LastOrDefault(r => r.UserId == userId));

        public Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default) =>
            Task.FromResult(schools);

        public Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default) =>
            Task.FromResult<RecommendSchoolInput?>(schools.FirstOrDefault(s => s.University.Code == code));
    }

    private sealed class FakeProfileFlow(StudentProfile? profile, IReadOnlyList<ProfileActivity>? activities = null) : IProfileFlowRepository
    {
        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            profile is not null ? Task.FromResult<StudentProfile?>(profile) : throw new NotSupportedException();
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, IEnumerable<ProfileCertification> certifications, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult(activities ?? []);
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
