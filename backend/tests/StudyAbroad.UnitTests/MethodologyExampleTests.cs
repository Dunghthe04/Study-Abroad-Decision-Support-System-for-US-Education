using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Các ví dụ có số trên trang /methodology (frontend/src/app/methodology/page.tsx) phải khớp công thức thật.
// Test này khoá các con số đó: đổi công thức xếp nhóm thì test đỏ, và trang phải sửa theo.
public class MethodologyExampleTests
{
    // Trường mẫu: GPA trung bình 3,6 · SAT 1100–1350 · GPA và điểm thi cùng "quan trọng" · ngoại khoá bậc trung bình · nhận 55%
    private static SchoolAcademics School(decimal acceptanceRate) => new(
        AvgGpa4: 3.6m, GpaDist: null,
        Sat25: 1100, Sat75: 1350, Act25: null, Act75: null,
        SatPolicy: "optional", SatSubmitPct: 0.7m,
        GpaImportance: "important", TestImportance: "important", EcImportance: "considered",
        AcceptanceRate: acceptanceRate, IntlAcceptanceRate: acceptanceRate);

    private static SchoolGrade Grade(decimal gpa, int sat, decimal acceptanceRate = 0.55m) =>
        SchoolGrouper.Grade(new StudentAcademics(gpa, sat, null), School(acceptanceRate));

    [Fact]
    public void Example_ProfileA_IsMatch_WithThePositionsShownOnThePage()
    {
        var grade = Grade(3.5m, 1250);

        Assert.Equal(SchoolGroup.Match, grade.Group);
        Assert.Equal(0.37, grade.Pg!.Value, 2);
        Assert.Equal(0.55, grade.Ps!.Value, 2);
        Assert.Equal(0.46, grade.PAdjusted!.Value, 2);
    }

    [Fact]
    public void Example_ProfileB_IsReach_WithThePositionsShownOnThePage()
    {
        var grade = Grade(3.2m, 1150);

        Assert.Equal(SchoolGroup.Reach, grade.Group);
        Assert.Equal(0.09, grade.Pg!.Value, 2);
        Assert.Equal(0.34, grade.Ps!.Value, 2);
        Assert.Equal(0.22, grade.PAdjusted!.Value, 2);
    }

    [Fact]
    public void Example_ProfileC_IsSafety_WithThePositionsShownOnThePage()
    {
        var grade = Grade(3.9m, 1400);

        Assert.Equal(SchoolGroup.Safety, grade.Group);
        Assert.Equal(0.84, grade.Pg!.Value, 2);
        Assert.Equal(0.83, grade.Ps!.Value, 2);
        Assert.Equal(0.83, grade.PAdjusted!.Value, 2);
    }

    [Fact]
    public void Example_ProfileC_AtASchoolAcceptingFortyPercent_IsCappedToMatch()
    {
        var grade = Grade(3.9m, 1400, acceptanceRate: 0.40m);

        Assert.Equal(SchoolGroup.Match, grade.Group);
        Assert.Contains(GroupReasons.CappedSelective, grade.Reasons);
    }

    [Fact]
    public void Example_ProfileC_AtASchoolAcceptingTwelvePercent_IsAlwaysReach()
    {
        var grade = Grade(3.9m, 1400, acceptanceRate: 0.12m);

        Assert.Equal(SchoolGroup.Reach, grade.Group);
        Assert.Contains(GroupReasons.LowAcceptRate, grade.Reasons);
    }

    [Fact]
    public void PageConstants_MatchTheCode()
    {
        var options = new GroupingOptions();
        Assert.Equal(0.25, options.LowerCut);          // "Dưới 25% là Khó vào"
        Assert.Equal(0.75, options.UpperCut);          // "từ 75% là An toàn hơn"
        Assert.Equal(0.15, options.AlwaysReachRate);   // "nhận dưới 15%: luôn Khó vào"
        Assert.Equal(0.50, options.SafetyMinRate);     // "tỉ lệ nhận dưới 50%: không được An toàn"
        Assert.Equal(0.5, options.IntlRatioCut);       // "thấp hơn một nửa tỉ lệ chung: hạ một nhóm"
        Assert.Equal(0.5, options.LowSubmitPct);       // "dưới 50% sinh viên nộp SAT"
        Assert.Equal(0.5, options.LowSubmitFactor);    // "trọng số điểm thi giảm một nửa"
        Assert.Equal(0.05, options.EcStep);            // "mỗi bậc lệch 5 điểm phần trăm"
        Assert.Equal(0.10, options.EcCap);             // "tối đa 10 điểm phần trăm"
        Assert.Equal(0.3, AcademicPosition.DefaultGpaSigma);   // "độ lệch 0,3"
        Assert.Equal(0.10m, RecommendationService.BudgetTolerance);   // "vượt ngân sách quá 10%"
        Assert.Equal(4, RecommendationService.MaxPerBlock);           // "tối đa 4 trường"

        var fit = new FitWeights();                    // 30 / 25 / 15 / 20 / 10
        Assert.Equal([0.30, 0.25, 0.15, 0.20, 0.10], [fit.Position, fit.Budget, fit.Aid, fit.Quality, fit.English]);
    }
}
