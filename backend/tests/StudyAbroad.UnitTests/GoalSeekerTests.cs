using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Goal-seeking: tìm mức tối thiểu của SAT, GPA, bậc ngoại khóa để một trường đạt nhóm đích.
// Dùng chính SchoolGrouper.Grade, nên test chủ yếu kiểm hai điều: mức tìm được thật sự đạt nhóm đích, và là mức nhỏ nhất.
public class GoalSeekerTests
{
    // Trường coi trọng GPA, điểm thi (vừa phải) và ngoại khóa (rất), GPA trung bình 3,5, SAT 1200–1380, nhận 60%.
    private static SchoolAcademics School(
        decimal? rate = 0.6m, decimal? intl = null, string ec = "very_important", string policy = "optional") => new(
        3.5m, null, 1200, 1380, null, null, policy, null, "important", "important", ec, rate, intl);

    private static StudentAcademics Student(decimal? gpa = 3.0m, int? sat = 1000, int ec = 1) => new(gpa, sat, null, ec);

    // Áp các thay đổi của một phương án vào hồ sơ
    private static StudentAcademics Apply(StudentAcademics student, GoalPlan plan)
    {
        foreach (var change in plan.Changes)
        {
            student = change.Factor switch
            {
                GoalFactor.Sat => student with { Sat = (int)change.To },
                GoalFactor.Gpa => student with { Gpa4 = change.To },
                _ => student with { EcTier = (int)change.To },
            };
        }
        return student;
    }

    private static SchoolGroup GroupOf(StudentAcademics student, SchoolAcademics school) => SchoolGrouper.Grade(student, school).Group;

    private static GoalPlan AloneOf(GoalSeekResult result, GoalFactor factor) =>
        result.Alone.Single(p => p.Changes.Single().Factor == factor);

    // ---------------------------------------------------------------- đạt được

    [Fact]
    public void AloneSat_IsTheSmallestScoreThatReachesTheTarget()
    {
        var school = School();
        var student = Student();

        var result = GoalSeeker.Seek(student, school, SchoolGroup.Match);

        Assert.Equal(GoalOutcome.Reachable, result.Outcome);
        Assert.Equal(SchoolGroup.Reach, result.Current);
        var sat = (int)AloneOf(result, GoalFactor.Sat).Changes.Single().To;
        Assert.True(GroupOf(student with { Sat = sat }, school) >= SchoolGroup.Match);
        Assert.True(GroupOf(student with { Sat = sat - 10 }, school) < SchoolGroup.Match);   // bước nhỏ hơn thì chưa đạt
        Assert.Equal((decimal?)1000, AloneOf(result, GoalFactor.Sat).Changes.Single().From);
    }

    [Fact]
    public void AloneGpa_IsTheSmallestGpaThatReachesTheTarget()
    {
        var school = School();
        var student = Student();

        var result = GoalSeeker.Seek(student, school, SchoolGroup.Match);

        var gpa = AloneOf(result, GoalFactor.Gpa).Changes.Single().To;
        Assert.True(GroupOf(student with { Gpa4 = gpa }, school) >= SchoolGroup.Match);
        Assert.True(GroupOf(student with { Gpa4 = gpa - 0.05m }, school) < SchoolGroup.Match);
    }

    [Fact]
    public void AFactorThatCannotReachTheTargetAlone_IsNotListedAlone()
    {
        // Ngoại khóa tối đa chỉ cộng 0,10 vào p', không đủ để từ Khó vào lên Vừa sức
        var result = GoalSeeker.Seek(Student(), School(), SchoolGroup.Match);

        Assert.DoesNotContain(result.Alone, p => p.Changes.Single().Factor == GoalFactor.EcTier);
    }

    [Fact]
    public void Cheapest_ReachesTheTarget_AndCostsNoMoreThanAnyFactorAlone()
    {
        var school = School();
        var student = Student();

        var result = GoalSeeker.Seek(student, school, SchoolGroup.Match);

        var cheapest = result.Cheapest!;
        Assert.True(GroupOf(Apply(student, cheapest), school) >= SchoolGroup.Match);
        Assert.All(result.Alone, p => Assert.True(cheapest.Effort <= p.Effort));
        Assert.Equal(GoalSeeker.Effort(cheapest.Changes, student), cheapest.Effort);
        Assert.Equal(GoalSeeker.DifficultyOf(cheapest.Effort), cheapest.Difficulty);
    }

    [Fact]
    public void WhenNoFactorWorksAlone_TheCheapestIsACombination()
    {
        // An toàn cần cả GPA lẫn SAT đủ cao (chỉ số yếu hơn 0,25 thì chỉ Vừa sức), nên một yếu tố riêng không đủ
        var school = School();
        var student = Student();

        var result = GoalSeeker.Seek(student, school, SchoolGroup.Safety);

        Assert.Equal(GoalOutcome.Reachable, result.Outcome);
        Assert.Empty(result.Alone);
        Assert.True(result.Cheapest!.Changes.Count >= 2);
        Assert.Equal(SchoolGroup.Safety, GroupOf(Apply(student, result.Cheapest), school));
    }

    [Fact]
    public void ResultsAreRepeatable()
    {
        var first = GoalSeeker.Seek(Student(), School(), SchoolGroup.Safety);
        var second = GoalSeeker.Seek(Student(), School(), SchoolGroup.Safety);

        Assert.Equal(first.Cheapest!.Changes, second.Cheapest!.Changes);
        Assert.Equal(first.Cheapest.Effort, second.Cheapest.Effort);
    }

    // ---------------------------------------------------------------- đã đạt

    [Theory]
    [InlineData(SchoolGroup.Reach)]
    [InlineData(SchoolGroup.Match)]
    public void AStudentAlreadyInTheTargetGroup_NeedsNothing(SchoolGroup target)
    {
        var result = GoalSeeker.Seek(Student(3.5m, 1290, 3), School(), target);   // p = 0,5: Vừa sức

        Assert.Equal(GoalOutcome.AlreadyReached, result.Outcome);
        Assert.Equal(SchoolGroup.Match, result.Current);
        Assert.Empty(result.Alone);
        Assert.Null(result.Cheapest);
    }

    // ---------------------------------------------------------------- bị chặn bởi luật

    [Fact]
    public void ASchoolAcceptingUnder15Percent_IsAlwaysReach_NoScoreChangesIt()
    {
        var result = GoalSeeker.Seek(Student(), School(rate: 0.10m), SchoolGroup.Match);

        Assert.Equal(GoalOutcome.Blocked, result.Outcome);
        Assert.Equal(SchoolGroup.Reach, result.Best);
        Assert.Contains(GroupReasons.LowAcceptRate, result.Reasons);
        Assert.Empty(result.Alone);
        Assert.Null(result.Cheapest);
    }

    [Fact]
    public void ASchoolAcceptingUnderHalf_CanNeverBeSafety_ButMatchIsReachable()
    {
        var school = School(rate: 0.40m);
        var student = Student(4.0m, 1600, 5);   // đã tối đa mọi yếu tố

        var toSafety = GoalSeeker.Seek(student, school, SchoolGroup.Safety);
        var toMatch = GoalSeeker.Seek(Student(), school, SchoolGroup.Match);

        Assert.Equal(GoalOutcome.Blocked, toSafety.Outcome);
        Assert.Equal(SchoolGroup.Match, toSafety.Best);
        Assert.Contains(GroupReasons.CappedSelective, toSafety.Reasons);
        Assert.Equal(GoalOutcome.Reachable, toMatch.Outcome);
    }

    [Fact]
    public void TheInternationalDowngrade_IsPartOfWhatTheStudentNeeds()
    {
        // Du học sinh khó vào hơn nhiều so với tỉ lệ chung: Vừa sức cần điểm của An toàn rồi bị hạ một bậc
        var school = School(rate: 0.60m, intl: 0.20m);
        var student = Student();

        var toMatch = GoalSeeker.Seek(student, school, SchoolGroup.Match);
        var toSafety = GoalSeeker.Seek(student, school, SchoolGroup.Safety);

        Assert.Equal(GoalOutcome.Reachable, toMatch.Outcome);
        var applied = SchoolGrouper.Grade(Apply(student, toMatch.Cheapest!), school);
        Assert.Equal(SchoolGroup.Match, applied.Group);
        Assert.Contains(GroupReasons.IntlDowngrade, applied.Reasons);
        Assert.Equal(GoalOutcome.Blocked, toSafety.Outcome);   // điểm cao đến đâu cũng chỉ tới Vừa sức
        Assert.Contains(GroupReasons.IntlDowngrade, toSafety.Reasons);
    }

    // ---------------------------------------------------------------- thiếu dữ liệu

    [Fact]
    public void AStudentWithoutATestAtASchoolRequiringIt_NeedsATestFirst()
    {
        var school = School(policy: "required");
        var student = Student(3.5m, sat: null, ec: 3);

        var result = GoalSeeker.Seek(student, school, SchoolGroup.Match);

        Assert.Equal(SchoolGroup.NoData, result.Current);
        Assert.Equal(GoalOutcome.Reachable, result.Outcome);
        Assert.Contains(GroupReasons.NeedTest, result.Reasons);
        var sat = AloneOf(result, GoalFactor.Sat).Changes.Single();
        Assert.Null(sat.From);
        Assert.True(GroupOf(Apply(student, AloneOf(result, GoalFactor.Sat)), school) >= SchoolGroup.Match);
        Assert.DoesNotContain(result.Alone, p => p.Changes.Single().Factor == GoalFactor.Gpa);   // chưa có điểm thi thì GPA không đủ
    }

    [Fact]
    public void ASchoolWithoutAcademicData_CannotBeSought()
    {
        var empty = new SchoolAcademics(null, null, null, null, null, null, "optional", null, null, null, null, 0.5m, null);

        var result = GoalSeeker.Seek(Student(), empty, SchoolGroup.Match);

        Assert.Equal(GoalOutcome.NoData, result.Outcome);
        Assert.Contains(GroupReasons.NoAcademicData, result.Reasons);
        Assert.Null(result.Cheapest);
    }

    // ---------------------------------------------------------------- yếu tố không có tác dụng

    [Theory]
    [InlineData("not_considered")]
    [InlineData(null)]
    public void ASchoolThatIgnoresExtracurriculars_ReportsNoEffect_AndNeverAsksForMore(string? ec)
    {
        var result = GoalSeeker.Seek(Student(), School(ec: ec!), SchoolGroup.Match);

        Assert.Contains(GoalFactor.EcTier, result.NoEffect);
        Assert.DoesNotContain(result.Cheapest!.Changes, c => c.Factor == GoalFactor.EcTier);
    }

    [Fact]
    public void ASchoolWithoutSatData_ReportsSatHasNoEffect()
    {
        var gpaOnly = new SchoolAcademics(3.5m, null, null, null, null, null, "optional", null, null, null, "very_important", 0.6m, null);

        var result = GoalSeeker.Seek(Student(), gpaOnly, SchoolGroup.Match);

        Assert.Contains(GoalFactor.Sat, result.NoEffect);
        Assert.Equal([GoalFactor.Gpa], result.Alone.Select(p => p.Changes.Single().Factor));
    }

    [Fact]
    public void AFactorAlreadyAtItsCeiling_IsNotReportedAsNoEffect()
    {
        var result = GoalSeeker.Seek(Student(3.0m, 1000, 5), School(), SchoolGroup.Match);

        Assert.DoesNotContain(GoalFactor.EcTier, result.NoEffect);   // đã bậc 5: không còn gì để tăng, không phải không tác dụng
    }

    // ---------------------------------------------------------------- công sức và nhãn

    [Fact]
    public void Effort_CountsSatHundredGpaTenthAndOneTierAsOneUnitEach()
    {
        var student = Student(3.5m, 1300, 2);

        Assert.Equal(1.0, GoalSeeker.Effort([new GoalChange(GoalFactor.Sat, 1300, 1400)], student));
        Assert.Equal(1.0, GoalSeeker.Effort([new GoalChange(GoalFactor.Gpa, 3.5m, 3.6m)], student));
        Assert.Equal(1.0, GoalSeeker.Effort([new GoalChange(GoalFactor.EcTier, 2, 3)], student));
        Assert.Equal(3.0, GoalSeeker.Effort(
            [new GoalChange(GoalFactor.Sat, 1300, 1400), new GoalChange(GoalFactor.Gpa, 3.5m, 3.6m), new GoalChange(GoalFactor.EcTier, 2, 3)], student));
    }

    [Fact]
    public void Effort_StartsFromAnAssumedBaselineWhenTheProfileHasNoScore()
    {
        var student = Student(null, null, 3);

        Assert.Equal(1.0, GoalSeeker.Effort([new GoalChange(GoalFactor.Sat, null, 1100)], student));   // mốc 1000
        Assert.Equal(1.0, GoalSeeker.Effort([new GoalChange(GoalFactor.Gpa, null, 3.1m)], student));   // mốc 3,0
    }

    [Theory]
    [InlineData(0.5, GoalDifficulty.Easy)]
    [InlineData(1.0, GoalDifficulty.Easy)]
    [InlineData(1.01, GoalDifficulty.Hard)]
    [InlineData(2.5, GoalDifficulty.Hard)]
    [InlineData(2.51, GoalDifficulty.VeryHard)]
    public void Difficulty_FollowsTheEffortThresholds(double effort, GoalDifficulty expected)
    {
        Assert.Equal(expected, GoalSeeker.DifficultyOf(effort));
    }

    // ---------------------------------------------------------------- đầu vào

    [Fact]
    public void TheTargetCannotBeNoData()
    {
        Assert.Throws<ArgumentException>(() => GoalSeeker.Seek(Student(), School(), SchoolGroup.NoData));
    }
}
