using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class HardFilterTests
{
    private const decimal Tolerance = 0.10m;

    private static FilterCriteria An(AidNeed aid = AidNeed.Preferred) => new(
        "Computer and information sciences", [], null, 50_000m, aid, Ielts: 6.5m);

    // Học sinh với các chứng chỉ chỉ định (không truyền thì chưa có chứng chỉ nào)
    private static FilterCriteria Cert(decimal? ielts = null, decimal? toefl = null, decimal? duolingo = null) => new(
        "Computer and information sciences", [], null, 50_000m, AidNeed.Preferred, ielts, toefl, duolingo);

    private static FilterSchool School(
        string code, decimal? cost, bool? hasAid, string major = "Computer and information sciences",
        decimal? minIelts = null, decimal? minToefl = null, decimal? minDuolingo = null) =>
        new(code, [major], "NY", "private", cost, hasAid, minIelts, minToefl, minDuolingo);

    [Fact]
    public void An_KeepsAffordableSchools_AndPutsColgateInAidBlock()
    {
        var results = HardFilter.Apply(An(),
        [
            School("Colgate", 95_610m, true),                                  // vượt trần, có học bổng
            School("B", 38_000m, false, minIelts: 6.0m),
            School("C", 46_000m, false, minIelts: 6.5m),
            School("D", 20_000m, false, major: "History"),                     // sai ngành
        ], Tolerance);

        Assert.Equal(FilterOutcome.AidBlock, results[0].Outcome);
        Assert.Equal(45_610m, results[0].OverBudgetUsd);
        Assert.Contains(FilterFlags.NeedBigAid, results[0].Flags);
        Assert.Contains(FilterFlags.EnglishMinUnknown, results[0].Flags);
        Assert.Equal(FilterOutcome.Keep, results[1].Outcome);
        Assert.Equal(FilterOutcome.Keep, results[2].Outcome);
        Assert.Equal((FilterOutcome.Excluded, "MAJOR"), (results[3].Outcome, results[3].ExcludeReason));
    }

    [Fact]
    public void An_WithoutAidNeed_DropsSchoolsOverTheCeiling()
    {
        var result = HardFilter.Check(An(AidNeed.None), School("Colgate", 95_610m, true), Tolerance);

        Assert.Equal((FilterOutcome.Excluded, "BUDGET"), (result.Outcome, result.ExcludeReason));
    }

    // ---------------------------------------------------------------- cổng tiếng Anh (B4)

    [Fact]
    public void English_ShortByHalfABand_IsKeptWithANearFlag()
    {
        // IELTS 6,5 của học sinh thấp hơn mức 7,0 nửa band: giữ trường, gắn cờ; tiếng Anh tính ở fit score
        var result = HardFilter.Check(An(), School("Strict", 40_000m, false, minIelts: 7.0m), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.Contains(FilterFlags.EnglishNear, result.Flags);
        Assert.Equal(0.5m, result.EnglishShortfall);
    }

    [Fact]
    public void English_MeetingTheMinimum_HasNoFlag_AndZeroShortfall()
    {
        var result = HardFilter.Check(An(), School("Open", 40_000m, false, minIelts: 6.5m), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.DoesNotContain(FilterFlags.EnglishNear, result.Flags);
        Assert.Equal(0m, result.EnglishShortfall);
    }

    // Ranh giới IELTS: thiếu 0 và 0,5 band thì giữ; đúng 1,0 band thì loại
    [Theory]
    [InlineData(7.0, FilterOutcome.Keep)]
    [InlineData(6.5, FilterOutcome.Keep)]
    [InlineData(6.0, FilterOutcome.Excluded)]
    [InlineData(5.5, FilterOutcome.Excluded)]
    public void English_Ielts_AFullBandShortIsExcluded(double ielts, FilterOutcome expected)
    {
        var result = HardFilter.Check(Cert(ielts: (decimal)ielts), School("S", 40_000m, false, minIelts: 7.0m), Tolerance);

        Assert.Equal(expected, result.Outcome);
        if (expected == FilterOutcome.Excluded) Assert.Equal(EnglishExclude.Short, result.ExcludeReason);
    }

    // Ranh giới TOEFL: 15 điểm = 1 band, trường yêu cầu 80 nên 66 thiếu 14 điểm (0,93 band) còn 65 thiếu đúng 15 điểm
    [Theory]
    [InlineData(80, FilterOutcome.Keep)]
    [InlineData(66, FilterOutcome.Keep)]
    [InlineData(65, FilterOutcome.Excluded)]
    public void English_Toefl_FifteenPointsShortIsExcluded(double toefl, FilterOutcome expected)
    {
        var result = HardFilter.Check(Cert(toefl: (decimal)toefl), School("S", 40_000m, false, minToefl: 80m), Tolerance);

        Assert.Equal(expected, result.Outcome);
    }

    // Ranh giới Duolingo: 25 điểm = 1 band, trường yêu cầu 115 nên 91 thiếu 24 điểm còn 90 thiếu đúng 25 điểm
    [Theory]
    [InlineData(115, FilterOutcome.Keep)]
    [InlineData(91, FilterOutcome.Keep)]
    [InlineData(90, FilterOutcome.Excluded)]
    public void English_Duolingo_TwentyFivePointsShortIsExcluded(double duolingo, FilterOutcome expected)
    {
        var result = HardFilter.Check(Cert(duolingo: (decimal)duolingo), School("S", 40_000m, false, minDuolingo: 115m), Tolerance);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public void English_OneTestPassingIsEnough()
    {
        // IELTS 5,5 thiếu 1 band, nhưng Duolingo 120 vượt mức 115: đạt
        var result = HardFilter.Check(
            Cert(ielts: 5.5m, duolingo: 120m),
            School("S", 40_000m, false, minIelts: 6.5m, minDuolingo: 115m), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.DoesNotContain(FilterFlags.EnglishNear, result.Flags);
        Assert.Equal(0m, result.EnglishShortfall);
    }

    [Fact]
    public void English_TheBestTestDecidesTheShortfall()
    {
        // IELTS 5,5 thiếu 1,0 band, Duolingo 110 thiếu 5 điểm (0,2 band): lấy bài ít thiếu nhất
        var result = HardFilter.Check(
            Cert(ielts: 5.5m, duolingo: 110m),
            School("S", 40_000m, false, minIelts: 6.5m, minDuolingo: 115m), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.Contains(FilterFlags.EnglishNear, result.Flags);
        Assert.Equal(0.2m, result.EnglishShortfall);
    }

    [Fact]
    public void English_WithoutAnyCertificate_IsExcluded()
    {
        var result = HardFilter.Check(Cert(), School("S", 40_000m, false, minIelts: 6.0m), Tolerance);

        Assert.Equal((FilterOutcome.Excluded, EnglishExclude.NoCertificate), (result.Outcome, result.ExcludeReason));
    }

    [Fact]
    public void English_SchoolWithoutAnyMinimum_IsKeptWithAFlag()
    {
        var result = HardFilter.Check(An(), School("S", 40_000m, false), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.Contains(FilterFlags.EnglishMinUnknown, result.Flags);
        Assert.Null(result.EnglishShortfall);
    }

    [Fact]
    public void English_NoTestInCommon_IsKeptWithAFlag()
    {
        // Học sinh chỉ có Duolingo, trường chỉ công bố mức IELTS: chưa so được
        var result = HardFilter.Check(Cert(duolingo: 100m), School("S", 40_000m, false, minIelts: 6.5m), Tolerance);

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
        Assert.Contains(FilterFlags.EnglishNotComparable, result.Flags);
    }

    [Fact]
    public void English_ExcludedSchoolStillCarriesTheShortfall()
    {
        var result = HardFilter.Check(Cert(ielts: 5.0m), School("S", 40_000m, false, minIelts: 6.5m), Tolerance);

        Assert.Equal(FilterOutcome.Excluded, result.Outcome);
        Assert.Equal(1.5m, result.EnglishShortfall);   // để thông báo "cần IELTS bao nhiêu"
    }

    [Fact]
    public void English_ThresholdsComeFromTheOptions()
    {
        // Nới ngưỡng loại lên 1,5 band: thiếu 1,0 band vẫn giữ
        var result = HardFilter.Check(
            Cert(ielts: 5.5m), School("S", 40_000m, false, minIelts: 6.5m), Tolerance,
            new EnglishOptions(ExcludeFromShortfall: 1.5m));

        Assert.Equal(FilterOutcome.Keep, result.Outcome);
    }
}
