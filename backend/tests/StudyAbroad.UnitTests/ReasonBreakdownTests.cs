using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class ReasonBreakdownTests
{
    private static readonly RecommendSettings Cfg = new();

    private static SchoolCandidate School(decimal? avg, int? p25, int? p75, decimal? tuition = 40000, decimal? minIelts = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "U1", "University 1", "KY", ["Computer Science"], avg, p25, p75, tuition, 10000, 1000,
            MinIelts: minIelts);

    private static StudentSnapshot Student(decimal? gpa = 2.75m, int? sat = 1300, decimal budget = 60000, decimal? ec = 1.69m, decimal? ielts = 6.5m) =>
        new("undergraduate", "Computer Science", gpa, sat, budget, [], ec, Ielts: ielts);

    private static ReasonParts Parts(StudentSnapshot s, SchoolCandidate c) =>
        ReasonBreakdown.Build(s, RecommendationScorer.Score(s, c, Cfg), Cfg.GpaBand);

    [Fact]
    public void GpaLowSatHigh_ReachExplainsLowerCriterionRule()
    {
        // Giống University of Kentucky: SAT cao hơn mốc 75% nhưng GPA thấp hơn trung bình 0.83 → thử sức vì GPA
        var p = Parts(Student(), School(3.58m, 1110, 1270, minIelts: 6.0m));

        Assert.StartsWith("Thử sức — vì GPA 2.75 thấp hơn GPA trung bình 3.58", p.CategoryReason);
        Assert.Contains("lấy mức thấp hơn", p.CategoryReason);
        Assert.Contains("SAT 1300 từ mốc 75% (1270) trở lên", p.Strengths);
        Assert.Contains("Tiếng Anh đạt yêu cầu (IELTS 6.5 / tối thiểu 6.0)", p.Strengths);
        Assert.Contains("Chi phí $51,000/năm nằm trong ngân sách $60,000", p.Strengths);
        Assert.Contains("GPA 2.75 thấp hơn GPA trung bình 3.58 của trường 0.83 điểm", p.Weaknesses);
        Assert.Contains("Ngoại khóa còn mỏng (1.69/4)", p.Weaknesses);
    }

    [Fact]
    public void OnlySatPublished_MatchReasonUsesSatWithoutLowerRule()
    {
        var p = Parts(Student(), School(null, 1100, 1310));

        Assert.Equal("Vừa sức — vì SAT 1300 nằm trong khoảng 25–75% (1100–1310).", p.CategoryReason);
        Assert.DoesNotContain(p.Strengths, x => x.StartsWith("GPA"));
    }

    [Fact]
    public void BothCriteriaSameLevel_MentionsBoth()
    {
        var p = Parts(Student(gpa: 3.9m), School(3.4m, 1000, 1200));
        Assert.Equal("An toàn — vì GPA 3.90 cao hơn GPA trung bình 3.40 từ 0.30 điểm trở lên và SAT 1300 từ mốc 75% (1200) trở lên.", p.CategoryReason);
    }

    [Fact]
    public void Weaknesses_EnglishBelowMinCostOverBudgetNoScore()
    {
        var p = Parts(Student(budget: 50000, ielts: 6.0m), School(3.5m, 1250, 1400, tuition: 43000, minIelts: 6.5m));   // tổng 54k

        Assert.Contains("Tiếng Anh chưa đạt mức tối thiểu (IELTS 6.0 / tối thiểu 6.5)", p.Weaknesses);
        Assert.Contains("Chi phí $54,000/năm vượt ngân sách $50,000 khoảng $4,000", p.Weaknesses);

        var noEnglish = Parts(Student(ielts: null), School(3.5m, 1250, 1400));
        Assert.Contains("Chưa có điểm tiếng Anh (IELTS/TOEFL/Duolingo)", noEnglish.Weaknesses);
    }

    [Fact]
    public void StrongExtracurricular_IsStrength_UnknownCost_IsWeakness()
    {
        var p = Parts(Student(ec: 3.2m), School(3.5m, 1250, 1400, tuition: null));
        Assert.Contains("Ngoại khóa mạnh (3.2/4)", p.Strengths);
        Assert.Contains("Trường chưa công bố chi phí, cần hỏi lại trường", p.Weaknesses);
    }

    [Fact]
    public void SchoolWithoutAcademicData_IsInsufficient()
    {
        var p = Parts(Student(), School(null, null, null));
        Assert.StartsWith("Chưa đủ dữ liệu — trường chưa công bố", p.CategoryReason);
    }

    [Fact]
    public void CommunityCollegeWithoutData_IsOpenAdmission()
    {
        var p = Parts(Student() with { StudyLevel = "community_college" }, School(null, null, null));
        Assert.StartsWith("An toàn — trường tuyển sinh mở", p.CategoryReason);
    }
}
