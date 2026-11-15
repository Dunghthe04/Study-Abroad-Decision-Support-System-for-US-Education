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
        StudentProfile? profile = null, IReadOnlyList<RecommendSchoolInput>? schools = null, IReadOnlyList<ProfileActivity>? activities = null,
        IReadOnlyList<ProfileCertification>? certifications = null)
    {
        var advisor = new FakeAdvisor();
        var store = new FakeRecommendationRepository(schools ?? []);
        return (new RecommendationService(advisor, store, new FakeProfileFlow(profile, activities, certifications)), advisor, store);
    }

    // certifications bỏ trống: các cột Sat/Act/Ielts/Toefl/Duolingo của profile (đọc lúc gọi, test có thể sửa sau Setup) được coi là điểm
    // đã thi và đổi thành dòng chứng chỉ, vì gợi ý trường đọc điểm từ bảng profile_certifications chứ không đọc cột trên profile
    private static IReadOnlyList<ProfileCertification> TakenCertificationsOf(StudentProfile p) =>
        new (string Type, decimal? Score)[]
            { ("sat", p.Sat), ("act", p.Act), ("ielts", p.Ielts), ("toefl", p.Toefl), ("duolingo", p.Duolingo) }
            .Where(x => x.Score is not null)
            .Select(x => new ProfileCertification { TestType = x.Type, OverallScore = x.Score, Category = "x" })
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

        await service.RecommendAsync(profile, schools);

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

        Assert.Equal(3, criteria.RootElement.GetProperty("tier").GetInt32());   // hồ sơ chưa có bậc ngoại khóa: coi là trung bình
        Assert.Equal("OK", criteria.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Recommend_SavedJsonKeepsTheShapeTheFrontendReads()
    {
        var (service, _, store) = Setup();
        await service.RecommendAsync(Profile(), [School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)]);

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

        await service1.RecommendAsync(Profile(), schools);
        await service2.RecommendAsync(Profile(), schools.AsEnumerable().Reverse().ToList());

        Assert.Equal(store1.Rows.Single().ItemsJson, store2.Rows.Single().ItemsJson);
    }

    [Fact]
    public async Task Recommend_AtMostFourSchoolsPerBlock()
    {
        var (service, _, store) = Setup();
        var schools = Enumerable.Range(1, 9)
            .Select(i => School($"S{i}", 30_000m + i * 100, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m))
            .ToList();

        await service.RecommendAsync(Profile(), schools);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(4, items.RootElement.GetProperty("match").GetArrayLength());
        Assert.Equal(9, items.RootElement.GetProperty("scored").GetArrayLength());   // danh sách đầy đủ vẫn được lưu
    }

    // ---------------------------------------------------------------- công thức đi qua service

    [Fact]
    public async Task Recommend_SchoolWithoutAcademicData_GoesToTheOtherBlock()
    {
        var (service, _, store) = Setup();

        await service.RecommendAsync(Profile(), [School("Bare", 30_000m, "none", Major)]);

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

        await service.RecommendAsync(Profile(), [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal("R", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal("Picky", items.RootElement.GetProperty("reach")[0].GetProperty("code").GetString());
        Assert.Equal(0.1, items.RootElement.GetProperty("scored")[0].GetProperty("acceptRate").GetDouble());
        // nhóm do tỷ lệ nhận quyết định thì phải nói lý do này, không chỉ các lý do về điểm
        var why = items.RootElement.GetProperty("reach")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString());
        Assert.Equal("LOW_RATE", why.First());
    }

    [Fact]
    public async Task Recommend_English_ShortByHalfABand_IsKeptWithAFlagAndALowerFit()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 6.5m;
        var school = School("Strict", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 7.0m);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];
        Assert.Contains("ENGLISH_NEAR", row.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(0.5, row.GetProperty("e").GetDouble());   // thiếu 0,5 band nên E = 0,5; tiếng Anh chỉ làm giảm fit score, không đổi nhóm
    }

    [Fact]
    public async Task Recommend_English_ShortByAFullBand_DropsTheSchool()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 5.5m;   // trường yêu cầu 7,0: thiếu 1,5 band
        var school = School("Strict", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 7.0m);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(0, items.RootElement.GetProperty("scored").GetArrayLength());   // bị loại, không vào ba nhóm
    }

    [Fact]
    public async Task Recommend_English_WithoutAnyCertificate_DropsEverySchool()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = null;
        var school = School("Open", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 6.0m);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(0, items.RootElement.GetProperty("scored").GetArrayLength());
    }

    // Thông báo tiếng Anh nằm trong criteria_json của lượt gợi ý
    [Fact]
    public async Task Recommend_English_TheCriteriaCarryTheHiddenCountAndTheLadder()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 5.5m;
        var strict = School("Strict", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 7.0m);   // thiếu 1,5 band: bị ẩn
        var open = School("Open", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 6.0m);       // thiếu 0,5 band: giữ

        await service.RecommendAsync(profile, [strict, open]);

        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        var english = criteria.RootElement.GetProperty("english");
        Assert.Equal(1, english.GetProperty("hidden").GetInt32());
        Assert.False(english.GetProperty("noCertificate").GetBoolean());
        var ladder = english.GetProperty("ladder");
        Assert.Equal(1, ladder.GetArrayLength());
        Assert.Equal(6.5, ladder[0].GetProperty("ielts").GetDouble());
        Assert.Equal(2, ladder[0].GetProperty("eligible").GetInt32());   // đạt IELTS 6,5 thì cả hai trường đủ điều kiện
    }

    [Fact]
    public async Task Recommend_English_TheCriteriaHaveNoNotice_WhenEnglishHidesNothing()
    {
        var (service, _, store) = Setup();
        var school = School("Open", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 6.0m);

        await service.RecommendAsync(Profile(), [school]);

        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        Assert.Equal(JsonValueKind.Null, criteria.RootElement.GetProperty("english").ValueKind);
    }

    [Fact]
    public async Task Recommend_English_AnEmptyListBecauseOfEnglishStillCarriesTheNotice()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = null;   // chưa có chứng chỉ nào
        var school = School("Open", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 6.0m);

        await service.RecommendAsync(profile, [school]);

        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        Assert.Equal(JsonValueKind.Null, criteria.RootElement.GetProperty("status").ValueKind);   // không còn trường nào để chọn
        var english = criteria.RootElement.GetProperty("english");
        Assert.True(english.GetProperty("noCertificate").GetBoolean());
        Assert.Equal(1, english.GetProperty("hidden").GetInt32());
    }

    // Học sinh không có IELTS: thành phần E tính từ bài thi học sinh có, so với mức tối thiểu cùng bài của trường
    [Theory]
    [InlineData(70, null, 0.33)]    // TOEFL 70 so với mức 80: thiếu 10 điểm, 10 / 15 = 0,67 band
    [InlineData(null, 100, 0.40)]   // Duolingo 100 so với mức 115: thiếu 15 điểm, 15 / 25 = 0,6 band
    [InlineData(85, null, 1.00)]    // TOEFL vượt mức tối thiểu
    public async Task Recommend_English_FitUsesTheTestTheStudentHas(int? toefl, int? duolingo, double expectedE)
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = null;
        profile.Toefl = toefl;
        profile.Duolingo = duolingo;
        var school = School("S", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m,
            minIelts: 6.5m, minToefl: 80m, minDuolingo: 115m);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(expectedE, items.RootElement.GetProperty("scored")[0].GetProperty("e").GetDouble(), 2);
    }

    [Fact]
    public async Task Recommend_English_FitUsesTheBestOfTheStudentsTests()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 5.5m;        // thiếu 1 band so với mức 6,5
        profile.Duolingo = 120m;     // vượt mức 115: bài này đạt nên E = 1
        var school = School("S", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m,
            minIelts: 6.5m, minDuolingo: 115m);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];
        Assert.Equal(1.0, row.GetProperty("e").GetDouble(), 9);
        Assert.DoesNotContain("ENGLISH_NEAR", row.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
    }

    [Fact]
    public async Task Recommend_English_FitDropsTheEnglishPart_WhenTheSchoolHasNoMinimum()
    {
        var (service, _, store) = Setup();
        var school = School("S", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);   // không có mức tối thiểu nào

        await service.RecommendAsync(Profile(), [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(JsonValueKind.Null, items.RootElement.GetProperty("scored")[0].GetProperty("e").ValueKind);
    }

    [Fact]
    public async Task Recommend_QualityComesFromTheRankingPosition()
    {
        var (service, _, store) = Setup();
        var ranked = School("Top", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (10, 100));
        var unranked = School("None", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);

        await service.RecommendAsync(Profile(), [ranked, unranked]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var rows = items.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.Equal(0.91, rows["Top"].GetProperty("q").GetDouble());               // 1 − (10 − 1) / 100
        Assert.Equal(JsonValueKind.Null, rows["None"].GetProperty("q").ValueKind);   // chưa có hạng: bỏ thành phần Q
    }

    [Fact]
    public async Task Recommend_WithNothingLeftAfterTheFilter_SavesAnEmptyRunWithoutCallingTheAdvisor()
    {
        var (service, advisor, store) = Setup();

        await service.RecommendAsync(Profile(), [School("D", 20_000m, "none", "History", sat: (1200, 1380))]);

        Assert.Empty(advisor.Calls);
        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(JsonValueKind.Null, criteria.RootElement.GetProperty("status").ValueKind);
        Assert.Equal(0, items.RootElement.GetProperty("scored").GetArrayLength());
    }

    // Bậc ngoại khóa do phần hồ sơ tính và lưu; gợi ý trường chỉ đọc. Chưa có bậc thì coi là trung bình (3).
    [Fact]
    public async Task Recommend_TheEcTierIsReadFromTheProfile_AndABlankOneMeansAverage()
    {
        var (service, _, store) = Setup();
        var school = School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);
        var strong = Profile();
        strong.ExtracurricularTier = 5;

        await service.RecommendAsync(strong, [school]);
        await service.RecommendAsync(Profile(), [school]);

        using var withTier = JsonDocument.Parse(store.Rows[0].CriteriaJson);
        using var blank = JsonDocument.Parse(store.Rows[1].CriteriaJson);
        Assert.Equal(5, withTier.RootElement.GetProperty("tier").GetInt32());
        Assert.Equal(3, blank.RootElement.GetProperty("tier").GetInt32());
    }

    [Fact]
    public async Task Recommend_TheEcTierChangesTheGroupOnlyAtSchoolsThatCareAboutIt()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.OverallGpa = null;
        profile.Sat = 985m;   // p ≈ 0,22
        profile.ExtracurricularTier = 5;
        var cares = School("Cares", 38_000m, "none", Major, sat: (1000, 1200), accept: 0.6m, ec: "very_important");
        var ignores = School("Ignores", 38_000m, "none", Major, sat: (1000, 1200), accept: 0.6m, ec: "not_considered");

        await service.RecommendAsync(profile, [cares, ignores]);

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
            service.RecommendAsync(Profile(need), [School("B", 38_000m, "none", Major)]));

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
            service.RecommendAsync(Profile(major: major), [School("B", 38_000m, "none", Major)]));

        Assert.Equal(RecommendationService.MissingMajorMessage, ex.Message);
        Assert.StartsWith("Hồ sơ", ex.Message);
        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Recommend_WithOptionalScholarship_StillRuns()
    {
        var (service, advisor, _) = Setup();

        await service.RecommendAsync(Profile(ProfileConstants.ScholarshipNeeds.Optional), [School("B", 38_000m, "none", Major)]);

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

        await service.RecommendAsync(profile, schools, [4, 2, 0]);
        var costFirst = MatchOrder(store.Rows.Last());
        var again = await service.RepickAsync(profile.UserId, [0, 2, 4], default);
        var qualityFirst = MatchOrder(again);

        Assert.Empty(advisor.Calls);   // đổi ưu tiên không gọi advisor
        Assert.Equal(2, store.Rows.Count);
        Assert.Equal(["Cheap", "Famous"], costFirst);
        Assert.Equal(["Famous", "Cheap"], qualityFirst);
        using var criteria = JsonDocument.Parse(again.CriteriaJson);
        Assert.Equal(0, criteria.RootElement.GetProperty("pref")[0].GetInt32());
        Assert.Equal(3, criteria.RootElement.GetProperty("tier").GetInt32());   // bậc đọc từ hồ sơ (chưa có): trung bình
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

        await service.RecommendAsync(profile, schools);
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
        IReadOnlyList<RecommendSchoolInput>? schools = null, int? ecTier = null, IReadOnlyList<ProfileActivity>? activities = null)
    {
        var profile = Profile();
        profile.ExtracurricularTier = ecTier;
        profile.OverallGpa = 3.5m;
        profile.Sat = 1290m;
        var list = schools ?? [School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)];
        var (service, advisor, store) = Setup(profile, list, activities);
        return (service, advisor, store, profile);
    }

    [Fact]
    public async Task Simulate_ScoresComeFromTheCertificationsTable_NotFromTheProfileColumns()
    {
        var (_, _, _, profile) = SimSetup();
        profile.Sat = 1500m;       // cột cũ: không được đọc
        var certs = new[]
        {
            new ProfileCertification { TestType = "sat", OverallScore = 1100m, Category = "standardized" },
            new ProfileCertification { TestType = "sat", OverallScore = 1250m, Category = "standardized" },
            new ProfileCertification { TestType = "ielts", OverallScore = 8m, IsTarget = true, Category = "english" }
        };
        var (service, _, _) = Setup(profile, [School("S", 38_000m, "none", Major, sat: (1200, 1380))], certifications: certs);

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        Assert.Equal(1250, result.Profile.Sat);     // thi nhiều lần: lấy cao nhất
        Assert.Null(result.Profile.Ielts);          // chỉ có điểm mục tiêu: chưa có điểm
    }

    [Fact]
    public async Task Simulate_ReturnsTheRealProfileValuesForTheSliders()
    {
        var (service, _, _, profile) = SimSetup(ecTier: 5, activities: FourDeclaredActivities());
        profile.Act = 28m;

        // đổi giá trị giả định không làm đổi "hồ sơ thật" trong kết quả
        var result = await service.SimulateAsync(profile.UserId, NoChange with { Gpa = 2.0m }, null, default);

        Assert.Equal(new SimulationProfile(3.5m, 1290, 28, 6.5m, 50_000m, ProfileConstants.ScholarshipNeeds.Preferred, 5, ActivityCount: 4), result.Profile);
    }

    // TOEFL giả định: trường yêu cầu TOEFL 100, học sinh chỉ có TOEFL 80 (thiếu hơn 1 band) nên bị ẩn; thử TOEFL 100 thì trường hiện ra
    [Fact]
    public async Task Simulate_ARaisedToefl_BringsBackASchoolHiddenForEnglish()
    {
        var school = School("T", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minToefl: 100m);
        var (service, _, _, profile) = SimSetup([school]);
        profile.Ielts = null;
        profile.Toefl = 80m;

        var result = await service.SimulateAsync(profile.UserId, NoChange with { Toefl = 100m }, null, default);

        Assert.Equal(0, result.Baseline.Kept);
        Assert.Equal(1, result.Simulated.Kept);
        Assert.Equal(80m, result.Profile.Toefl);   // "hồ sơ thật" vẫn là 80
        var change = Assert.Single(result.Changes);
        Assert.Equal("OUT", change.From);
    }

    // Duolingo giả định chỉ thay đúng điểm Duolingo, không đụng các bài thi khác
    [Fact]
    public async Task Simulate_ADuolingoValue_OnlyReplacesDuolingo()
    {
        var school = School("D", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minDuolingo: 120m);
        var (service, _, _, profile) = SimSetup([school]);
        profile.Ielts = null;
        profile.Duolingo = 95m;   // thiếu 25 điểm = 1 band: bị ẩn

        var result = await service.SimulateAsync(profile.UserId, NoChange with { Duolingo = 120m }, null, default);

        Assert.Equal(0, result.Baseline.Kept);
        Assert.Equal(1, result.Simulated.Kept);
        Assert.Equal(95m, result.Profile.Duolingo);
    }

    private static List<ProfileActivity> FourDeclaredActivities() => Enumerable.Range(0, 4)
        .Select(i => new ProfileActivity { Kind = "extracurricular", Title = $"Hoạt động {i}", Role = "member", ImpactLevel = 2, DurationMonths = 12 })
        .ToList();

    // Số hoạt động và giải thưởng đã khai (để giao diện nói "đã khai N hoạt động, M giải thưởng"); mục thiếu tên hoặc phạm vi không tính
    [Fact]
    public async Task Simulate_CountsTheDeclaredActivitiesAndAwards()
    {
        var activities = new List<ProfileActivity>
        {
            new() { Kind = "extracurricular", Title = "CLB Tin học", Role = "leader", ImpactLevel = 2, DurationMonths = 12 },
            new() { Kind = "extracurricular", Title = "Tình nguyện", Role = "member", ImpactLevel = 1, DurationMonths = 6 },
            new() { Kind = "award", Title = "Giải nhì Toán tỉnh", ImpactLevel = 3 },
            new() { Kind = "extracurricular", Title = "", ImpactLevel = 3 },                 // thiếu tên: không tính
            new() { Kind = "extracurricular", Title = "Thiếu phạm vi", ImpactLevel = null }, // thiếu phạm vi: không tính
        };
        var (service, _, _, profile) = SimSetup(activities: activities);

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        Assert.Equal(2, result.Profile.ActivityCount);
        Assert.Equal(1, result.Profile.AwardCount);
    }

    // ---- thông tin một trường (chat trả lời "học phí X bao nhiêu"): số thật từ dữ liệu, không cần hồ sơ đủ

    private static RecommendSchoolInput InfoSchool()
    {
        var school = School("S", 40_000m, "need_and_merit", Major, sat: (1370, 1530), accept: 0.30m, minIelts: 7m, minToefl: 100m);
        var o = school.Offering;
        o.LivingUsd = 15_000m; o.FeesUsd = 3_000m; o.OtherExpensesUsd = 2_000m;
        o.IntlAidAvgUsd = 25_000m; o.IntlAidRecipients = 120; o.SatPolicy = "optional";
        o.Act25 = 31; o.Act75 = 35; o.RetentionRate1Yr = 0.95m; o.GradRate6Yr = 0.91m;
        o.IntlApplicants = 1000; o.IntlAdmitted = 100;
        o.DeadlinesJson = """{"RD":"2027-01-05","ED":"2026-11-01"}""";
        school.University.City = "Boston"; school.University.Website = "https://example.edu";
        return school;
    }

    [Fact]
    public async Task SchoolInfo_ReturnsTheRealNumbersOfOneSchool()
    {
        var (service, _, store) = Setup(Profile(), [InfoSchool()]);
        store.Rankings.Add(new SchoolRanking("U.S. News Best Colleges 2027 – National Universities", "Tổng quát", 2027, 42, "#42", false));

        var info = await service.SchoolInfoAsync(Guid.NewGuid(), "s", default);   // mã không phân biệt hoa thường

        Assert.Equal(("S", "NY", "private", "Boston"), (info.Code, info.State, info.Control, info.City));
        Assert.Equal(new SchoolCostInfo(40_000m, 15_000m, 3_000m, 2_000m, 60_000m), info.Cost);
        Assert.Equal(new SchoolEnglishInfo(7m, 100m, null), info.English);
        Assert.Equal(new SchoolTestInfo("optional", 1370, 1530, 31, 35), info.Test);
        Assert.Equal(0.30m, info.Admission.AcceptRate);
        Assert.Equal(0.10m, info.Admission.IntlAcceptRate);      // 100 / 1000
        Assert.Equal(new SchoolAidInfo("need_and_merit", 25_000m, 120), info.Aid);
        Assert.Equal(new SchoolOutcomeInfo(0.95m, 0.91m), info.Outcomes);
        Assert.Equal([Major], info.Majors);
        Assert.Equal(42, Assert.Single(info.Rankings).Rank);
        Assert.Equal([new SchoolDeadline("ED", "2026-11-01"), new SchoolDeadline("RD", "2027-01-05")], info.Deadlines);   // theo ngày
    }

    [Fact]
    public async Task SchoolInfo_DropsSatQuartilesThatAreOnTheWrongScale()
    {
        var school = InfoSchool();
        school.Offering.Sat25 = 665; school.Offering.Sat75 = 739;   // điểm một phần thi, không phải SAT tổng
        var (service, _, _) = Setup(Profile(), [school]);

        var info = await service.SchoolInfoAsync(Guid.NewGuid(), "S", default);

        Assert.Null(info.Test.Sat25);
        Assert.Null(info.Test.Sat75);
    }

    [Fact]
    public async Task SchoolInfo_AUnknownSchool_Throws()
    {
        var (service, _, _) = Setup(Profile(), [InfoSchool()]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SchoolInfoAsync(Guid.NewGuid(), "ZZZ", default));

        Assert.StartsWith("Chưa có trường ZZZ", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không phải json")]
    [InlineData("[1,2]")]
    [InlineData("""{"RD":null,"ED":42}""")]
    public void ParseDeadlines_BadOrEmptyData_GivesNoDeadlinesInsteadOfFailing(string? json)
    {
        Assert.Empty(RecommendationService.ParseDeadlines(json));
    }

    // ---- tìm trường theo điều kiện (không dùng hồ sơ học sinh)

    private static RecommendSchoolInput SearchSchool(
        string code, string state, string control, decimal tuition, string aid = "none", string sat = "required",
        decimal? accept = 0.5m, decimal? ielts = 6.5m, string[]? majors = null, (int Rank, int Total)? rank = null, string? city = null, bool ivy = false)
    {
        var school = School(code, tuition, aid, majors?[0] ?? "Computer Science", accept: accept, minIelts: ielts, rank: rank);
        school.University.State = state;
        school.University.Control = control;
        school.University.City = city;
        school.University.IsIvy = ivy;
        school.Offering.SatPolicy = sat;
        school.Offering.LivingUsd = 10_000m;
        return majors is { Length: > 1 } ? school with { Majors = majors } : school;
    }

    private static async Task<SchoolSearchResult> SearchWith(
        SchoolSearchFilters filters, SchoolSort sort = SchoolSort.Quality, params RecommendSchoolInput[] schools)
    {
        var profile = Profile();
        var (service, _, _) = Setup(profile, schools);
        return await service.SearchSchoolsAsync(profile.UserId, filters, sort, default);
    }

    [Fact]
    public async Task Search_FiltersByStateAndAid_AndSaysHowManyMatched()
    {
        var result = await SearchWith(
            new SchoolSearchFilters(States: ["CA"], Aid: "any"), SchoolSort.Quality,
            SearchSchool("a", "CA", "private", 40_000m, aid: "need_and_merit"),
            SearchSchool("b", "CA", "public", 20_000m, aid: "none"),          // không có học bổng cho du học sinh
            SearchSchool("c", "TX", "private", 30_000m, aid: "merit_only"),    // sai bang
            SearchSchool("d", "CA", "public", 25_000m, aid: "merit_only"));

        Assert.Equal(2, result.Total);
        Assert.Equal(["a", "d"], result.Items.Select(i => i.Code).OrderBy(c => c));
    }

    [Fact]
    public async Task Search_AllConditionsMustHoldTogether()
    {
        var result = await SearchWith(
            new SchoolSearchFilters(States: ["TX"], Control: "public", MaxCostUsd: 35_000m, TestPolicy: "optional"), SchoolSort.Quality,
            SearchSchool("ok", "TX", "public", 20_000m, sat: "optional"),                 // chi phí 30.000 (20.000 học phí + 10.000 sinh hoạt)
            SearchSchool("pricey", "TX", "public", 40_000m, sat: "optional"),            // 50.000 vượt mức
            SearchSchool("private", "TX", "private", 20_000m, sat: "optional"),
            SearchSchool("needs-sat", "TX", "public", 20_000m, sat: "required"));

        Assert.Equal(["ok"], result.Items.Select(i => i.Code));
    }

    [Theory]
    [InlineData("optional", new[] { "opt", "none" })]       // test-optional và không nhận SAT đều là "không cần SAT"
    [InlineData("required", new[] { "req" })]
    public async Task Search_TestPolicy(string wanted, string[] expected)
    {
        var result = await SearchWith(
            new SchoolSearchFilters(TestPolicy: wanted), SchoolSort.Quality,
            SearchSchool("opt", "TX", "public", 10_000m, sat: "optional"),
            SearchSchool("none", "TX", "public", 10_000m, sat: "not_accepted"),
            SearchSchool("req", "TX", "public", 10_000m, sat: "required"));

        Assert.Equal(expected.OrderBy(c => c), result.Items.Select(i => i.Code).OrderBy(c => c));
    }

    [Theory]
    [InlineData("need", new[] { "n", "nm" })]
    [InlineData("merit", new[] { "m", "nm" })]
    [InlineData("any", new[] { "n", "m", "nm" })]
    public async Task Search_AidKinds(string wanted, string[] expected)
    {
        var result = await SearchWith(
            new SchoolSearchFilters(Aid: wanted), SchoolSort.Quality,
            SearchSchool("n", "TX", "public", 10_000m, aid: "need_only"),
            SearchSchool("m", "TX", "public", 10_000m, aid: "merit_only"),
            SearchSchool("nm", "TX", "public", 10_000m, aid: "need_and_merit"),
            SearchSchool("none", "TX", "public", 10_000m, aid: "none"));

        Assert.Equal(expected.OrderBy(c => c), result.Items.Select(i => i.Code).OrderBy(c => c));
    }

    [Fact]
    public async Task Search_MajorCityEnglishRankAcceptanceAndIvy()
    {
        var schools = new[]
        {
            SearchSchool("cs-boston", "MA", "private", 50_000m, ielts: 7m, majors: ["Computer Science", "History"], rank: (10, 100), city: "Boston", accept: 0.2m),
            SearchSchool("history", "MA", "private", 50_000m, ielts: 6m, majors: ["History"], rank: (60, 100), city: "Boston", accept: 0.6m),
            SearchSchool("ivy", "RI", "private", 70_000m, ielts: 7.5m, majors: ["Economics"], rank: (14, 100), accept: 0.05m, ivy: true),
        };

        Assert.Equal(["cs-boston"], (await SearchWith(new SchoolSearchFilters(Major: "computer science"), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["cs-boston", "history"], (await SearchWith(new SchoolSearchFilters(City: "boston"), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["history"], (await SearchWith(new SchoolSearchFilters(MaxIelts: 6.5m), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["cs-boston", "ivy"], (await SearchWith(new SchoolSearchFilters(MaxRank: 20), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["cs-boston", "ivy"], (await SearchWith(new SchoolSearchFilters(MaxAcceptRate: 0.25m), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["history"], (await SearchWith(new SchoolSearchFilters(MinAcceptRate: 0.5m), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
        Assert.Equal(["ivy"], (await SearchWith(new SchoolSearchFilters(Ivy: true), SchoolSort.Quality, schools)).Items.Select(i => i.Code));
    }

    [Fact]
    public async Task Search_ASchoolWithoutTheNumberBeingFilteredIsNotIncluded()
    {
        var unknownCost = SearchSchool("x", "TX", "public", 10_000m);
        unknownCost.Offering.TuitionUsd = null;                      // chưa có học phí nên không có tổng chi phí
        var known = SearchSchool("y", "TX", "public", 10_000m);

        var result = await SearchWith(new SchoolSearchFilters(MaxCostUsd: 50_000m), SchoolSort.Quality, unknownCost, known);

        Assert.Equal(["y"], result.Items.Select(i => i.Code));        // không thể nói trường x trong ngân sách khi chưa có số
    }

    [Fact]
    public async Task Search_SortsByQualityByDefault_WithTheUnrankedLast()
    {
        var result = await SearchWith(
            new SchoolSearchFilters(States: ["TX"]), SchoolSort.Quality,
            SearchSchool("unranked", "TX", "public", 10_000m),
            SearchSchool("mid", "TX", "public", 10_000m, rank: (50, 100)),
            SearchSchool("top", "TX", "public", 10_000m, rank: (1, 100)));

        Assert.Equal(["top", "mid", "unranked"], result.Items.Select(i => i.Code));
        Assert.Equal(1, result.Items[0].BestRank);
    }

    [Fact]
    public async Task Search_OtherSortOrders()
    {
        var schools = new[]
        {
            SearchSchool("cheap-hard", "TX", "public", 10_000m, accept: 0.1m),
            SearchSchool("mid", "TX", "public", 30_000m, accept: 0.5m),
            SearchSchool("pricey-easy", "TX", "public", 60_000m, accept: 0.9m),
        };
        var f = new SchoolSearchFilters(States: ["TX"]);

        Assert.Equal(["cheap-hard", "mid", "pricey-easy"], (await SearchWith(f, SchoolSort.LowestCost, schools)).Items.Select(i => i.Code));
        Assert.Equal(["pricey-easy", "mid", "cheap-hard"], (await SearchWith(f, SchoolSort.EasiestAdmission, schools)).Items.Select(i => i.Code));
        Assert.Equal(["cheap-hard", "mid", "pricey-easy"], (await SearchWith(f, SchoolSort.MostSelective, schools)).Items.Select(i => i.Code));
    }

    [Fact]
    public async Task Search_NothingMatches_SaysWhichConditionToDropToGetResults()
    {
        var result = await SearchWith(
            new SchoolSearchFilters(States: ["TX"], Control: "public", Aid: "any"), SchoolSort.Quality,
            SearchSchool("a", "TX", "private", 10_000m, aid: "need_only"),     // sai loại trường
            SearchSchool("b", "TX", "private", 10_000m, aid: "merit_only"),
            SearchSchool("c", "TX", "public", 10_000m, aid: "none"));           // không học bổng

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
        // bỏ "loai_truong" thì còn 2 trường (a, b); bỏ "hoc_bong" thì còn 1 trường (c); bỏ "bang" vẫn 0 nên không nêu
        Assert.Equal([("loai_truong", 2), ("hoc_bong", 1)], result.Relaxed.Select(r => (r.Filter, r.Total)));
    }

    [Fact]
    public async Task Search_ReturnsAtMostAHundredItemsButTheRealTotal()
    {
        var many = Enumerable.Range(0, 130).Select(i => SearchSchool($"s{i:000}", "TX", "public", 10_000m)).ToArray();

        var result = await SearchWith(new SchoolSearchFilters(States: ["TX"]), SchoolSort.Quality, many);

        Assert.Equal(130, result.Total);
        Assert.Equal(100, result.Items.Count);
    }

    // ---- SAT của trường phải là điểm tổng

    // Dữ liệu trường đôi khi lưu mốc SAT của MỘT phần thi (665 đến 739): SAT tổng 1200 của học sinh không được "vượt" mốc đó (vị trí 100%)
    [Fact]
    public async Task Recommend_ASchoolWithSectionScaleSat_IsTreatedAsMissingNotAsAHundredPercent()
    {
        var profile = Profile();
        profile.Sat = 1200m;
        var wrong = School("W", 30_000m, "none", Major, sat: (665, 739), accept: 0.6m, avgGpa: 3.5m);
        var right = School("R", 30_000m, "none", Major, sat: (1270, 1410), accept: 0.6m, avgGpa: 3.5m);
        var (service, _, store) = Setup(profile, [wrong, right]);

        await service.RecommendAsync(profile, [wrong, right]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var scored = items.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.Equal(JsonValueKind.Null, scored["W"].GetProperty("ps").ValueKind);        // thiếu dữ liệu, không phải 100
        Assert.Equal(JsonValueKind.Null, scored["W"].GetProperty("sat75").ValueKind);
        Assert.True(scored["R"].GetProperty("ps").GetDouble() < 50);                      // 1200 dưới mốc 25% của trường thật (1270)
        Assert.Equal(1270, scored["R"].GetProperty("sat25").GetInt32());
        Assert.Equal(1410, scored["R"].GetProperty("sat75").GetInt32());
    }

    [Theory]
    [InlineData(665, 739, false)]    // điểm một phần thi (toán)
    [InlineData(772, 816, false)]    // lệch thang: mốc 25% và 75% của một phần thi
    [InlineData(1270, 1410, true)]
    [InlineData(900, 1100, true)]    // trường rất dễ vào vẫn là SAT tổng
    [InlineData(1500, 1500, false)]  // mốc 75% phải lớn hơn mốc 25%
    [InlineData(1500, 1700, false)]  // vượt 1600
    public void SatScale_AcceptsOnlyTotalScoreQuartiles(int p25, int p75, bool total)
    {
        Assert.Equal(total, SatScale.IsTotal(p25, p75));
    }

    // Trường có nhiều sinh viên GPA 4,0 (53,7%): GPA 4,0 của học sinh chỉ ngang đa số, thẻ cần biết tỉ lệ đó để nói đúng
    [Fact]
    public async Task Recommend_StoresTheShareOfStudentsWithAPerfectGpa()
    {
        var profile = Profile();
        profile.OverallGpa = 4.0m;
        var school = School("T", 30_000m, "none", Major, sat: (1270, 1410), accept: 0.6m);
        var o = school.Offering;
        o.GpaPctBelow250 = 0.005m; o.GpaPct250To299 = 0.005m; o.GpaPct300To324 = 0.02m; o.GpaPct325To349 = 0.05m;
        o.GpaPct350To374 = 0.10m; o.GpaPct375To399 = 0.283m; o.GpaPct40 = 0.537m;
        var (service, _, store) = Setup(profile, [school]);

        await service.RecommendAsync(profile, [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];
        Assert.Equal(0.537, row.GetProperty("gpaTopShare").GetDouble(), 3);
        Assert.InRange(row.GetProperty("pg").GetDouble(), 70, 76);   // 4,0 ở khoảng 73%: ngang đa số, chưa vượt mốc 75%
    }

    // Kết quả mang theo SAT và GPA của học sinh để thẻ trường so với khoảng của trường
    [Fact]
    public async Task Recommend_StoresTheStudentsSatAndGpaWithTheCriteria()
    {
        var profile = Profile();
        profile.Sat = 1200m;
        profile.OverallGpa = 3.7m;
        var school = School("R", 30_000m, "none", Major, sat: (1270, 1410), accept: 0.6m, avgGpa: 3.5m);
        var (service, _, store) = Setup(profile, [school]);

        await service.RecommendAsync(profile, [school]);

        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        Assert.Equal(1200, criteria.RootElement.GetProperty("studentSat").GetInt32());
        Assert.Equal(3.7m, criteria.RootElement.GetProperty("studentGpa").GetDecimal());
    }

    // ---- giải thích theo hồ sơ thật hoặc theo giả định đang xem

    private static async Task<(RecommendationService Service, FakeAdvisor Advisor, StudentProfile Profile)> SavedRunSetup()
    {
        var (service, advisor, _, profile) = SimSetup();   // hồ sơ thật: GPA 3,5, SAT 1290, trường S là Vừa sức
        await service.RecommendAsync(profile, [School("S", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)]);
        return (service, advisor, profile);
    }

    [Fact]
    public async Task Explain_WithoutOverrides_UsesTheSavedRunAndTheRealProfile()
    {
        var (service, advisor, profile) = await SavedRunSetup();

        await service.ExplainAsync(profile.UserId, "S", default);

        Assert.Equal(3.5, advisor.LastExplain!.Student.Gpa);
        Assert.Equal(1290, advisor.LastExplain.Student.Sat);
        Assert.Null(advisor.LastExplain.Basis);
        Assert.Equal("M", advisor.LastExplain.School.G2);
    }

    // Giải thích đi theo cái đang xem: có giá trị giả định thì số của học sinh và nhóm của trường lấy từ kết quả giả định
    [Fact]
    public async Task Explain_WithOverrides_UsesTheSimulatedNumbersAndTheSimulatedGroup()
    {
        var (service, advisor, profile) = await SavedRunSetup();
        var overrides = NoChange with { Gpa = 2.5m, Sat = 1000m };

        await service.ExplainAsync(profile.UserId, "S", default, overrides, "GPA 2.5, SAT 1000");

        Assert.Equal(2.5, advisor.LastExplain!.Student.Gpa);
        Assert.Equal(1000, advisor.LastExplain.Student.Sat);
        Assert.Equal("GPA 2.5, SAT 1000", advisor.LastExplain.Basis);
        Assert.Equal("R", advisor.LastExplain.School.G2);   // điểm giả định thấp hơn nên trường xuống Khó vào
    }

    [Fact]
    public async Task ExplainCard_WithOverrides_FollowsTheSimulatedGroup()
    {
        var (service, _, profile) = await SavedRunSetup();

        var real = await service.ExplainCardAsync(profile.UserId, "S", default);
        var simulated = await service.ExplainCardAsync(profile.UserId, "S", default, NoChange with { Gpa = 2.5m, Sat = 1000m });

        Assert.Equal(("M", "R"), (real.G2, simulated.G2));
    }

    [Fact]
    public async Task ExplainCard_ShowsTheFullStateName_ButTheAdvisorStillGetsTheCode()
    {
        var (service, advisor, profile) = await SavedRunSetup();

        var card = await service.ExplainCardAsync(profile.UserId, "S", default);
        await service.ExplainAsync(profile.UserId, "S", default);

        Assert.Equal("New York", card.State);
        Assert.Equal("NY", advisor.LastExplain!.School.State);
    }

    [Fact]
    public async Task Explain_WithEmptyOverrides_IsTheRealProfile()
    {
        var (service, advisor, profile) = await SavedRunSetup();

        await service.ExplainAsync(profile.UserId, "S", default, NoChange, "không có gì");

        Assert.Null(advisor.LastExplain!.Basis);   // không có giá trị nào thì không coi là giả định
        Assert.Equal(3.5, advisor.LastExplain.Student.Gpa);
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
    public async Task Simulate_UsesTheEcTierSavedInTheProfile()
    {
        var (service, _, store, profile) = SimSetup(ecTier: 5);

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        using var criteria = JsonDocument.Parse(result.CriteriaJson);
        Assert.Equal(5, criteria.RootElement.GetProperty("tier").GetInt32());
        Assert.Empty(store.Rows);   // mô phỏng không lưu gì
    }

    [Fact]
    public async Task Simulate_WithoutASavedEcTier_UsesTheAverageTier()
    {
        var (service, _, _, profile) = SimSetup();

        var result = await service.SimulateAsync(profile.UserId, NoChange, null, default);

        using var criteria = JsonDocument.Parse(result.CriteriaJson);
        Assert.Equal(3, criteria.RootElement.GetProperty("tier").GetInt32());
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
    [InlineData(130.0, null)]   // TOEFL vượt 120
    [InlineData(null, 5.0)]     // Duolingo dưới 10
    [InlineData(null, 200.0)]   // Duolingo vượt 160
    public async Task Simulate_RejectsAToeflOrDuolingoOutOfRange(double? toefl, double? duolingo)
    {
        var (service, _, _, profile) = SimSetup();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SimulateAsync(profile.UserId, NoChange with { Toefl = (decimal?)toefl, Duolingo = (decimal?)duolingo }, null, default));
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
        await service.RecommendAsync(profile, schools, [3, 1, 1]);

        var result = await service.SimulateAsync(profile.UserId, NoChange, [3, 1, 1], default);

        using var saved = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        using var simulated = JsonDocument.Parse(result.ItemsJson);
        Assert.Equal(saved.RootElement.GetRawText(), simulated.RootElement.GetRawText());
    }

    // Ảnh của trường đi cùng thẻ trường để giao diện hiển thị; trường chưa có ảnh thì để null
    [Fact]
    public async Task RecommendAsync_CarriesTheLogoAndCoverOfEachSchool_NullWhenMissing()
    {
        var profile = Profile();
        var withImages = School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)
            with { Images = new SchoolImages("https://cdn.test/b-logo.webp", "https://cdn.test/b-cover.webp") };
        var logoOnly = School("C", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)
            with { Images = new SchoolImages("https://cdn.test/c-logo.webp", null) };
        var without = School("E", 31_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var schools = new[] { withImages, logoOnly, without };
        var (service, _, store) = Setup(profile, schools);

        await service.RecommendAsync(profile, schools);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var scored = items.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.Equal("https://cdn.test/b-logo.webp", scored["B"].GetProperty("logoUrl").GetString());
        Assert.Equal("https://cdn.test/b-cover.webp", scored["B"].GetProperty("coverUrl").GetString());
        Assert.Equal("https://cdn.test/c-logo.webp", scored["C"].GetProperty("logoUrl").GetString());
        Assert.Equal(JsonValueKind.Null, scored["C"].GetProperty("coverUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, scored["E"].GetProperty("logoUrl").ValueKind);
    }

    // ---------------------------------------------------------------- dữ liệu giả

    private static RecommendSchoolInput School(
        string code, decimal tuition, string aidPolicy, string major,
        (int Low, int High)? sat = null, decimal? accept = null, decimal? avgGpa = null,
        decimal? minIelts = null, decimal? minToefl = null, decimal? minDuolingo = null, (int Rank, int Total)? rank = null, int? admitted = null, int? applicants = null,
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
            MinToefl = minToefl,
            MinDuolingo = minDuolingo,
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
        var student = new StudentAcademics(3.0m, 1000, null, AcademicBlend.NeutralEcTier);   // hồ sơ chưa có bậc ngoại khóa
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

    // Trường bị ẩn vì tiếng Anh: khung nói rõ lý do và mức cần đạt, không tính như thể trường nằm trong danh sách
    [Fact]
    public async Task GoalSeek_ASchoolHiddenForEnglish_SaysWhatScoreIsNeeded()
    {
        var strict = School("E", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m,
            minIelts: 7.5m, minToefl: 100m, minDuolingo: 130m);
        var (service, profile) = GoalSetup(strict);
        profile.Ielts = 6.0m;   // thiếu 1,5 band: bị loại

        var result = await service.GoalSeekAsync(profile.UserId, "E", "match", default);

        Assert.Equal("ENGLISH", result.FilterReason);
        Assert.Equal(7.5m, result.English!.MinIelts);
        Assert.Equal(100m, result.English.MinToefl);
        Assert.Equal(130m, result.English.MinDuolingo);
        Assert.Equal(1.5m, result.English.ShortfallBand);
    }

    [Fact]
    public async Task GoalSeek_WithoutAnEnglishCertificate_SaysSo()
    {
        var strict = School("E", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m, minIelts: 6.5m);
        var (service, profile) = GoalSetup(strict);
        profile.Ielts = null;

        var result = await service.GoalSeekAsync(profile.UserId, "E", "match", default);

        Assert.Equal("ENGLISH_NO_CERT", result.FilterReason);
        Assert.Null(result.English!.ShortfallBand);
    }

    // ---- vị trí của trường và các mức đích

    // Trường Khó vào: có hai mức đích (Vừa sức và An toàn), mỗi mức một kết quả riêng
    [Fact]
    public async Task GoalSeek_AReachSchool_HasBothTargetsAbove()
    {
        var (service, profile) = GoalSetup();

        var result = await service.GoalSeekAsync(profile.UserId, "S", null, default);

        Assert.Equal(new GoalPosition("in_list", "R", null, false), result.Position);
        Assert.Equal(["M", "S"], result.Targets!.Select(t => t.Target));
        Assert.All(result.Targets!, t => Assert.NotEqual("already_reached", t.Outcome));
        Assert.True(result.Targets![0].Alone.Any(p => p.Changes.Single().Factor == "sat"));
    }

    // Trường Vừa sức: chỉ còn mức An toàn
    [Fact]
    public async Task GoalSeek_AMatchSchool_HasOnlyTheSafetyTarget()
    {
        var (service, profile) = GoalSetup();
        profile.OverallGpa = 3.5m;
        profile.Sat = 1290m;

        var result = await service.GoalSeekAsync(profile.UserId, "S", null, default);

        Assert.Equal("M", result.Position!.Group);
        Assert.Equal(["S"], result.Targets!.Select(t => t.Target));
    }

    // Trường đã An toàn: không có mức đích nào, chỉ báo vị trí
    [Fact]
    public async Task GoalSeek_ASafetySchool_HasNoTargets()
    {
        var (service, profile) = GoalSetup();
        profile.OverallGpa = 4.0m;
        profile.Sat = 1560m;

        var result = await service.GoalSeekAsync(profile.UserId, "S", null, default);

        Assert.Equal(new GoalPosition("in_list", "S", null, false), result.Position);
        Assert.Empty(result.Targets!);
    }

    // Trường vượt ngân sách nhưng có học bổng: vẫn trong danh sách, ở khối cần học bổng lớn
    [Fact]
    public async Task GoalSeek_ASchoolThatNeedsAid_IsInTheListFlaggedNeedAid()
    {
        var pricey = School("P", 62_000m, "preferred", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m);
        var (service, profile) = GoalSetup(pricey);

        var result = await service.GoalSeekAsync(profile.UserId, "P", null, default);

        Assert.Equal("in_list", result.Position!.Status);
        Assert.True(result.Position.NeedAid);
    }

    // Số liệu tuyển sinh kèm theo để lời giải thích nói được con số và mốc
    [Fact]
    public async Task GoalSeek_CarriesTheAcceptRateAndTheThresholds()
    {
        var (service, profile) = GoalSetup();

        var stats = (await service.GoalSeekAsync(profile.UserId, "S", null, default)).Stats!;

        Assert.Equal(0.60m, stats.AcceptRate);
        Assert.Equal((0.15, 0.5, 0.5), (stats.AlwaysReachRate, stats.SafetyMinRate, stats.IntlRatioCut));
    }

    // Trường bị loại: vẫn có nhóm học thuật ("nếu đủ điều kiện") và các mức đích, kèm lý do bị loại
    [Theory]
    [InlineData("MAJOR")]
    [InlineData("ENGLISH")]
    [InlineData("BUDGET")]
    public async Task GoalSeek_AnExcludedSchool_KeepsItsGroupAndTargets_AndSaysWhyItIsOut(string reason)
    {
        var school = reason switch
        {
            "MAJOR" => School("X", 38_000m, "none", "Music", sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m),
            "ENGLISH" => School("X", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m, minIelts: 7.5m),
            _ => School("X", 62_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m),
        };
        var (service, profile) = GoalSetup(school);
        if (reason == "ENGLISH") profile.Ielts = 6.0m;

        var result = await service.GoalSeekAsync(profile.UserId, "X", null, default);

        Assert.Equal("excluded", result.Position!.Status);
        Assert.Equal(reason, result.Position.ExcludeReason);
        Assert.Equal("R", result.Position.Group);
        Assert.Equal(["M", "S"], result.Targets!.Select(t => t.Target));
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

        public ExplainRequestDto? LastExplain { get; private set; }

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default)
        {
            LastExplain = request;
            return Task.FromResult(new ExplainResponseDto("1.0", "gemini", "OK", "Tóm tắt giải thích.", [], []));
        }

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRecommendationRepository(IReadOnlyList<RecommendSchoolInput> schools) : IRecommendationRepository
    {
        public List<Recommendation> Rows { get; } = [];
        public List<SchoolRanking> Rankings { get; } = [];

        public Task<IReadOnlyList<SchoolRanking>> ListSchoolRankingsAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchoolRanking>>(Rankings);

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

    private sealed class FakeProfileFlow(
        StudentProfile? profile, IReadOnlyList<ProfileActivity>? activities = null, IReadOnlyList<ProfileCertification>? certifications = null)
        : IProfileFlowRepository
    {
        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            profile is not null ? Task.FromResult<StudentProfile?>(profile) : throw new NotSupportedException();
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UsStateOption>>([new("NY", "New York", "northeast", true)]);
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, IEnumerable<ProfileCertification> certifications, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult(activities ?? []);
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult(certifications ?? (profile is null ? [] : TakenCertificationsOf(profile)));
    }
}
