using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class RecommendationScorerTests
{
    private static readonly string[] CsBiz = ["Computer Science", "Business"];

    private static SchoolCandidate School(string code, string state, string[] majors,
        decimal? avg, int? p25, int? p75, decimal? tuition, decimal? living, decimal? fees) =>
        new(Guid.NewGuid(), Guid.NewGuid(), code, code, state, majors, avg, p25, p75, tuition, living, fees);

    // Giống 9 trường DEMO trong DbSeeder
    private static readonly SchoolCandidate[] Demo =
    [
        School("DEMO_UA", "CA", CsBiz, 3.90m, 1450, 1560, 60000, 18000, 2000), // 80k
        School("DEMO_UB", "NY", CsBiz, 3.50m, 1250, 1400, 35000, 14000, 1000), // 50k
        School("DEMO_UC", "TX", CsBiz, 3.10m, 1050, 1200, 22000, 12000, 1000), // 35k
        School("DEMO_UD", "WA", CsBiz, null, 1200, 1350, 26000, 13000, 1000),  // 40k
        School("DEMO_UE", "FL", CsBiz, 3.40m, null, null, 30000, 14000, 1000), // 45k
        School("DEMO_UF", "OH", CsBiz, null, null, null, 18000, 11000, 1000),  // 30k
        School("DEMO_UG", "IL", CsBiz, 3.30m, 1150, 1300, null, null, null),   // ? 
        School("DEMO_UH", "MA", CsBiz, 3.60m, 1300, 1450, 95000, 22000, 3000), // 120k
        School("DEMO_UI", "PA", ["Nursing"], 3.40m, 1200, 1350, 28000, 13000, 1000),
    ];

    private static readonly RecommendSettings Cfg = new();
    private static SchoolCandidate B => Demo[1];   // DEMO_UB: Match, tổng chi phí 50k

    private static StudentSnapshot Student(decimal budget = 50000, decimal? ec = null, string[]? states = null) =>
        new("undergraduate", "Computer Science", 3.5m, 1300, budget, states ?? [], ec);

    private static Dictionary<string, AdmissionCategory> Run(StudentSnapshot s, RecommendSettings? cfg = null) =>
        RecommendationScorer.Recommend(s, Demo, cfg ?? new RecommendSettings())
            .ToDictionary(x => x.Candidate.Code, x => x.AdmissionCategory);

    [Fact]
    public void Recommend_DemoProfile_ExpectedCategories()
    {
        var r = Run(Student());

        Assert.Equal(AdmissionCategory.Safety, r["DEMO_UC"]);
        Assert.Equal(AdmissionCategory.Match, r["DEMO_UB"]);
        Assert.Equal(AdmissionCategory.Match, r["DEMO_UD"]);          // chỉ có SAT
        Assert.Equal(AdmissionCategory.Match, r["DEMO_UE"]);          // chỉ có GPA
        Assert.Equal(AdmissionCategory.Match, r["DEMO_UG"]);          // thiếu chi phí vẫn giữ
        Assert.Equal(AdmissionCategory.InsufficientData, r["DEMO_UF"]);
        Assert.False(r.ContainsKey("DEMO_UA"));                       // 80k > 55k
        Assert.False(r.ContainsKey("DEMO_UH"));                       // 120k
        Assert.False(r.ContainsKey("DEMO_UI"));                       // khác ngành
    }

    [Fact]
    public void Recommend_HigherBudget_IncludesReachSchool()
    {
        var r = Run(Student(budget: 100000));
        Assert.Equal(AdmissionCategory.Reach, r["DEMO_UA"]);          // GPA 3.5 vs 3.9 → thấp
    }

    [Fact]
    public void Recommend_UnknownCost_IsFlagged()
    {
        var g = RecommendationScorer.Recommend(Student(), Demo, new RecommendSettings())
            .Single(x => x.Candidate.Code == "DEMO_UG");
        Assert.True(g.CostUnknown);
    }

    [Fact]
    public void Recommend_PreferredStates_FiltersOthers()
    {
        var r = Run(Student(states: ["NY", "TX"]));
        Assert.Equal(["DEMO_UB", "DEMO_UC"], r.Keys.Order().ToArray());
    }

    [Fact]
    public void Recommend_RespectsPerCategoryLimit()
    {
        var cfg = new RecommendSettings { PerCategory = new PerCategoryLimits(Reach: 3, Match: 2, Safety: 4) };
        var r = Run(Student(), cfg);
        Assert.Equal(2, r.Values.Count(c => c == AdmissionCategory.Match));
    }

    [Fact]
    public void Recommend_DifferentProfiles_DifferentLists()        // tiêu chí 1
    {
        var poor = Run(Student(budget: 40000));
        var rich = Run(Student(budget: 100000));
        Assert.NotEqual(poor.Keys.Order(), rich.Keys.Order());
    }

    [Fact]
    public void Recommend_SameInput_SameOrder()                       // tiêu chí 5
    {
        var a = RecommendationScorer.Recommend(Student(), Demo, new RecommendSettings()).Select(x => x.Candidate.Code);
        var b = RecommendationScorer.Recommend(Student(), Demo.Reverse().ToList(), new RecommendSettings()).Select(x => x.Candidate.Code);
        Assert.Equal(a, b);                                           // đảo thứ tự đầu vào vẫn ra cùng kết quả
    }

    [Fact]
    public void Recommend_SortedByCategory_ReachMatchSafetyThenInsufficient()
    {
        var order = RecommendationScorer.Recommend(Student(budget: 100000), Demo, new RecommendSettings())
            .Select(x => x.AdmissionCategory).ToList();
        Assert.Equal(order.Order().ToList(), order);                  // Reach(0) → Match(1) → Safety(2) → InsufficientData(3)
    }

    [Fact]
    public void Recommend_ChangeWeights_ChangesOrderWithinGroup()    // tiêu chí 4
    {
        // Cả hai đều Match; X rẻ hơn, Y học thuật hợp hơn
        SchoolCandidate[] two =
        [
            School("X", "CA", CsBiz, 3.50m, 1250, 1400, 30000, 10000, 0), // 40k
        School("Y", "CA", CsBiz, 3.40m, 1200, 1400, 45000, 13000, 0), // 58k
    ];
        var student = Student(budget: 60000);
        string[] Run2(SawWeights w) => RecommendationScorer.Recommend(student, two, new RecommendSettings { Weights = w })
            .Select(x => x.Candidate.Code).ToArray();

        Assert.Equal(["Y", "X"], Run2(new SawWeights(Academic: 1, Finance: 0, English: 0, Extracurricular: 0)));
        Assert.Equal(["X", "Y"], Run2(new SawWeights(Academic: 0, Finance: 1, English: 0, Extracurricular: 0)));
    }

    [Fact]
    public void Score_StudentMissingData_ReweightsRemaining()
    {
        // HỌC SINH chưa nhập ngân sách → bỏ tiêu chí tài chính, điểm = điểm học thuật (tiêu chí duy nhất còn lại)
        var g = RecommendationScorer.Score(Student() with { AnnualBudgetUsd = null }, B, Cfg);
        Assert.Null(g.Fit.Finance);
        Assert.Equal(Math.Round(g.Fit.Academic!.Value, 4), g.Score);
    }

    [Fact]
    public void Score_SchoolMissingCost_GetsNeutralFinance()
    {
        // TRƯỜNG thiếu học phí → giá trị trung tính, không được lợi hơn trường có số liệu thật
        var g = RecommendationScorer.Score(Student(), Demo.Single(x => x.Code == "DEMO_UG"), Cfg);
        Assert.True(g.CostUnknown);
        Assert.Equal(Cfg.MissingValue, g.Fit.Finance);
    }

    [Fact]
    public void Score_SchoolMissingAcademicData_GetsNeutralAcademic()
    {
        // Trường không có GPA TB lẫn SAT → học thuật trung tính (nhóm vẫn là "chưa đủ dữ liệu")
        var g = RecommendationScorer.Score(Student(), Demo.Single(x => x.Code == "DEMO_UF"), Cfg);
        Assert.Equal(AdmissionCategory.InsufficientData, g.AdmissionCategory);
        Assert.Equal(Cfg.MissingValue, g.Fit.Academic);
    }

    [Fact]
    public void Score_OregonStateExample_MatchesHandCalculation()
    {
        // Số liệu thật từ file của Dương; học sinh GPA 3.5, SAT 1300, IELTS 6.5, ngoại khóa 3.2/4, ngân sách 60k
        var osu = new SchoolCandidate(Guid.NewGuid(), Guid.NewGuid(), "209542", "Oregon State University", "OR",
            ["Computer Science"], 3.70m, 1140, 1400, 38190, 16386, 2592, AcceptanceRate: 0.773m, MinIelts: 6m, MinToefl: 70m);
        var student = new StudentSnapshot("undergraduate", "Computer Science", 3.5m, 1300, 60000, [], 3.2m, Ielts: 6.5m);

        var r = RecommendationScorer.Score(student, osu, new RecommendSettings());

        Assert.Equal(AdmissionCategory.Match, r.AdmissionCategory);
        Assert.Equal(57168m, r.TotalCostUsd);
        Assert.Equal(1m, r.Fit.English);
        Assert.Equal(EnglishStatus.Met, r.English);
        Assert.Equal(0.8m, r.Fit.Extracurricular);
        // Học thuật = (GPA (3.5−3.7+0.3)/0.6 = 0.167 + SAT (1300−1140)/260 = 0.615) / 2 = 0.391
        // Tài chính = (60000 − 57168) / 60000 = 0.047;  Tiếng Anh = 1;  Ngoại khóa = 0.8
        // Trọng số ngoại khóa = 0.2 × (1 − 0.773) = 0.0454  →  tổng trọng số = 0.4 + 0.3 + 0.1 + 0.0454 = 0.8454
        // Điểm = (0.4×0.391 + 0.3×0.047 + 0.1×1 + 0.0454×0.8) / 0.8454 ≈ 0.3630
        Assert.InRange(r.Score!.Value, 0.362m, 0.364m);
    }

    [Fact]
    public void Score_StrongExtracurricular_FavoursSelectiveSchools()
    {
        var selective = School("SEL", "CA", CsBiz, 3.5m, 1250, 1400, 30000, 10000, 0) with { AcceptanceRate = 0.10m };
        var open = School("OPEN", "CA", CsBiz, 3.5m, 1250, 1400, 30000, 10000, 0) with { AcceptanceRate = 0.80m };
        var cfg = new RecommendSettings();

        var strong = Student(ec: 4);
        Assert.True(RecommendationScorer.Score(strong, selective, cfg).Score > RecommendationScorer.Score(strong, open, cfg).Score);

        var weak = Student(ec: 0);                                    // ngoại khóa yếu → bất lợi hơn ở trường chọn lọc
        Assert.True(RecommendationScorer.Score(weak, selective, cfg).Score < RecommendationScorer.Score(weak, open, cfg).Score);

        var unknown = Student(ec: null);                              // chưa có điểm ngoại khóa → bỏ tiêu chí, hai trường bằng điểm
        Assert.Equal(RecommendationScorer.Score(unknown, selective, cfg).Score, RecommendationScorer.Score(unknown, open, cfg).Score);
    }

    [Fact]
    public void Score_StrongExtracurricular_DoesNotHurtOpenSchools()
    {
        // Cách cũ nhân điểm ngoại khóa với (1 − tỉ lệ nhận) nên trường dễ vào bị kéo tụt điểm
        var open = B with { AcceptanceRate = 0.95m };
        var without = RecommendationScorer.Score(Student(), open, Cfg).Score;
        var strong = RecommendationScorer.Score(Student(ec: 4), open, Cfg).Score;
        Assert.True(strong >= without);
    }

    // ---------- Cao đẳng cộng đồng tuyển sinh mở ----------

    [Fact]
    public void Score_CommunityCollegeWithoutData_IsOpenAdmissionSafety()
    {
        var cc = School("CC_1", "CA", CsBiz, null, null, null, 12000, 10000, null);
        var r = RecommendationScorer.Score(Student() with { StudyLevel = "community_college" }, cc, Cfg);
        Assert.Equal(AdmissionCategory.Safety, r.AdmissionCategory);
        Assert.True(r.OpenAdmission);
    }

    [Fact]
    public void Score_UndergraduateWithoutData_StaysInsufficient()
    {
        var uni = School("U_1", "CA", CsBiz, null, null, null, 12000, 10000, null);
        var r = RecommendationScorer.Score(Student(), uni, Cfg);   // bậc đại học không nằm trong OpenAdmissionLevels
        Assert.Equal(AdmissionCategory.InsufficientData, r.AdmissionCategory);
        Assert.False(r.OpenAdmission);
    }

    // ---------- Điều kiện tiếng Anh ----------

    [Fact]
    public void Score_EnglishBelowMinimum_IsFlagged()
    {
        var r = RecommendationScorer.Score(Student() with { Ielts = 6.0m }, B with { MinIelts = 6.5m }, Cfg);
        Assert.Equal(EnglishStatus.BelowMin, r.English);
        Assert.InRange(r.Fit.English!.Value, 0.92m, 0.93m);       // 6.0 / 6.5
    }

    [Fact]
    public void Score_EnglishAboveMinimum_IsMetAndCappedAtOne()
    {
        var r = RecommendationScorer.Score(Student() with { Ielts = 8.0m }, B with { MinIelts = 6.5m }, Cfg);
        Assert.Equal(EnglishStatus.Met, r.English);
        Assert.Equal(1m, r.Fit.English);                           // cao hơn mức tối thiểu không được thêm điểm
    }

    [Fact]
    public void Score_EnglishAnyTestPassing_IsEnough()
    {
        // IELTS chưa đạt nhưng TOEFL đạt → tính là đạt
        var s = Student() with { Ielts = 6.0m, Toefl = 90m };
        var r = RecommendationScorer.Score(s, B with { MinIelts = 6.5m, MinToefl = 80m }, Cfg);
        Assert.Equal(EnglishStatus.Met, r.English);
    }

    [Fact]
    public void Score_EnglishDuolingo_IsChecked()
    {
        var r = RecommendationScorer.Score(Student() with { Duolingo = 100m }, B with { MinDuolingo = 110m }, Cfg);
        Assert.Equal(EnglishStatus.BelowMin, r.English);
    }

    [Fact]
    public void Score_StudentWithoutEnglishTest_IsNoScoreAndDropped()
    {
        var r = RecommendationScorer.Score(Student(), B with { MinIelts = 6.5m }, Cfg);
        Assert.Equal(EnglishStatus.NoScore, r.English);
        Assert.Null(r.Fit.English);
    }

    [Fact]
    public void Score_SchoolWithoutEnglishMinimum_IsUnknownAndNeutral()
    {
        var r = RecommendationScorer.Score(Student() with { Ielts = 6.5m }, B, Cfg);
        Assert.Equal(EnglishStatus.Unknown, r.English);
        Assert.Equal(Cfg.MissingValue, r.Fit.English);
    }

    // ---------- So ngành: trùng tên, không phân biệt hoa thường ----------

    [Fact]
    public void Recommend_Major_ExactNameIgnoringCaseAndSpaces()
    {
        var r = RecommendationScorer.Recommend(Student() with { Major = "  computer science " }, Demo, Cfg)
            .Select(x => x.Candidate.Code).ToList();
        Assert.Contains("DEMO_UB", r);                               // trường ghi "Computer Science"
        Assert.DoesNotContain("DEMO_UI", r);                         // trường chỉ có Nursing
    }

    [Fact]
    public void Recommend_Major_NonStandardName_NoMatch()            // tên chưa chuẩn hóa → không đoán
    {
        Assert.Empty(RecommendationScorer.Recommend(Student() with { Major = "IT" }, Demo, Cfg));
    }
}
