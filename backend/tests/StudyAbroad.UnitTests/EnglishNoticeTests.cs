using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Thông báo tiếng Anh (B4): số trường bị ẩn chỉ vì tiếng Anh và bậc thang mở khóa.
public class EnglishNoticeTests
{
    private const decimal Tolerance = 0.10m;
    private const string Major = "Computer and information sciences";

    private static FilterCriteria Student(decimal? ielts = null, decimal? toefl = null, decimal? duolingo = null) =>
        new(Major, [], null, 50_000m, AidNeed.Preferred, ielts, toefl, duolingo);

    private static FilterSchool School(string code, decimal? minIelts, decimal cost = 40_000m, bool hasAid = false) =>
        new(code, [Major], "NY", "private", cost, hasAid, minIelts);

    private static EnglishSummary? Build(FilterCriteria criteria, params FilterSchool[] schools) =>
        EnglishNotice.Build(criteria, schools, HardFilter.Apply(criteria, schools, Tolerance), Tolerance);

    [Fact]
    public void NothingToReport_WhenEnglishExcludesNoSchool()
    {
        var summary = Build(Student(ielts: 7.0m), School("A", 6.5m), School("B", 7.0m));

        Assert.Null(summary);
    }

    [Fact]
    public void Hidden_CountsOnlySchoolsBlockedByEnglishAlone()
    {
        // A: thiếu 1,5 band nhưng chi phí trong ngân sách → bị ẩn chỉ vì tiếng Anh
        // B: thiếu 1,5 band và cũng vượt ngân sách, không có học bổng → vẫn bị loại nếu tiếng Anh đủ, không tính
        // C: đạt tiếng Anh
        var summary = Build(
            Student(ielts: 6.0m),
            School("A", 7.5m), School("B", 7.5m, cost: 200_000m), School("C", 6.0m));

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Hidden);
        Assert.False(summary.NoCertificate);
    }

    [Fact]
    public void Ladder_ListsOnlyLevelsAboveTheStudentsIelts_WhereTheCountGrows()
    {
        // IELTS 5,0. Trường yêu cầu 5,5 / 6,0 / 6,5 / 7,0. Hiện chỉ trường 5,5 đủ (thiếu 0,5 band); thiếu từ 1 band thì loại
        var summary = Build(
            Student(ielts: 5.0m),
            School("S55", 5.5m), School("S60", 6.0m), School("S65", 6.5m), School("S70", 7.0m));

        Assert.NotNull(summary);
        Assert.Equal(3, summary!.Hidden);
        // 5,5 → 5,5 và 6,0 (2) · 6,0 → thêm 6,5 (3) · 6,5 → thêm 7,0 (4) · 7,0 và 7,5 không thêm trường nào nên không ghi
        Assert.Equal(
            [new EnglishLadderStep(5.5m, 2), new EnglishLadderStep(6.0m, 3), new EnglishLadderStep(6.5m, 4)],
            summary.Ladder);
    }

    [Fact]
    public void Ladder_SkipsLevelsAtOrBelowTheStudentsOwnIelts()
    {
        // IELTS 6,0: bậc thang không có 5,0 / 5,5 / 6,0
        var summary = Build(Student(ielts: 6.0m), School("A", 7.5m), School("B", 6.0m));

        Assert.NotNull(summary);
        Assert.All(summary!.Ladder, step => Assert.True(step.Ielts > 6.0m));
    }

    [Fact]
    public void Ladder_IsEmpty_WhenNoIeltsLevelCanOpenTheSchool()
    {
        // Học sinh chỉ có TOEFL 60 và trường chỉ công bố mức TOEFL 80 (thiếu 1,33 band): thêm IELTS không giúp gì
        var schools = new[] { new FilterSchool("T", [Major], "NY", "private", 40_000m, false, MinToefl: 80m) };
        var criteria = Student(toefl: 60m);

        var summary = EnglishNotice.Build(criteria, schools, HardFilter.Apply(criteria, schools, Tolerance), Tolerance);

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Hidden);
        Assert.Empty(summary.Ladder);
    }

    [Fact]
    public void NoCertificate_IsReported_AndEverySchoolIsHidden()
    {
        var summary = Build(Student(), School("A", 6.0m), School("B", 6.5m), School("C", 7.0m));

        Assert.NotNull(summary);
        Assert.True(summary!.NoCertificate);
        Assert.Equal(3, summary.Hidden);
        // Chưa có chứng chỉ thì IELTS 5,0 vẫn chưa mở trường nào (trường thấp nhất yêu cầu 6,0, thiếu đúng 1 band)
        Assert.Equal(
            [new EnglishLadderStep(5.5m, 1), new EnglishLadderStep(6.0m, 2), new EnglishLadderStep(6.5m, 3)],
            summary.Ladder);
    }

    [Fact]
    public void NothingToReport_WhenTheListIsEmptyForOtherReasons()
    {
        // Mọi trường sai ngành: không phải do tiếng Anh nên không có thông báo tiếng Anh
        var wrongMajor = new FilterSchool("W", ["History"], "NY", "private", 40_000m, false, 7.5m);

        var summary = Build(Student(ielts: 5.0m), wrongMajor);

        Assert.Null(summary);
    }
}
