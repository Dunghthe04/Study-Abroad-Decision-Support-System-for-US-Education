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
    public void Recommend_ExtracurricularBoostsReachAndMatchOnly()
    {
        var s0 = RecommendationScorer.Recommend(Student(ec: 0), Demo, new RecommendSettings()).ToDictionary(x => x.Candidate.Code);
        var s10 = RecommendationScorer.Recommend(Student(ec: 10), Demo, new RecommendSettings()).ToDictionary(x => x.Candidate.Code);

        Assert.True(s10["DEMO_UB"].Score > s0["DEMO_UB"].Score);       // Match được cộng
        Assert.Equal(s0["DEMO_UC"].Score, s10["DEMO_UC"].Score);      // Safety không đổi
    }

    [Fact]
    public void Recommend_SameInput_SameOrder()                       // tiêu chí 5
    {
        var a = RecommendationScorer.Recommend(Student(), Demo, new RecommendSettings()).Select(x => x.Candidate.Code);
        var b = RecommendationScorer.Recommend(Student(), Demo.Reverse().ToList(), new RecommendSettings()).Select(x => x.Candidate.Code);
        Assert.Equal(a, b);                                           // đảo thứ tự đầu vào vẫn ra cùng kết quả
    }
}
