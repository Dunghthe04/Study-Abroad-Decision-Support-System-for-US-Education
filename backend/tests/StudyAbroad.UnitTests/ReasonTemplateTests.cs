using System.Text.RegularExpressions;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class ReasonTemplateTests
{
    private static readonly RecommendSettings Cfg = new();
    private static readonly StudentSnapshot Student =
        new("undergraduate", "Computer Science", 3.5m, 1300, 50000, [], null);

    // Giống DEMO_UB: GPA TB 3.5, SAT 1250–1400, tổng chi phí 50,000
    private static readonly SchoolCandidate B = new(Guid.NewGuid(), Guid.NewGuid(), "DEMO_UB", "Demo B", "NY",
        ["Computer Science"], 3.50m, 1250, 1400, 35000, 14000, 1000);

    private static string Reason(StudentSnapshot s, SchoolCandidate c) =>
        ReasonTemplate.Build(s, RecommendationScorer.Score(s, c, Cfg));

    [Fact]
    public void Build_AllNumbersComeFromData()          // tiêu chí: mọi con số khớp dữ liệu
    {
        var reason = Reason(Student, B);
        string[] allowed = ["3.50", "1300", "1250", "1400", "50,000"];
        foreach (Match m in Regex.Matches(reason, @"\d[\d,.]*"))
            Assert.Contains(m.Value.TrimEnd('.', ','), allowed);
        Assert.Contains("Vừa sức (Match)", reason);
    }

    [Fact]
    public void Build_SafetySchool()
    {
        var c = B with { AvgGpa4 = 3.10m, Sat25 = 1050, Sat75 = 1200 };
        Assert.Contains("An toàn (Safety)", Reason(Student, c));
    }

    [Fact]
    public void Build_ReachSchool()
    {
        var c = B with { AvgGpa4 = 3.90m, Sat25 = 1450, Sat75 = 1560 };
        Assert.Contains("Thử thách (Reach)", Reason(Student, c));
    }

    [Fact]
    public void Build_OnlySat_DoesNotMentionGpa()
    {
        var reason = Reason(Student with { Gpa4 = null }, B);
        Assert.DoesNotContain("GPA", reason);
        Assert.Contains("SAT 1300", reason);
    }

    [Fact]
    public void Build_UnknownCost_DoesNotInventNumbers()
    {
        var reason = Reason(Student, B with { TuitionUsd = null });
        Assert.Contains("Chưa có dữ liệu chi phí", reason);
        Assert.DoesNotContain("$", reason);
    }

    [Fact]
    public void Build_NoBudget_MentionsOnlyCost()
    {
        var reason = Reason(Student with { AnnualBudgetUsd = null }, B);
        Assert.Contains("$50,000/năm", reason);
        Assert.DoesNotContain("ngân sách của bạn", reason);
    }

    [Fact]
    public void Build_EnglishBelowMinimum_MentionsRequirement()
    {
        var reason = Reason(Student with { Ielts = 6.0m }, B with { MinIelts = 6.5m, MinToefl = 80m });
        Assert.Contains("IELTS ≥ 6.5", reason);
        Assert.Contains("TOEFL ≥ 80", reason);
    }

    [Fact]
    public void Build_EnglishMet_NoWarning()
    {
        var reason = Reason(Student with { Ielts = 7.0m }, B with { MinIelts = 6.5m });
        Assert.DoesNotContain("tiếng Anh", reason);
    }

    [Fact]
    public void Build_OpenAdmission()
    {
        var cc = B with { AvgGpa4 = null, Sat25 = null, Sat75 = null };
        var reason = Reason(Student with { StudyLevel = "community_college" }, cc);
        Assert.Contains("tuyển sinh mở", reason);
    }

    [Fact]
    public void Build_InsufficientData()
    {
        var reason = Reason(Student, B with { AvgGpa4 = null, Sat25 = null, Sat75 = null });
        Assert.Contains("chưa công bố đủ GPA/SAT", reason);
    }

    [Theory]
    [InlineData(AdmissionCategory.Reach, "reach")]
    [InlineData(AdmissionCategory.Match, "match")]
    [InlineData(AdmissionCategory.Safety, "safety")]
    [InlineData(AdmissionCategory.InsufficientData, "insufficient_data")]
    public void CategoryCode(AdmissionCategory category, string expected) =>
        Assert.Equal(expected, ReasonTemplate.CategoryCode(category));

    [Theory]
    [InlineData(EnglishStatus.Met, "met")]
    [InlineData(EnglishStatus.BelowMin, "below_min")]
    [InlineData(EnglishStatus.NoScore, "no_score")]
    [InlineData(EnglishStatus.Unknown, "unknown")]
    public void EnglishCode(EnglishStatus status, string expected) =>
        Assert.Equal(expected, ReasonTemplate.EnglishCode(status));
}
