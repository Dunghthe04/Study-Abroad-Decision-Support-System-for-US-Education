using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class SchoolPickerTests
{
    // Ví dụ mục 8 file AI_CACH_THUC_HIEN: An với Colgate (khối học bổng), Trường B (An toàn hơn), Trường C (Vừa sức)
    private static readonly AiPickItem Colgate = new("190099", "RF", "AID", null, null, null, 45610m, 17.2m, 0m, 0m, 0.25m);
    private static readonly AiPickItem B = new("B", "S", "MAIN", 0.24m, 0.105m, 0.8m, null, 70m, 87.5m, 0m, null);
    private static readonly AiPickItem C = new("C", "M", "MAIN", 0.08m, 0.261m, 0.9m, null, 50m, 58.33m, 0m, null);

    private static AiPickResult Run(int[] pref, string needAid, params AiPickItem[] items) =>
        SchoolPicker.Pick(new AiPickRequest(pref, needAid, items), new PerCategoryLimits());

    [Fact]
    public void DocumentExample_Balanced_MatchesExpectedOutput()
    {
        var r = Run([2, 2, 2], "preferred", Colgate, B, C);

        Assert.Equal(("C", 0.414m), (r.Match.Single().Code, r.Match.Single().X!.Value));
        Assert.Equal(["GPA_MID", "SAT_IN_RANGE", "BUDGET_OK"], r.Match.Single().Why);
        Assert.Equal(("B", 0.382m), (r.Safety.Single().Code, r.Safety.Single().X!.Value));
        Assert.Equal(["GPA_MID", "SAT_ABOVE", "BUDGET_OK"], r.Safety.Single().Why);
        Assert.Equal(["GPA_LOW", "SAT_BELOW", "NEED_AID", "INTL_LOW"], r.Aid.Single().Why);
        Assert.Empty(r.Reach);
        Assert.Equal("code", r.SkillVersion);
    }

    [Fact]
    public void LowCostButton_ChangesScoresNotGroups()
    {
        // Tài liệu: bấm "Chi phí thấp" [3,1,1] thì B = 0,325, C = 0,280; nhóm không đổi
        var r = Run([3, 1, 1], "preferred", Colgate, B, C);
        Assert.Equal(0.325m, r.Safety.Single().X);
        Assert.Equal(0.280m, r.Match.Single().X);
    }

    [Fact]
    public void AllPreferencesZero_UsesEqualWeights()
    {
        Assert.Equal(0.414m, Run([0, 0, 0], "no", C).Match.Single().X);
    }

    [Fact]
    public void MissingCriterion_IsDroppedAndWeightsRenormalized()
    {
        var noAid = C with { H = null };
        // (0,08 + 0,9) / 2 = 0,49
        Assert.Equal(0.49m, Run([2, 2, 2], "no", noAid).Match.Single().X);
    }

    [Fact]
    public void Blocks_AreSortedByXAndCutAtLimits()
    {
        var items = Enumerable.Range(0, 7).Select(k => C with { Code = $"M{k}", Q = k / 10m }).ToArray();
        var r = Run([2, 2, 2], "no", items);
        Assert.Equal(["M6", "M5", "M4", "M3", "M2"], r.Match.Select(e => e.Code));   // 7 trường Vừa sức → giữ 5
    }

    [Fact]
    public void ReachIncludesFarReach()
    {
        var far = B with { Code = "F", G2 = "RF" };
        var reach = B with { Code = "R", G2 = "R" };
        Assert.Equal(2, Run([2, 2, 2], "no", far, reach).Reach.Count);
    }

    [Fact]
    public void AidBlock_OnlyWhenStudentNeedsAid_SortedByGap()
    {
        var small = Colgate with { Code = "S", Gap = 5000m };
        Assert.Empty(Run([2, 2, 2], "no", Colgate).Aid);
        Assert.Equal(["S", "190099"], Run([2, 2, 2], "required", Colgate, small).Aid.Select(e => e.Code));
    }

    [Fact]
    public void MissingDataAndOpenAdmission_GoToOther()
    {
        var na = new AiPickItem("NA1", "NA", "MAIN", 0.2m, null, 0.8m, null, null, null, 0m, null);
        var open = new AiPickItem("O1", "O", "MAIN", 0.3m, 0.1m, 0.7m, null, 90m, null, 0m, null);
        Assert.Equal(["NA1", "O1"], Run([2, 2, 2], "no", na, open).Other.Select(e => e.Code));
    }

    [Theory]
    [InlineData(2.0, null, "EC_PLUS")]
    [InlineData(-3.0, null, "EC_MINUS")]
    [InlineData(4.0, 0.3, "INTL_LOW")]     // INTL_LOW thay chỗ EC_*
    public void Why_FourthReasonRules(double adj, double? r, string expected)
    {
        var item = C with { Adj = (decimal)adj, R = (decimal?)r };
        Assert.Equal(expected, SchoolPicker.Why(item)[^1]);
    }

    [Fact]
    public void Why_SkipsMissingScores()
    {
        Assert.Equal(["BUDGET_OK"], SchoolPicker.Why(C with { Pg = null, Ps = null, Adj = 0m }));
    }

    [Fact]
    public void AidBlock_IsCutAtFiveSchools()
    {
        var items = Enumerable.Range(0, 8).Select(k => Colgate with { Code = $"A{k}", Gap = 1000m * (8 - k) }).ToArray();
        var aid = Run([2, 2, 2], "preferred", items).Aid;
        Assert.Equal(["A7", "A6", "A5", "A4", "A3"], aid.Select(e => e.Code));   // vượt ít tiền nhất trước, tối đa 5
    }
}
