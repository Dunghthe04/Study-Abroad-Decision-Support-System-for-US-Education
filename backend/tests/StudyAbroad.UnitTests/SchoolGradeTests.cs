using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước ghép: chấm một trường cho một học sinh bằng cả bốn bước (CONG_THUC_GOI_Y_TRUONG.docx, mục 8).
public class SchoolGradeTests
{
    // Trường B trong tài liệu: SAT 1200–1380, phân bố GPA 0 / 5 / 10 / 20 / 30 / 25 / 10 %,
    // coi trọng GPA very_important, điểm thi và ngoại khóa important; nhận 45% (du học sinh 30%).
    private static SchoolAcademics SchoolB() => new(
        null, [0m, 0.05m, 0.10m, 0.20m, 0.30m, 0.25m, 0.10m], 1200, 1380, null, null,
        "optional", null, "very_important", "important", "important", 0.45m, 0.30m);

    // Trường chỉ có SAT 1000–1200 (không có GPA).
    private static SchoolAcademics TestOnly(string? ec = null, string? policy = "optional", decimal? rate = 0.6m) => new(
        null, null, 1000, 1200, null, null, policy, null, null, null, ec, rate, null);

    // Trường chỉ có GPA trung bình 3,5.
    private static SchoolAcademics GpaOnly() => new(
        3.5m, null, null, null, null, null, "optional", null, null, null, null, 0.6m, null);

    private static SchoolAcademics Empty(string? policy = null) => new(
        null, null, null, null, null, null, policy, null, null, null, null, 0.5m, null);

    private static StudentAcademics Student(decimal? gpa = 3.5m, int? sat = 1300, int? act = null, int ec = 3) =>
        new(gpa, sat, act, ec);

    // ---------------------------------------------------------------- ví dụ trong tài liệu

    [Fact]
    public void WorkedExampleSchoolB_IsMatch_WithTheDocumentNumbers()
    {
        var grade = SchoolGrouper.Grade(Student(3.5m, 1300, ec: 4), SchoolB());

        Assert.Equal(0.35, grade.Pg!.Value, 2);
        Assert.Equal(0.53, grade.Ps!.Value, 2);
        Assert.Equal(0.43, grade.P!.Value, 2);
        Assert.Equal(0.46, grade.PAdjusted!.Value, 2);
        Assert.Equal(SchoolGroup.Match, grade.Group);
        Assert.Contains(GroupReasons.InRange, grade.Reasons);
    }

    // ---------------------------------------------------------------- điểm thi

    [Fact]
    public void StudentWithoutTest_AtOptionalSchool_IsGradedOnGpaAlone()
    {
        var grade = SchoolGrouper.Grade(Student(3.5m, sat: null), SchoolB());

        Assert.Null(grade.Ps);
        Assert.NotNull(grade.Pg);
        Assert.Equal(grade.Pg!.Value, grade.P!.Value, 9);
        Assert.NotEqual(SchoolGroup.NoData, grade.Group);
    }

    [Fact]
    public void StudentWithoutTest_AtRequiredSchool_NeedsATest()
    {
        var school = SchoolB() with { SatPolicy = "required" };

        var grade = SchoolGrouper.Grade(Student(3.5m, sat: null), school);

        Assert.Equal(SchoolGroup.NoData, grade.Group);
        Assert.Contains(GroupReasons.NeedTest, grade.Reasons);
    }

    [Fact]
    public void StudentWithATest_AtRequiredSchool_IsGradedNormally()
    {
        var school = SchoolB() with { SatPolicy = "required" };

        Assert.NotEqual(SchoolGroup.NoData, SchoolGrouper.Grade(Student(), school).Group);
    }

    [Fact]
    public void StudentWithActOnly_UsesTheSchoolsActRange()
    {
        var school = new SchoolAcademics(null, null, null, null, 26, 31, "optional", null, null, null, null, 0.6m, null);

        var grade = SchoolGrouper.Grade(Student(null, sat: null, act: 28), school);

        Assert.NotNull(grade.Ps);
        Assert.NotEqual(SchoolGroup.NoData, grade.Group);
    }

    [Fact]
    public void SatAndActAreNotConvertedIntoEachOther()
    {
        // học sinh chỉ có SAT, trường chỉ công bố ACT: không so sánh được điểm thi
        var school = new SchoolAcademics(3.5m, null, null, null, 26, 31, "optional", null, null, null, null, 0.6m, null);

        var grade = SchoolGrouper.Grade(Student(3.5m, sat: 1300), school);

        Assert.Null(grade.Ps);
        Assert.NotNull(grade.Pg);
    }

    [Fact]
    public void StudentWithBothTests_PrefersSat()
    {
        var school = new SchoolAcademics(null, null, 1000, 1200, 20, 24, "optional", null, null, null, null, 0.6m, null);

        var withSat = SchoolGrouper.Grade(Student(null, sat: 1100, act: 34), school);

        Assert.Equal(0.5, withSat.Ps!.Value, 2);   // 1100 là điểm giữa 1000–1200, không dùng ACT 34
    }

    [Fact]
    public void InvalidQuartiles_AreTreatedAsMissing()
    {
        var school = new SchoolAcademics(3.5m, null, 1200, 1200, null, null, "optional", null, null, null, null, 0.6m, null);

        var grade = SchoolGrouper.Grade(Student(3.5m, 1300), school);

        Assert.Null(grade.Ps);
        Assert.NotNull(grade.Pg);
    }

    // ---------------------------------------------------------------- GPA

    [Fact]
    public void SchoolWithoutGpaData_IsGradedOnTestAlone()
    {
        var grade = SchoolGrouper.Grade(Student(3.5m, 1100), TestOnly());

        Assert.Null(grade.Pg);
        Assert.Equal(grade.Ps!.Value, grade.P!.Value, 9);
        Assert.Equal(SchoolGroup.Match, grade.Group);
    }

    [Fact]
    public void StudentWithoutGpa_IsGradedOnTestAlone()
    {
        var grade = SchoolGrouper.Grade(Student(null, 1100), TestOnly());

        Assert.Null(grade.Pg);
        Assert.NotNull(grade.Ps);
    }

    [Fact]
    public void SchoolWithOnlyAverageGpa_UsesTheBellCurveFallback()
    {
        var grade = SchoolGrouper.Grade(Student(3.5m, sat: null), GpaOnly());

        Assert.Equal(0.5, grade.Pg!.Value, 2);
    }

    // ---------------------------------------------------------------- thiếu dữ liệu

    [Fact]
    public void SchoolWithNoAcademicData_IsNoData()
    {
        var grade = SchoolGrouper.Grade(Student(), Empty());

        Assert.Equal(SchoolGroup.NoData, grade.Group);
        Assert.Contains(GroupReasons.NoAcademicData, grade.Reasons);
        Assert.Null(grade.P);
        Assert.Null(grade.PAdjusted);
    }

    [Fact]
    public void StudentWithNothing_IsNoData_EvenIfTheSchoolHasData()
    {
        var grade = SchoolGrouper.Grade(Student(null, null, null), SchoolB());

        Assert.Equal(SchoolGroup.NoData, grade.Group);
        Assert.Contains(GroupReasons.NoAcademicData, grade.Reasons);
    }

    [Fact]
    public void MissingAcceptanceRate_IsFlagged_ButTheSchoolIsStillGraded()
    {
        var grade = SchoolGrouper.Grade(Student(null, 1190), TestOnly(rate: null));

        Assert.NotEqual(SchoolGroup.NoData, grade.Group);
        Assert.Contains(GroupReasons.NoAcceptRate, grade.Reasons);
    }

    // ---------------------------------------------------------------- ngoại khóa đi vào nhóm

    [Fact]
    public void Activities_CanPushABorderlineStudentIntoMatch()
    {
        // SAT 985 ở trường 1000–1200: p ≈ 0,22, dưới 0,25
        var plain = SchoolGrouper.Grade(Student(null, 985, ec: 3), TestOnly(ec: "very_important"));
        var strong = SchoolGrouper.Grade(Student(null, 985, ec: 5), TestOnly(ec: "very_important"));
        var weak = SchoolGrouper.Grade(Student(null, 985, ec: 1), TestOnly(ec: "very_important"));

        Assert.Equal(SchoolGroup.Reach, plain.Group);
        Assert.Equal(SchoolGroup.Match, strong.Group);
        Assert.Equal(SchoolGroup.Reach, weak.Group);
    }

    [Fact]
    public void Activities_AreIgnoredByASchoolThatDoesNotConsiderThem()
    {
        var a = SchoolGrouper.Grade(Student(null, 985, ec: 5), TestOnly(ec: "not_considered"));
        var b = SchoolGrouper.Grade(Student(null, 985, ec: 1), TestOnly(ec: "not_considered"));

        Assert.Equal(a.PAdjusted, b.PAdjusted);
    }

    [Fact]
    public void Activities_CannotRescueAVeryWeakStudent()
    {
        var grade = SchoolGrouper.Grade(Student(null, 800, ec: 5), TestOnly(ec: "very_important"));

        Assert.Equal(SchoolGroup.Reach, grade.Group);
    }

    [Fact]
    public void StudentWithoutActivityData_DefaultsToTheNeutralTier()
    {
        var withDefault = SchoolGrouper.Grade(new StudentAcademics(3.5m, 1300, null), SchoolB());
        var withThree = SchoolGrouper.Grade(Student(3.5m, 1300, ec: 3), SchoolB());

        Assert.Equal(withThree.PAdjusted, withDefault.PAdjusted);
    }

    // ---------------------------------------------------------------- luật nhóm đi qua bước ghép

    [Fact]
    public void VerySelectiveSchool_IsReach_EvenForAStrongStudent()
    {
        var school = TestOnly(rate: 0.08m);

        Assert.Equal(SchoolGroup.Reach, SchoolGrouper.Grade(Student(null, 1500), school).Group);
    }

    [Fact]
    public void StrongTestCannotHideWeakGpa()
    {
        // trường coi trọng điểm thi, GPA học sinh 2,9 thấp hơn nhiều so với trung bình 3,5 → không An toàn
        var school = new SchoolAcademics(3.5m, null, 1000, 1200, null, null, "optional", null,
            "not_considered", "very_important", null, 0.6m, null);

        var grade = SchoolGrouper.Grade(Student(2.9m, 1400), school);

        Assert.True(grade.PAdjusted >= 0.75);
        Assert.True(grade.Pg < 0.25);
        Assert.Equal(SchoolGroup.Match, grade.Group);
    }

    [Fact]
    public void IntlAcceptanceFarBelowOverall_DropsAGroup()
    {
        var school = TestOnly(rate: 0.20m) with { IntlAcceptanceRate = 0.08m };

        Assert.Equal(SchoolGroup.Reach, SchoolGrouper.Grade(Student(null, 1100), school).Group);
    }

    // ---------------------------------------------------------------- ổn định

    [Fact]
    public void SameInput_SameGrade()
    {
        var a = SchoolGrouper.Grade(Student(3.5m, 1300, ec: 4), SchoolB());
        var b = SchoolGrouper.Grade(Student(3.5m, 1300, ec: 4), SchoolB());

        Assert.Equal(a.Group, b.Group);
        Assert.Equal(a.PAdjusted, b.PAdjusted);
    }

    [Fact]
    public void RaisingGpa_NeverLowersTheGroup()
    {
        var previous = SchoolGroup.NoData;
        foreach (var gpa in new[] { 2.5m, 3.0m, 3.3m, 3.5m, 3.7m, 3.9m, 4.0m })
        {
            var group = SchoolGrouper.Grade(Student(gpa, 1300), SchoolB()).Group;
            Assert.True(group >= previous, $"GPA {gpa}: {group} thấp hơn {previous}");
            previous = group;
        }
    }

    [Fact]
    public void RaisingSat_NeverLowersTheGroup()
    {
        var previous = SchoolGroup.NoData;
        foreach (var sat in new[] { 900, 1000, 1100, 1200, 1300, 1400, 1500 })
        {
            var group = SchoolGrouper.Grade(Student(null, sat), TestOnly()).Group;
            Assert.True(group >= previous, $"SAT {sat}: {group} thấp hơn {previous}");
            previous = group;
        }
    }
}
