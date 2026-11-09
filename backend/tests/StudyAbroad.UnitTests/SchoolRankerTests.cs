using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước 5 của công thức gợi ý trường: fit score để chọn và sắp xếp trong nhóm (CONG_THUC_GOI_Y_TRUONG.docx, mục 7).
public class SchoolRankerTests
{
    private static readonly FitStudent Student = new(45_000m);

    private static FitSchool School(
        decimal? cost = 38_000m, decimal? aid = null, decimal? englishShortfall = 0m, params (int, int)[] ranks) =>
        new(cost, aid, englishShortfall,
            SchoolRanker.QualityFromRanks(ranks.Select(r => new RankPosition(r.Item1, r.Item2)).ToList()));

    private static double Value(double p, FitStudent? student = null, FitSchool? school = null, FitWeights? w = null) =>
        SchoolRanker.Score(p, student ?? Student, school ?? School(ranks: (10, 100)), w).Value;

    // ---------------------------------------------------------------- từng thành phần

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(0.9, 0.2)]
    [InlineData(0.1, 0.2)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.0)]
    public void A_IsHighestWhenTheStudentIsInTheMiddle(double pAdjusted, double expected)
    {
        Assert.Equal(expected, SchoolRanker.Score(pAdjusted, Student, School()).Position!.Value, 9);
    }

    [Theory]
    [InlineData(30_000, 0.3333)]
    [InlineData(38_000, 0.1556)]
    [InlineData(45_000, 0.0)]    // đúng bằng ngân sách
    [InlineData(48_000, 0.0)]    // vượt ngân sách: không âm
    public void C_IsHigherTheCheaperTheSchoolIsComparedToTheBudget(int cost, double expected)
    {
        var c = SchoolRanker.Score(0.5, Student, School(cost)).Budget!.Value;

        Assert.True(Math.Abs(c - expected) < 0.0001, $"C = {c:F4}, mong {expected}");
    }

    // H: học bổng cho du học sinh, không phụ thuộc ngân sách. H chính là phần trăm chi phí cả năm mà học bổng trung bình chi trả
    [Theory]
    [InlineData(40_000, 40_000, 1.0)]     // học bổng bằng cả chi phí
    [InlineData(40_000, 30_000, 0.75)]    // 75% chi phí
    [InlineData(60_000, 15_000, 0.25)]    // 25% chi phí
    [InlineData(80_000, 8_000, 0.1)]      // trường đắt, học bổng nhỏ so với chi phí
    [InlineData(38_000, 0, 0.0)]          // trong ngân sách mà không có học bổng: KHÔNG được điểm tối đa
    public void H_IsTheAidAsAShareOfTheCost_NotAnyMoreWhetherTheSchoolFitsTheBudget(int cost, int aid, double expected)
    {
        var h = SchoolRanker.Score(0.5, Student, School(cost, aid)).Aid!.Value;

        Assert.Equal(expected, h, 9);
    }

    [Fact]
    public void H_IsTheSame_WhetherOrNotTheSchoolIsInsideTheBudget()
    {
        var inside = SchoolRanker.Score(0.5, Student, School(40_000, 12_000)).Aid;      // ngân sách 45.000
        var outside = SchoolRanker.Score(0.5, new FitStudent(20_000m), School(40_000, 12_000)).Aid;

        Assert.Equal(inside, outside);
    }

    [Fact]
    public void H_IsZero_WhenThePolicyStatesThereIsNoAidForInternationalStudents()
    {
        // có số trung bình nhưng chính sách nói không có học bổng: tin chính sách
        Assert.Equal(0.0, SchoolRanker.Score(0.5, Student, new FitSchool(40_000m, 15_000m, 0m, null, "none")).Aid!.Value, 9);
        Assert.Equal(0.0, SchoolRanker.Score(0.5, Student, new FitSchool(40_000m, null, 0m, null, "none")).Aid!.Value, 9);
    }

    [Fact]
    public void H_IsMissing_WhenTheAidIsUnknownOrImplausible()
    {
        Assert.Null(SchoolRanker.Score(0.5, Student, School(60_000, aid: null)).Aid);
        Assert.Null(SchoolRanker.Score(0.5, Student, School(38_000, aid: null)).Aid);       // trong ngân sách cũng không còn mặc định 1
        Assert.Null(SchoolRanker.Score(0.5, Student, School(40_000, aid: 90_000)).Aid);     // học bổng lớn hơn cả chi phí là số liệu sai
    }

    [Fact]
    public void WithAidPriorityAtTheTop_ASchoolWithBigAidRanksAboveACheapOneWithNone()
    {
        // Thành phần H của trường học bổng lớn cao hơn trường rẻ không có học bổng (trước đây trường rẻ được 1 vì trong ngân sách)
        Assert.True(SchoolRanker.Score(0.5, Student, new FitSchool(60_000m, 35_000m, 0m, 0.5, "need_and_merit")).Aid
                    > SchoolRanker.Score(0.5, Student, new FitSchool(30_000m, null, 0m, 0.5, "none")).Aid);
    }

    [Theory]
    [InlineData(1, 100, 1.00)]
    [InlineData(10, 100, 0.91)]
    [InlineData(80, 100, 0.21)]
    [InlineData(100, 100, 0.01)]
    public void Q_IsTheRankTurnedIntoZeroToOne(int rank, int total, double expected)
    {
        var q = SchoolRanker.Score(0.5, Student, School(ranks: (rank, total))).Quality!.Value;

        Assert.Equal(expected, q, 9);
    }

    [Fact]
    public void Q_AveragesAcrossRankings_AndIgnoresInvalidOnes()
    {
        var q = SchoolRanker.Score(0.5, Student, School(ranks: [(1, 100), (50, 100), (5, 0), (200, 100)])).Quality!.Value;

        Assert.Equal((1.0 + 0.51) / 2, q, 9);   // (1/100 → 1,00) và (50/100 → 0,51); bảng sai bị bỏ
    }

    [Fact]
    public void Q_IsMissing_WithoutValidRankings()
    {
        Assert.Null(SchoolRanker.Score(0.5, Student, School()).Quality);
        Assert.Null(SchoolRanker.Score(0.5, Student, School(ranks: (5, 0))).Quality);
    }

    // E = 1 - mức thiếu tiếng Anh (band), mức thiếu đã tính ở bước lọc từ bài thi tốt nhất của học sinh
    [Theory]
    [InlineData(0.0, 1.0)]    // đã đạt mức tối thiểu
    [InlineData(0.2, 0.8)]    // thiếu 5 điểm Duolingo (0,2 band)
    [InlineData(0.5, 0.5)]    // thiếu nửa band
    [InlineData(1.0, 0.0)]    // thiếu một band
    [InlineData(1.5, 0.0)]    // thiếu nhiều: không âm
    public void E_IsOneMinusTheShortfallInBands(double shortfall, double expected)
    {
        var e = SchoolRanker.Score(0.5, Student, School(englishShortfall: (decimal)shortfall)).English!.Value;

        Assert.Equal(expected, e, 9);
    }

    [Fact]
    public void E_IsMissing_WhenTheShortfallCouldNotBeComputed()
    {
        // trường không công bố mức tối thiểu, hoặc không có bài thi nào so được
        Assert.Null(SchoolRanker.Score(0.5, Student, School(englishShortfall: null)).English);
    }

    [Fact]
    public void Q_AGivenQualityScoreIsUsedAsIs_ButKeptWithinZeroAndOne()
    {
        Assert.Equal(0.7, SchoolRanker.Score(0.5, Student, new FitSchool(null, null, null, 0.7)).Quality!.Value, 9);
        Assert.Equal(1.0, SchoolRanker.Score(0.5, Student, new FitSchool(null, null, null, 1.4)).Quality!.Value, 9);
        Assert.Equal(0.0, SchoolRanker.Score(0.5, Student, new FitSchool(null, null, null, -0.2)).Quality!.Value, 9);
    }

    // ---------------------------------------------------------------- tổng

    [Fact]
    public void WorkedExample_TwoSchoolsInTheSameGroup_TheFitterOneIsHigher()
    {
        // U: p' = 0,5, chi phí 38.000, chưa có số liệu học bổng (bỏ H), hạng 10/100, đạt tiếng Anh  →  khoảng 0,73
        var u = SchoolRanker.Score(0.5, Student, School(38_000m, null, 0m, (10, 100))).Value;
        // V: p' = 0,7, chi phí 60.000 học bổng 3.000 (5% chi phí, H = 0,05), hạng 80/100, thiếu nửa band tiếng Anh  →  khoảng 0,28
        var v = SchoolRanker.Score(0.7, Student, School(60_000m, 3_000m, 0.5m, (80, 100))).Value;

        Assert.True(Math.Abs(u - 0.73) < 0.005, $"U = {u:F4}");
        Assert.True(Math.Abs(v - 0.28) < 0.005, $"V = {v:F4}");
        Assert.True(u > v);
    }

    [Fact]
    public void MissingComponents_AreDropped_AndTheRestKeepTheirRelativeWeights()
    {
        // trường thiếu chi phí và học bổng: chỉ còn A, Q, E với trọng số 0,30 / 0,20 / 0,10
        var school = new FitSchool(null, null, 0m, 0.91);

        var score = SchoolRanker.Score(0.5, Student, school);

        var expected = (0.30 * 1.0 + 0.20 * 0.91 + 0.10 * 1.0) / 0.60;
        Assert.Equal(expected, score.Value, 9);
    }

    [Fact]
    public void WithOnlyThePositionKnown_FitEqualsA()
    {
        var score = SchoolRanker.Score(0.6, new FitStudent(null), new FitSchool(null, null, null, null));

        Assert.Equal(score.Position!.Value, score.Value, 9);
    }

    [Fact]
    public void Fit_IsAlwaysBetweenZeroAndOne()
    {
        foreach (var p in new[] { 0.0, 0.3, 0.5, 0.8, 1.0 })
            Assert.InRange(Value(p), 0.0, 1.0);
    }

    [Fact]
    public void Weights_CanBeChangedByTheStudentsPriorities()
    {
        var cheap = School(20_000m, null, 0m, (90, 100));      // rẻ, hạng thấp
        var prestigious = School(70_000m, 10_000m, 0m, (2, 100));   // đắt, hạng cao

        var costFirst = new FitWeights(Position: 0.1, Budget: 0.8, Aid: 0.0, Quality: 0.1, English: 0.0);
        var qualityFirst = new FitWeights(Position: 0.1, Budget: 0.0, Aid: 0.0, Quality: 0.9, English: 0.0);

        Assert.True(Value(0.5, school: cheap, w: costFirst) > Value(0.5, school: prestigious, w: costFirst));
        Assert.True(Value(0.5, school: prestigious, w: qualityFirst) > Value(0.5, school: cheap, w: qualityFirst));
    }

    // ---------------------------------------------------------------- ba thanh ưu tiên

    [Fact]
    public void Priorities_AllBalanced_GiveTheDefaultWeights()
    {
        Assert.Equal(new FitWeights(), SchoolRanker.WeightsFromPriorities([2, 2, 2]));
    }

    [Fact]
    public void Priorities_ScaleTheMatchingWeightByLevelOverTwo()
    {
        var w = SchoolRanker.WeightsFromPriorities([4, 0, 1]);

        Assert.Equal(0.50, w.Budget, 9);     // 0,25 × 4/2
        Assert.Equal(0.0, w.Aid, 9);         // 0,15 × 0/2
        Assert.Equal(0.10, w.Quality, 9);    // 0,20 × 1/2
        Assert.Equal(0.30, w.Position, 9);   // không đổi
        Assert.Equal(0.10, w.English, 9);    // không đổi
    }

    [Theory]
    [InlineData(null)]
    public void Priorities_Missing_UseTheDefaults(int[]? priorities)
    {
        Assert.Equal(new FitWeights(), SchoolRanker.WeightsFromPriorities(priorities));
    }

    [Theory]
    [InlineData(new[] { 2, 2 })]
    [InlineData(new[] { 2, 2, 2, 2 })]
    [InlineData(new[] { 5, 2, 2 })]
    [InlineData(new[] { -1, 2, 2 })]
    public void Priorities_Invalid_UseTheDefaults(int[] priorities)
    {
        Assert.Equal(new FitWeights(), SchoolRanker.WeightsFromPriorities(priorities));
    }

    [Fact]
    public void Priorities_ChangeTheOrderInsideAGroup_ButNotTheGroup()
    {
        var cheap = Graded("CHEAP", SchoolGroup.Match, 0.5, School(20_000m, null, 0m, (90, 100)));
        var famous = Graded("FAMOUS", SchoolGroup.Match, 0.5, School(70_000m, 10_000m, 0m, (2, 100)));

        var costFirst = SchoolRanker.Pick([cheap, famous], Student, SchoolRanker.WeightsFromPriorities([4, 2, 0]));
        var qualityFirst = SchoolRanker.Pick([cheap, famous], Student, SchoolRanker.WeightsFromPriorities([0, 2, 4]));

        Assert.Equal(["CHEAP", "FAMOUS"], costFirst.Match.Select(r => r.Code));
        Assert.Equal(["FAMOUS", "CHEAP"], qualityFirst.Match.Select(r => r.Code));
    }

    // ---------------------------------------------------------------- chọn tối đa 4 trường mỗi nhóm

    private static GradedSchool Graded(string code, SchoolGroup group, double? pAdjusted, FitSchool? fit = null) =>
        new(code, new SchoolGrade(group, null, null, pAdjusted, pAdjusted, []), fit ?? School(ranks: (10, 100)));

    [Fact]
    public void Pick_SortsEachGroupByFitDescending_AndKeepsAtMostFour()
    {
        var schools = new[]
        {
            Graded("M1", SchoolGroup.Match, 0.20), Graded("M2", SchoolGroup.Match, 0.50),
            Graded("M3", SchoolGroup.Match, 0.44), Graded("M4", SchoolGroup.Match, 0.62),
            Graded("M5", SchoolGroup.Match, 0.58), Graded("M6", SchoolGroup.Match, 0.74),
        };

        var list = SchoolRanker.Pick(schools, Student);

        Assert.Equal(["M2", "M3", "M5", "M4"], list.Match.Select(r => r.Code));   // gần giữa nhóm nhất lên đầu
        Assert.Equal(6, list.Available.Match);
    }

    [Fact]
    public void Pick_KeepsTheGroupsApart_AndReportsTheRealCounts()
    {
        var schools = new[]
        {
            Graded("R1", SchoolGroup.Reach, 0.10), Graded("R2", SchoolGroup.Reach, 0.20),
            Graded("M1", SchoolGroup.Match, 0.50),
            Graded("S1", SchoolGroup.Safety, 0.80), Graded("S2", SchoolGroup.Safety, 0.90),
            Graded("N1", SchoolGroup.NoData, null),
        };

        var list = SchoolRanker.Pick(schools, Student);

        Assert.Equal(["R2", "R1"], list.Reach.Select(r => r.Code));
        Assert.Equal(["M1"], list.Match.Select(r => r.Code));
        Assert.Equal(["S1", "S2"], list.Safety.Select(r => r.Code));
        Assert.Equal(["N1"], list.NoData.Select(r => r.Code));
        Assert.Equal(new GroupCounts(2, 1, 2, 1), list.Available);
    }

    [Fact]
    public void Pick_NoDataSchoolsHaveNoFitScore()
    {
        var list = SchoolRanker.Pick([Graded("N1", SchoolGroup.NoData, null)], Student);

        Assert.Null(list.NoData[0].Fit);
    }

    [Fact]
    public void Pick_TiesAreBrokenByCode_SoTheResultIsDeterministic()
    {
        var schools = new[]
        {
            Graded("B", SchoolGroup.Match, 0.5), Graded("A", SchoolGroup.Match, 0.5), Graded("C", SchoolGroup.Match, 0.5),
        };

        var list = SchoolRanker.Pick(schools, Student);

        Assert.Equal(["A", "B", "C"], list.Match.Select(r => r.Code));
    }

    [Fact]
    public void Pick_GivesTheSameResultWhateverTheInputOrder()
    {
        var schools = Enumerable.Range(1, 12)
            .Select(i => Graded($"S{i:00}", SchoolGroup.Match, 0.3 + i * 0.03)).ToList();

        var forward = SchoolRanker.Pick(schools, Student).Match.Select(r => r.Code).ToList();
        var backward = SchoolRanker.Pick(Enumerable.Reverse(schools), Student).Match.Select(r => r.Code).ToList();

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void Pick_TheLimitPerGroupCanBeChanged()
    {
        var schools = Enumerable.Range(1, 10).Select(i => Graded($"S{i:00}", SchoolGroup.Safety, 0.8)).ToList();

        Assert.Equal(2, SchoolRanker.Pick(schools, Student, maxPerGroup: 2).Safety.Count);
        Assert.Equal(10, SchoolRanker.Pick(schools, Student, maxPerGroup: 99).Safety.Count);
    }

    [Fact]
    public void Pick_WithNoSchools_ReturnsEmptyGroups()
    {
        var list = SchoolRanker.Pick([], Student);

        Assert.Empty(list.Reach);
        Assert.Empty(list.Match);
        Assert.Empty(list.Safety);
        Assert.Equal(new GroupCounts(0, 0, 0, 0), list.Available);
    }

    [Fact]
    public void Pick_DoesNotMoveASchoolBetweenGroups()
    {
        // fit chỉ xếp thứ tự trong nhóm: trường Khó vào có fit rất cao vẫn nằm ở Khó vào
        var schools = new[]
        {
            Graded("R", SchoolGroup.Reach, 0.5, School(10_000m, null, 0m, (1, 100))),
            Graded("S", SchoolGroup.Safety, 0.95, School(90_000m, null, 1.5m, (100, 100))),
        };

        var list = SchoolRanker.Pick(schools, Student);

        Assert.Equal(["R"], list.Reach.Select(r => r.Code));
        Assert.Equal(["S"], list.Safety.Select(r => r.Code));
    }
}
