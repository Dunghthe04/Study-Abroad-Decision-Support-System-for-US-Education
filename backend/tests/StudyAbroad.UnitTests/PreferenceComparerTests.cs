using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.ParentLinks;

namespace StudyAbroad.UnitTests;

// UC-76 (2b): so danh sách của con với danh sách theo ưu tiên cha mẹ (cùng khuôn ItemsJson), dựng nhãn, hạng, số đếm, lý do.
public class PreferenceComparerTests
{
    private static readonly int[] Balanced = [2, 2, 2];

    // Dựng ItemsJson giả: blocks = khối → mã trường theo thứ tự; scored = mã → (cost, f, h, q, aidAvg, aidPolicy, gap)
    private static JsonElement Items(
        Dictionary<string, string[]> blocks,
        params (string Code, double? Cost, double? F, double? H, double? Q, double? AidAvg, string? AidPolicy, double? Gap)[] scored)
    {
        var root = new JsonObject
        {
            ["scored"] = new JsonArray(scored.Select(s => (JsonNode)new JsonObject
            {
                ["code"] = s.Code, ["name"] = "Trường " + s.Code, ["cost"] = s.Cost, ["f"] = s.F, ["h"] = s.H, ["q"] = s.Q,
                ["aidAvg"] = s.AidAvg, ["aidPolicy"] = s.AidPolicy, ["gap"] = s.Gap,
            }).ToArray()),
        };
        foreach (var (group, codes) in blocks)
            root[group] = new JsonArray(codes.Select(c => (JsonNode)new JsonObject { ["code"] = c }).ToArray());
        return JsonSerializer.Deserialize<JsonElement>(root.ToJsonString());
    }

    private static (string, double?, double?, double?, double?, double?, string?, double?) S(
        string code, double cost = 40_000, double f = 0.5, double h = 0.3, double q = 0.5,
        double? aidAvg = null, string? aidPolicy = null, double? gap = null) => (code, cost, f, h, q, aidAvg, aidPolicy, gap);

    private static ComparedSchool Of(PreferenceComparison r, string code) => r.Schools.Single(x => x.Code == code);

    // 1
    [Fact]
    public void SameSchoolsSameOrder_SameResult_WithKeptNote()
    {
        var items = Items(new() { ["match"] = ["A", "B"], ["safety"] = ["C"] }, S("A"), S("B"), S("C"), S("D"));

        var r = PreferenceComparer.Compare(items, items, Balanced, [4, 0, 2], null);

        Assert.True(r.SameResult);
        Assert.All(r.Schools, x => Assert.Equal(CompareLabels.Both, x.Label));
        Assert.Equal(new CompareCounts(3, 0, 0), r.Counts);
        Assert.Equal(4, r.Kept);   // scored = các trường qua lọc
        Assert.Contains("cùng danh sách", r.SameNote);
        Assert.Contains("còn 4 trường", r.SameNote);
    }

    // 2
    [Fact]
    public void SameSchoolsDifferentOrder_StillSameResult_WithRanks()
    {
        var student = Items(new() { ["match"] = ["A", "B"] }, S("A"), S("B"));
        var parent = Items(new() { ["match"] = ["B", "A"] }, S("A"), S("B"));

        var r = PreferenceComparer.Compare(student, parent, Balanced, Balanced, null);

        Assert.True(r.SameResult);   // không bỏ/thêm trường nào
        var b = Of(r, "B");
        Assert.Equal(CompareLabels.ParentHigher, b.Label);
        Assert.Equal((2, 1), (b.StudentRank!.Value, b.ParentRank!.Value));
        Assert.Equal(CompareLabels.ParentLower, Of(r, "A").Label);
    }

    // 3
    [Fact]
    public void DropsAndAdds_CountedInThreeGroups()
    {
        var student = Items(new() { ["reach"] = ["A"], ["match"] = ["B", "C"] }, S("A"), S("B"), S("C"), S("D"));
        var parent = Items(new() { ["reach"] = ["A"], ["match"] = ["B", "D"] }, S("A"), S("B"), S("C"), S("D"));

        var r = PreferenceComparer.Compare(student, parent, Balanced, [4, 2, 2], null);

        Assert.False(r.SameResult);
        Assert.Null(r.SameNote);
        Assert.Equal(new CompareCounts(2, 1, 1), r.Counts);
        var c = Of(r, "C");
        Assert.Equal(CompareLabels.ParentDrops, c.Label);
        Assert.Equal(("match", 2), (c.StudentGroup!, c.StudentRank!.Value));
        Assert.Null(c.ParentGroup);
        var d = Of(r, "D");
        Assert.Equal(CompareLabels.ParentAdds, d.Label);
        Assert.Equal(("match", 2), (d.ParentGroup!, d.ParentRank!.Value));
        Assert.Equal("Trường D", d.Name);
        // Thứ tự: danh sách của con trước, trường cha mẹ thêm ở cuối
        Assert.Equal(["A", "B", "C", "D"], r.Schools.Select(x => x.Code));
    }

    // 4
    [Fact]
    public void ParentBudgetLower_MovedToAid_WithCostAndBudget()
    {
        var student = Items(new() { ["match"] = ["A"] }, S("A", cost: 58_000));
        var parent = Items(new() { ["aid"] = ["A"] }, S("A", cost: 58_000));

        var r = PreferenceComparer.Compare(student, parent, Balanced, Balanced, 40_000m);

        var a = Of(r, "A");
        Assert.Equal(CompareLabels.MovedToAid, a.Label);
        Assert.Equal("Tổng chi phí 58.000 USD/năm, vượt ngân sách cha mẹ chấp nhận 40.000 USD", a.Reason);
        Assert.True(r.SameResult);   // vẫn cùng tập trường
    }

    // 5
    [Fact]
    public void ParentBudgetLower_SchoolExcluded_DroppedOverBudget()
    {
        var student = Items(new() { ["match"] = ["A", "B"] }, S("A", cost: 70_000), S("B", cost: 30_000));
        var parent = Items(new() { ["match"] = ["B"] }, S("B", cost: 30_000));   // A không còn trong scored: bị lọc

        var r = PreferenceComparer.Compare(student, parent, Balanced, Balanced, 40_000m);

        var a = Of(r, "A");
        Assert.Equal(CompareLabels.DroppedOverBudget, a.Label);
        Assert.Contains("70.000 USD/năm", a.Reason);
        Assert.Contains("40.000 USD", a.Reason);
    }

    // 6
    [Fact]
    public void StudentInAid_ParentBudgetHigher_MovedOutOfAid()
    {
        var student = Items(new() { ["aid"] = ["A"] }, S("A", cost: 70_000));
        var parent = Items(new() { ["match"] = ["A"] }, S("A", cost: 70_000));

        var r = PreferenceComparer.Compare(student, parent, Balanced, Balanced, 80_000m);

        Assert.Equal(CompareLabels.MovedOutOfAid, Of(r, "A").Label);
    }

    // 7: cha mẹ coi trọng chất lượng hơn hẳn → trường chất lượng cao lên hạng, lý do nói chất lượng
    [Fact]
    public void Reason_PicksQuality_WhenParentWeightsQuality()
    {
        var student = Items(new() { ["match"] = ["A", "B"] }, S("A", q: 0.3), S("B", q: 0.9));
        var parent = Items(new() { ["match"] = ["B", "A"] }, S("A", q: 0.3), S("B", q: 0.9));

        var r = PreferenceComparer.Compare(student, parent, [2, 2, 0], [2, 2, 4], null);

        Assert.Equal("Điểm chất lượng cao hơn các trường cùng nhóm", Of(r, "B").Reason);
        Assert.Equal("Điểm chất lượng thấp hơn các trường cùng nhóm", Of(r, "A").Reason);
    }

    // 8: chênh nhiều nhất ở chi phí (cha mẹ 4, con 0) dù chất lượng cũng chênh nhẹ → lý do theo chi phí, nói số tiền
    [Fact]
    public void Reason_PicksLargestDifference_CostWithRealNumbers()
    {
        var student = Items(new() { ["match"] = ["A", "B"] },
            S("A", cost: 55_000, f: 0.0, q: 0.6, gap: 5_000), S("B", cost: 30_000, f: 0.4, q: 0.5));
        var parent = Items(new() { ["match"] = ["B", "A"] },
            S("A", cost: 55_000, f: 0.0, q: 0.6, gap: 5_000), S("B", cost: 30_000, f: 0.4, q: 0.5));

        var r = PreferenceComparer.Compare(student, parent, [0, 2, 2], [4, 2, 3], null);

        Assert.Equal("Tổng chi phí 30.000 USD/năm, rẻ hơn các trường cùng nhóm", Of(r, "B").Reason);   // không có ngân sách để so
        Assert.Equal("Tổng chi phí 55.000 USD/năm, vượt ngân sách 50.000 USD", Of(r, "A").Reason);
    }

    // 9
    [Fact]
    public void Reason_Aid_UsesAverageOrNoAidPolicy()
    {
        var student = Items(new() { ["match"] = ["A", "B"] },
            S("A", h: 0.0, aidPolicy: "none"), S("B", h: 0.6, aidAvg: 32_000));
        var parent = Items(new() { ["match"] = ["B", "A"] },
            S("A", h: 0.0, aidPolicy: "none"), S("B", h: 0.6, aidAvg: 32_000));

        var r = PreferenceComparer.Compare(student, parent, Balanced, [2, 4, 2], null);

        Assert.Equal("Học bổng du học sinh trung bình 32.000 USD/năm", Of(r, "B").Reason);
        Assert.Equal("Trường không cấp học bổng cho du học sinh", Of(r, "A").Reason);
    }

    // 10: cùng ưu tiên (không yếu tố nào chênh) mà vẫn đổi thứ tự → câu chung
    [Fact]
    public void Reason_NoClearFactor_GenericSentence()
    {
        var student = Items(new() { ["match"] = ["A", "B"] }, S("A"), S("B"));
        var parent = Items(new() { ["match"] = ["B", "A"] }, S("A"), S("B"));

        var r = PreferenceComparer.Compare(student, parent, Balanced, Balanced, null);

        Assert.Equal("Thứ tự trong nhóm đổi nhẹ theo ưu tiên", Of(r, "B").Reason);
        Assert.Null(PreferenceComparer.Compare(student, student, Balanced, Balanced, null).Schools[0].Reason);   // cùng hạng: không lý do
    }

    // 11
    [Fact]
    public void EmptyOrMissingBlocks_NoCrash()
    {
        var empty = JsonSerializer.Deserialize<JsonElement>("{}");
        var r = PreferenceComparer.Compare(empty, empty, Balanced, Balanced, null);

        Assert.Empty(r.Schools);
        Assert.True(r.SameResult);
        Assert.Equal(0, r.Kept);
    }

    // 12: trường rẻ hơn cùng nhóm nhưng vẫn vượt ngân sách cha mẹ (nằm trong mức dư 10%) → không được nói "trong ngân sách"
    [Fact]
    public void Reason_CheaperButOverParentBudget_SaysStillOver()
    {
        var student = Items(new() { ["reach"] = ["A", "B"] },
            S("A", cost: 90_000, f: 0.0), S("B", cost: 82_678, f: 0.4));
        var parent = Items(new() { ["reach"] = ["B", "A"] },
            S("A", cost: 90_000, f: 0.0), S("B", cost: 82_678, f: 0.4, gap: 4_869));

        var r = PreferenceComparer.Compare(student, parent, [0, 2, 2], [4, 2, 2], 77_809m);

        Assert.Equal("Tổng chi phí 82.678 USD/năm, rẻ hơn các trường cùng nhóm nhưng vẫn vượt ngân sách cha mẹ 4.869 USD",
            Of(r, "B").Reason);
    }

    // 13: rẻ hơn và không vượt ngân sách cha mẹ → "trong ngân sách cha mẹ"; không có ngân sách cha mẹ thì dựa vào gap của trường
    [Fact]
    public void Reason_CheaperWithinParentBudget_OrByGap()
    {
        var student = Items(new() { ["match"] = ["A", "B"] },
            S("A", cost: 60_000, f: 0.0), S("B", cost: 40_000, f: 0.4, gap: 0));
        var parent = Items(new() { ["match"] = ["B", "A"] },
            S("A", cost: 60_000, f: 0.0), S("B", cost: 40_000, f: 0.4, gap: 0));

        var withBudget = PreferenceComparer.Compare(student, parent, [0, 2, 2], [4, 2, 2], 50_000m);
        Assert.Equal("Tổng chi phí 40.000 USD/năm, rẻ hơn các trường cùng nhóm, trong ngân sách cha mẹ", Of(withBudget, "B").Reason);

        var noBudget = PreferenceComparer.Compare(student, parent, [0, 2, 2], [4, 2, 2], null);
        Assert.Equal("Tổng chi phí 40.000 USD/năm, rẻ hơn các trường cùng nhóm, trong ngân sách", Of(noBudget, "B").Reason);

        var overByGap = Items(new() { ["match"] = ["B", "A"] },
            S("A", cost: 60_000, f: 0.0), S("B", cost: 40_000, f: 0.4, gap: 2_000));
        var r = PreferenceComparer.Compare(student, overByGap, [0, 2, 2], [4, 2, 2], null);
        Assert.Equal("Tổng chi phí 40.000 USD/năm, rẻ hơn các trường cùng nhóm nhưng vẫn vượt ngân sách 2.000 USD", Of(r, "B").Reason);
    }
}
