using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class HardFilterTests
{
    private const decimal Tolerance = 0.10m;

    //Trường mẫu "đạt hết": tổng chi phí 50.000, có ngành Engineering, IELTS tối thiểu 6.5
    private static SchoolCandidate School() => new(
        Guid.NewGuid(), Guid.NewGuid(), "X", "School X", "NY", ["Engineering", "Business/marketing"],
        AvgGpa4: 3.5m, Sat25: 1300, Sat75: 1450,
        TuitionUsd: 35000m, LivingUsd: 15000m, FeesUsd: 0m,
        MinIelts: 6.5m, Control: "private", HasIntlAid: true);

    //Học sinh An: ngành Engineering, ngân sách 50.000, chọn "Nên có" học bổng, IELTS 7.0
    private static FilterCriteria An() => new("Engineering", [], null, 50000m, AidNeed.Preferred, Ielts: 7.0m);

    private static FilterResult Run(FilterCriteria c, SchoolCandidate s) => HardFilter.Check(c, s, Tolerance);

    [Fact]
    public void SchoolMeetingAllConditions_IsKeptWithoutFlags()
    {
        var r = Run(An(), School());
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
        Assert.Empty(r.Flags);
    }

    // ---- Ngành ----
    [Fact]
    public void MajorNotOffered_IsExcluded()
    {
        var r = Run(An() with { Major = "Psychology" }, School());
        Assert.Equal(FilterOutcome.Excluded, r.Outcome);
        Assert.Equal("MAJOR", r.ExcludeReason);
    }

    [Fact]
    public void NoMajorChosen_DoesNotFilter() =>
        Assert.Equal(FilterOutcome.Keep, Run(An() with { Major = null }, School() with { Majors = ["Psychology"] }).Outcome);

    [Fact]
    public void SchoolWithoutMajorData_IsKeptWithFlag()
    {
        var r = Run(An(), School() with { Majors = [] });
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
        Assert.Contains(FilterFlags.MajorUnknown, r.Flags);
    }

    // ---- Bang ----
    [Fact]
    public void StateOutsideChosenList_IsExcluded() =>
        Assert.Equal("STATE", Run(An() with { States = ["CA", "MA"] }, School()).ExcludeReason);

    [Fact]
    public void SchoolWithoutState_IsExcludedWhenStatesChosen() =>
        Assert.Equal("STATE", Run(An() with { States = ["NY"] }, School() with { State = null }).ExcludeReason);

    // ---- Loại trường ----
    [Fact]
    public void DifferentControl_IsExcluded() =>
        Assert.Equal("CONTROL", Run(An() with { Control = "public" }, School()).ExcludeReason);

    [Fact]
    public void UnknownControl_IsKeptWithFlag() =>
        Assert.Contains(FilterFlags.ControlUnknown, Run(An() with { Control = "public" }, School() with { Control = null }).Flags);

    // ---- Tiếng Anh ----
    [Fact]
    public void EnglishBelowMinimum_IsExcluded() =>
        Assert.Equal("ENGLISH", Run(An() with { Ielts = 6.0m }, School()).ExcludeReason);

    [Fact]
    public void EnglishPassingAnyOneTest_IsKept()
    {
        // IELTS trượt nhưng TOEFL đạt → giữ
        var r = Run(An() with { Ielts = 6.0m, Toefl = 100m }, School() with { MinToefl = 90m });
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
    }

    [Fact]
    public void SchoolWithoutEnglishMinimum_IsKeptWithFlag() =>
        Assert.Contains(FilterFlags.EnglishMinUnknown, Run(An(), School() with { MinIelts = null }).Flags);

    [Fact]
    public void StudentWithoutEnglishScore_IsKeptWithFlag() =>
        Assert.Contains(FilterFlags.EnglishNoScore, Run(An() with { Ielts = null }, School()).Flags);

    [Fact]
    public void DifferentTestTypes_AreKeptWithFlag()
    {
        // Học sinh chỉ có IELTS, trường chỉ công bố TOEFL → không so được
        var r = Run(An(), School() with { MinIelts = null, MinToefl = 90m });
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
        Assert.Contains(FilterFlags.EnglishNotComparable, r.Flags);
    }

    // ---- Ngân sách và học bổng ----
    [Fact]
    public void CostWithinTenPercentTolerance_IsKept()
    {
        // 54.000 > ngân sách 50.000 nhưng ≤ trần 55.000
        var r = Run(An(), School() with { TuitionUsd = 39000m });
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
    }

    [Fact]
    public void Colgate_OverBudgetWithIntlAid_GoesToAidBlock()
    {
        // Ví dụ trong tài liệu: Colgate 95.610, chưa công bố mức tiếng Anh, có học bổng du học sinh
        var colgate = School() with { TuitionUsd = 95610m, LivingUsd = 0m, MinIelts = null };
        var r = Run(An(), colgate);

        Assert.Equal(FilterOutcome.AidBlock, r.Outcome);
        Assert.Equal(45610m, r.OverBudgetUsd);
        Assert.Contains(FilterFlags.NeedBigAid, r.Flags);
        Assert.Contains(FilterFlags.EnglishMinUnknown, r.Flags);
    }

    [Fact]
    public void OverBudget_StudentDoesNotNeedAid_IsExcluded() =>
        Assert.Equal("BUDGET", Run(An() with { AidNeed = AidNeed.None }, School() with { TuitionUsd = 80000m }).ExcludeReason);

    [Fact]
    public void OverBudget_SchoolHasNoIntlAid_IsExcluded() =>
        Assert.Equal("BUDGET_NO_AID", Run(An(), School() with { TuitionUsd = 80000m, HasIntlAid = false }).ExcludeReason);

    [Fact]
    public void OverBudget_UnknownIntlAid_GoesToAidBlockWithFlag()
    {
        var r = Run(An() with { AidNeed = AidNeed.Required }, School() with { TuitionUsd = 80000m, HasIntlAid = null });
        Assert.Equal(FilterOutcome.AidBlock, r.Outcome);
        Assert.Contains(FilterFlags.AidUnknown, r.Flags);
    }

    [Fact]
    public void UnknownCost_IsKeptWithFlag()
    {
        var r = Run(An(), School() with { TuitionUsd = null });
        Assert.Equal(FilterOutcome.Keep, r.Outcome);
        Assert.Contains(FilterFlags.CostUnknown, r.Flags);
    }

    [Fact]
    public void Apply_ReturnsOneResultPerSchool()
    {
        var results = HardFilter.Apply(An(), [School(), School() with { Majors = ["History"] }], Tolerance);
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results.Count(r => r.Outcome == FilterOutcome.Excluded));
    }
}
