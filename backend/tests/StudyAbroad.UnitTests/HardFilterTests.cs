using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class HardFilterTests
{
    private const decimal Tolerance = 0.10m;

    private static FilterCriteria An(AidNeed aid = AidNeed.Preferred) => new(
        "Computer and information sciences", [], null, 50_000m, aid, Ielts: 6.5m);

    private static FilterSchool School(
        string code, decimal? cost, bool? hasAid, string major = "Computer and information sciences",
        decimal? minIelts = null) =>
        new(code, [major], "NY", "private", cost, hasAid, minIelts);

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
}