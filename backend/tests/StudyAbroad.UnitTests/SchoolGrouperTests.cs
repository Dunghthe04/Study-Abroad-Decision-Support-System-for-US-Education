using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước 4 của công thức gợi ý trường: xếp nhóm Khó vào / Vừa sức / An toàn từ p' và tỉ lệ nhận
// (CONG_THUC_GOI_Y_TRUONG.docx, mục 6).
public class SchoolGrouperTests
{
    private static GroupDecision Classify(
        double pAdj, double? pg = 0.5, double? ps = 0.5, double? rate = 0.6, double? intl = null,
        GroupingOptions? options = null) =>
        SchoolGrouper.Classify(pAdj, pg, ps, rate, intl, options);

    // ---------------------------------------------------------------- theo p'

    [Theory]
    [InlineData(0.0, SchoolGroup.Reach)]
    [InlineData(0.24, SchoolGroup.Reach)]
    [InlineData(0.25, SchoolGroup.Match)]    // đúng mốc dưới: vào Vừa sức
    [InlineData(0.50, SchoolGroup.Match)]
    [InlineData(0.74, SchoolGroup.Match)]
    [InlineData(0.75, SchoolGroup.Safety)]   // đúng mốc trên: vào An toàn
    [InlineData(1.0, SchoolGroup.Safety)]
    public void ByPAdjusted_QuarterAndThreeQuartersAreTheCuts(double pAdj, SchoolGroup expected)
    {
        Assert.Equal(expected, Classify(pAdj, pg: pAdj, ps: pAdj).Group);
    }

    [Fact]
    public void Reasons_SayWhichRangeDecidedTheGroup()
    {
        Assert.Contains(GroupReasons.BelowRange, Classify(0.1, 0.1, 0.1).Reasons);
        Assert.Contains(GroupReasons.InRange, Classify(0.5).Reasons);
        Assert.Contains(GroupReasons.AboveRange, Classify(0.9, 0.9, 0.9).Reasons);
    }

    // ---------------------------------------------------------------- tỉ lệ nhận

    [Fact]
    public void VerySelectiveSchool_IsAlwaysReach_EvenWithTopScores()
    {
        var d = Classify(0.95, 0.95, 0.95, rate: 0.10);

        Assert.Equal(SchoolGroup.Reach, d.Group);
        Assert.Contains(GroupReasons.LowAcceptRate, d.Reasons);
    }

    [Fact]
    public void AcceptanceRateExactlyAtTheLimit_IsNotForcedToReach()
    {
        Assert.Equal(SchoolGroup.Match, Classify(0.5, rate: 0.15).Group);
    }

    [Fact]
    public void SchoolAcceptingUnderHalf_IsNeverSafety_OnlyMatch()
    {
        var d = Classify(0.9, 0.9, 0.9, rate: 0.40);

        Assert.Equal(SchoolGroup.Match, d.Group);
        Assert.Contains(GroupReasons.CappedSelective, d.Reasons);
    }

    [Fact]
    public void AcceptanceRateExactlyHalf_CanBeSafety()
    {
        Assert.Equal(SchoolGroup.Safety, Classify(0.9, 0.9, 0.9, rate: 0.50).Group);
    }

    [Fact]
    public void WithoutAcceptanceRate_RateRulesAreSkipped_AndFlagged()
    {
        var d = Classify(0.9, 0.9, 0.9, rate: null);

        Assert.Equal(SchoolGroup.Safety, d.Group);
        Assert.Contains(GroupReasons.NoAcceptRate, d.Reasons);
    }

    // ---------------------------------------------------------------- chỉ số yếu

    [Fact]
    public void StrongMetricCannotHideAWeakOne_StaysMatch()
    {
        // p' = 0,78 (≥ 0,75) nhưng GPA chỉ ở 0,20 (dưới 0,25) nên không được An toàn
        var d = Classify(0.78, pg: 0.20, ps: 0.95, rate: 0.70);

        Assert.Equal(SchoolGroup.Match, d.Group);
        Assert.Contains(GroupReasons.CappedWeakMetric, d.Reasons);
    }

    [Fact]
    public void WeakTestScoreAlsoBlocksSafety()
    {
        Assert.Equal(SchoolGroup.Match, Classify(0.78, pg: 0.95, ps: 0.20, rate: 0.70).Group);
    }

    [Fact]
    public void MetricExactlyAtTheLowerCut_IsNotWeak()
    {
        Assert.Equal(SchoolGroup.Safety, Classify(0.8, pg: 0.25, ps: 0.95, rate: 0.70).Group);
    }

    [Fact]
    public void MissingMetric_IsNotTreatedAsWeak()
    {
        Assert.Equal(SchoolGroup.Safety, Classify(0.9, pg: null, ps: 0.9, rate: 0.70).Group);
        Assert.Equal(SchoolGroup.Safety, Classify(0.9, pg: 0.9, ps: null, rate: 0.70).Group);
    }

    // ---------------------------------------------------------------- hạ nhóm du học sinh

    [Fact]
    public void IntlAcceptanceFarBelowOverall_DropsMatchToReach()
    {
        // r_qt / r = 0,08 / 0,20 = 0,4 < 0,5
        var d = Classify(0.5, rate: 0.20, intl: 0.08);

        Assert.Equal(SchoolGroup.Reach, d.Group);
        Assert.Contains(GroupReasons.IntlDowngrade, d.Reasons);
    }

    [Fact]
    public void IntlAcceptanceFarBelowOverall_DropsSafetyToMatch()
    {
        Assert.Equal(SchoolGroup.Match, Classify(0.9, 0.9, 0.9, rate: 0.70, intl: 0.20).Group);
    }

    [Fact]
    public void IntlRatioExactlyHalf_DoesNotDrop()
    {
        Assert.Equal(SchoolGroup.Match, Classify(0.5, rate: 0.20, intl: 0.10).Group);
    }

    [Fact]
    public void ReachCannotDropFurther()
    {
        var d = Classify(0.1, 0.1, 0.1, rate: 0.30, intl: 0.01);

        Assert.Equal(SchoolGroup.Reach, d.Group);
        Assert.DoesNotContain(GroupReasons.IntlDowngrade, d.Reasons);
    }

    [Fact]
    public void WithoutIntlRate_ThereIsNoDowngrade()
    {
        Assert.Equal(SchoolGroup.Match, Classify(0.5, rate: 0.20, intl: null).Group);
    }

    [Fact]
    public void ZeroAcceptanceRate_DoesNotDivideByZero()
    {
        var d = Classify(0.5, rate: 0.0, intl: 0.0);

        Assert.Equal(SchoolGroup.Reach, d.Group);   // dưới 15%
    }

    // ---------------------------------------------------------------- ví dụ trong tài liệu

    [Theory]
    [InlineData("P", 0.70, 0.90, 0.80, 0.60, 0.54, SchoolGroup.Safety)]
    [InlineData("Q", 0.70, 0.90, 0.80, 0.40, 0.36, SchoolGroup.Match)]     // r < 50%
    [InlineData("R", 0.20, 0.95, 0.78, 0.70, 0.63, SchoolGroup.Match)]     // GPA yếu bị SAT che
    [InlineData("S", 0.50, 0.50, 0.50, 0.20, 0.08, SchoolGroup.Reach)]     // r_qt / r = 0,4: hạ nhóm
    [InlineData("T", 0.90, 0.90, 0.90, 0.10, 0.09, SchoolGroup.Reach)]     // r < 15%
    public void WorkedExamples_FromTheDocument(
        string school, double pg, double ps, double pAdj, double rate, double intl, SchoolGroup expected)
    {
        Assert.Equal(expected, Classify(pAdj, pg, ps, rate, intl).Group);
    }

    // ---------------------------------------------------------------- ổn định và tham số

    [Fact]
    public void SameInput_SameDecision()
    {
        var a = Classify(0.78, 0.2, 0.95, 0.7, 0.5);
        var b = Classify(0.78, 0.2, 0.95, 0.7, 0.5);

        Assert.Equal(a.Group, b.Group);
        Assert.Equal(a.Reasons, b.Reasons);
    }

    [Fact]
    public void RaisingPAdjusted_NeverLowersTheGroup()
    {
        var previous = SchoolGroup.NoData;
        for (var p = 0.0; p <= 1.0001; p += 0.05)
        {
            var group = Classify(p, pg: p, ps: p, rate: 0.7, intl: 0.6).Group;
            Assert.True(group >= previous, $"p' = {p:F2}: {group} thấp hơn {previous}");
            previous = group;
        }
    }

    [Fact]
    public void Cuts_CanBeChangedThroughOptions()
    {
        var stricter = new GroupingOptions(LowerCut: 0.40, UpperCut: 0.90);

        Assert.Equal(SchoolGroup.Reach, Classify(0.35, 0.35, 0.35, options: stricter).Group);
        Assert.Equal(SchoolGroup.Match, Classify(0.85, 0.85, 0.85, options: stricter).Group);
    }

    [Fact]
    public void RateLimits_CanBeChangedThroughOptions()
    {
        var relaxed = new GroupingOptions(AlwaysReachRate: 0.05, SafetyMinRate: 0.30);

        Assert.Equal(SchoolGroup.Safety, Classify(0.9, 0.9, 0.9, rate: 0.40, options: relaxed).Group);
        Assert.Equal(SchoolGroup.Match, Classify(0.5, rate: 0.10, options: relaxed).Group);
    }

    [Fact]
    public void Group_NeverReturnsNoData_WhenPIsGiven()
    {
        foreach (var p in new[] { 0.0, 0.3, 0.8, 1.0 })
            Assert.NotEqual(SchoolGroup.NoData, Classify(p, p, p).Group);
    }
}
