using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bảng "bạn so với trường" của goal-seeking: mỗi tiêu chí bộ chấm dùng đặt cạnh số của trường, nói rõ thiếu, đủ hay vượt,
// không đoán khi thiếu số, và dùng đúng cùng luật hạ nhóm với bộ chấm.
public class GoalComparisonTests
{
    private static SchoolAcademics School(
        decimal? gpa = 3.9m, int? sat25 = 1520, int? sat75 = 1580, int? act25 = null, int? act75 = null,
        decimal? accept = 0.6m, decimal? intl = 0.5m, string? ec = "considered", IReadOnlyList<decimal>? dist = null) =>
        new(gpa, dist, sat25, sat75, act25, act75, null, null, "considered", "considered", ec, accept, intl);

    private static IReadOnlyList<GoalCompareRow> Rows(StudentAcademics student, SchoolAcademics school) =>
        GoalComparison.Rows(student, school, SchoolGrouper.Grade(student, school));

    private static GoalCompareRow Row(IReadOnlyList<GoalCompareRow> rows, string factor) =>
        rows.Single(r => r.Factor == factor);

    // ---------------------------------------------------------------- điểm thi

    [Fact]
    public void SatBelowTheSchoolRange_ShowsHowManyPointsBelowTheLowEnd()
    {
        var sat = Row(Rows(new StudentAcademics(3.6m, 1400, null), School()), "sat");

        Assert.Equal("below", sat.Status);
        Assert.Equal((1400, 1520, 1580, -120), (sat.Student, sat.Low, sat.High, sat.Gap));
    }

    [Theory]
    [InlineData(1520, "within", 0)]   // đúng mốc dưới
    [InlineData(1580, "within", 0)]   // đúng mốc trên
    [InlineData(1550, "within", 0)]
    [InlineData(1600, "above", 20)]
    public void SatInsideOrAboveTheRange(int sat, string status, int gap)
    {
        var row = Row(Rows(new StudentAcademics(3.6m, sat, null), School()), "sat");

        Assert.Equal(status, row.Status);
        Assert.Equal(gap, row.Gap);
    }

    [Fact]
    public void SatPosition_IsTheSameNumberTheGrouperUses()
    {
        // SAT 1300 so với 1060-1250: cao hơn khoảng 85% sinh viên của trường (số kiểm tay: 0,848)
        var sat = Row(Rows(new StudentAcademics(3.7m, 1300, null), School(sat25: 1060, sat75: 1250)), "sat");

        Assert.Equal(0.848, sat.Position!.Value, 3);
        Assert.Equal(AcademicPosition.FromQuartiles(1300, 1060, 1250), sat.Position!.Value, 3);
    }

    [Fact]
    public void ActIsComparedOnlyWhenTheStudentHasNoSatAndTheSchoolPublishesAct()
    {
        var onlyAct = Rows(new StudentAcademics(3.6m, null, 30), School(act25: 33, act75: 35));
        var both = Rows(new StudentAcademics(3.6m, 1400, 30), School(act25: 33, act75: 35));

        Assert.Equal(-3, Row(onlyAct, "act").Gap);
        Assert.DoesNotContain(onlyAct, r => r.Factor == "sat");
        Assert.Contains(both, r => r.Factor == "sat");
        Assert.DoesNotContain(both, r => r.Factor == "act");
    }

    // ---------------------------------------------------------------- GPA

    [Fact]
    public void GpaBelowTheAverage_ShowsTheShortfall()
    {
        var gpa = Row(Rows(new StudentAcademics(3.6m, 1400, null), School()), "gpa");

        Assert.Equal("below", gpa.Status);
        Assert.Equal(-0.3m, gpa.Gap);
    }

    [Theory]
    [InlineData(3.86, "within")]   // lệch trong 0,05 coi là ngang trung bình
    [InlineData(3.95, "within")]
    [InlineData(4.0, "above")]
    public void GpaNearTheAverageCountsAsLevel(double gpa, string status)
    {
        var row = Row(Rows(new StudentAcademics((decimal)gpa, 1400, null), School()), "gpa");

        Assert.Equal(status, row.Status);
    }

    [Fact]
    public void GpaPosition_UsesTheSchoolDistributionNotJustTheAverage()
    {
        // Trường có 35,5% sinh viên đúng 4,0: GPA 3,70 chỉ hơn khoảng 38% sinh viên dù chỉ thấp hơn trung bình 0,06 (số kiểm tay: 0,384)
        decimal[] dist = [0.0090m, 0.0584m, 0.0772m, 0.1078m, 0.1644m, 0.2284m, 0.3550m];

        var gpa = Row(Rows(new StudentAcademics(3.70m, 1300, null), School(gpa: 3.76m, dist: dist)), "gpa");

        Assert.Equal("below", gpa.Status);
        Assert.Equal(-0.06m, gpa.Gap);
        Assert.Equal(0.384, gpa.Position!.Value, 3);
    }

    // ---------------------------------------------------------------- ngoại khóa

    [Theory]
    [InlineData(1, "below", -2)]
    [InlineData(3, "within", 0)]
    [InlineData(5, "above", 2)]
    public void ActivitiesAreComparedWithTheNeutralTier(int tier, string status, int gap)
    {
        var ec = Row(Rows(new StudentAcademics(3.6m, 1400, null, tier), School()), "ec");

        Assert.Equal((status, gap, "considered"), (ec.Status, (int)ec.Gap!.Value, ec.Importance));
    }

    [Fact]
    public void ASchoolThatDoesNotConsiderActivities_IsUnknownSoTheTierChangesNothing()
    {
        var ec = Row(Rows(new StudentAcademics(3.6m, 1400, null, 1), School(ec: "not_considered")), "ec");

        Assert.Equal("unknown", ec.Status);
    }

    // ---------------------------------------------------------------- tỉ lệ nhận

    [Fact]
    public void FewInternationalAdmits_IsCappedBecauseTheLawLooksAtTheInternationalRate()
    {
        // Oklahoma State: nhận chung 73,7%, du học sinh 18,4%: luật tỉ lệ nhận xét tỉ lệ du học sinh (UC-22), dưới 50% nên không bao giờ là An toàn
        var student = new StudentAcademics(3.7m, 1300, null);
        var school = School(sat25: 1060, sat75: 1250, accept: 0.737m, intl: 0.184m);

        var accept = Row(Rows(student, school), "accept");

        Assert.Equal("capped", accept.Status);
        Assert.Equal((0.737m, 0.184m), (accept.Low, accept.High));
        Assert.Equal(0.25m, Math.Round(accept.Gap!.Value, 2));
    }

    [Fact]
    public void TheAcceptRateRule_IsAPropertyOfTheSchool_SoItIsMarkedEvenForAWeakStudent()
    {
        var student = new StudentAcademics(2.5m, 900, null);
        var school = School(sat25: 1060, sat75: 1250, accept: 0.737m, intl: 0.184m);

        Assert.Equal("capped", Row(Rows(student, school), "accept").Status);
    }

    [Fact]
    public void ALowOverallRate_IsNotCappedWhenTheInternationalRateIsHighEnough()
    {
        // Chung 20% sẽ chặn An toàn, nhưng du học sinh nhận 60%: luật xét tỉ lệ du học sinh nên không chặn
        var accept = Row(Rows(new StudentAcademics(3.7m, 1300, null), School(accept: 0.20m, intl: 0.60m)), "accept");

        Assert.Equal("ok", accept.Status);
    }

    [Fact]
    public void AVeryLowAcceptRate_IsMarkedAlwaysReach()
    {
        var accept = Row(Rows(new StudentAcademics(4.0m, 1600, null), School(accept: 0.046m, intl: 0.03m)), "accept");

        Assert.Equal("always_reach", accept.Status);
    }

    [Fact]
    public void ASchoolAcceptingUnderHalf_IsMarkedCappedBecauseItCanNeverBeSafety()
    {
        // Cal State Long Beach: nhận chung 19,4%, du học sinh 42,8% (không bị hạ nhóm) nhưng dưới 50% nên không bao giờ là An toàn
        var student = new StudentAcademics(3.7m, 1300, null);
        var school = School(gpa: 3.66m, sat25: 1220, sat75: 1420, accept: 0.194m, intl: 0.428m);

        var accept = Row(Rows(student, school), "accept");

        Assert.Equal("capped", accept.Status);
        Assert.Equal((0.194m, 0.428m), (accept.Low, accept.High));
    }

    [Fact]
    public void ASchoolAcceptingHalfOrMore_IsNotCapped()
    {
        var accept = Row(Rows(new StudentAcademics(3.7m, 1300, null), School(accept: 0.5m, intl: 0.55m)), "accept");

        Assert.Equal("ok", accept.Status);
    }

    [Fact]
    public void ASchoolWithoutAnAcceptRate_IsUnknown()
    {
        var accept = Row(Rows(new StudentAcademics(3.6m, 1400, null), School(accept: null, intl: null)), "accept");

        Assert.Equal("unknown", accept.Status);
        Assert.Null(accept.Gap);
    }

    // ---------------------------------------------------------------- thiếu số

    [Fact]
    public void ASchoolThatDoesNotPublishTheNumber_IsUnknownNotGuessed()
    {
        var rows = Rows(new StudentAcademics(3.6m, 1400, null), School(gpa: null, sat25: null, sat75: null));

        Assert.Equal("unknown", Row(rows, "sat").Status);
        Assert.Equal("unknown", Row(rows, "gpa").Status);
        Assert.Null(Row(rows, "sat").Gap);
        Assert.Null(Row(rows, "gpa").Gap);
    }

    [Fact]
    public void AStudentWithoutTheNumber_IsNoProfileNotZero()
    {
        var rows = Rows(new StudentAcademics(null, null, null), School());

        Assert.Equal("no_profile", Row(rows, "sat").Status);
        Assert.Equal("no_profile", Row(rows, "gpa").Status);
        Assert.Null(Row(rows, "sat").Student);
        Assert.Null(Row(rows, "gpa").Student);
    }
}
