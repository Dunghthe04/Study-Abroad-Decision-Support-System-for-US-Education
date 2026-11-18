using System.Globalization;
using System.Text.Json;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Nhãn của một trường khi đặt danh sách của con cạnh danh sách theo ưu tiên cha mẹ.</summary>
public static class CompareLabels
{
    public const string Both = "both";                          // cả hai cùng chọn, cùng hạng
    public const string ParentHigher = "parent_higher";         // cả hai cùng chọn, cha mẹ xếp cao hơn
    public const string ParentLower = "parent_lower";           // cả hai cùng chọn, cha mẹ xếp thấp hơn
    public const string MovedToAid = "moved_to_aid";            // theo cha mẹ chuyển sang "Cần học bổng lớn"
    public const string MovedOutOfAid = "moved_out_of_aid";     // con cần học bổng lớn, theo cha mẹ thì không
    public const string GroupChanged = "group_changed";         // cả hai cùng chọn nhưng khác nhóm (hiếm)
    public const string ParentDrops = "parent_drops";           // chỉ con có
    public const string DroppedOverBudget = "dropped_over_budget"; // chỉ con có, bị loại vì vượt ngân sách cha mẹ
    public const string ParentAdds = "parent_adds";             // chỉ cha mẹ có
}

/// <param name="StudentGroup">Khối trong danh sách của con (reach/match/safety/aid/other), null nếu con không có.</param>
/// <param name="StudentRank">Hạng trong khối của con, từ 1.</param>
public record ComparedSchool(
    string Code, string? Name, string? StudentGroup, int? StudentRank, string? ParentGroup, int? ParentRank, string Label, string? Reason);

public record CompareCounts(int Both, int StudentOnly, int ParentOnly);

/// <param name="Kept">Số trường của con qua bộ lọc cứng (bang, loại trường, ngân sách).</param>
/// <param name="SameNote">Câu "cùng danh sách" khi <paramref name="SameResult"/>, null nếu khác.</param>
public record PreferenceComparison(
    IReadOnlyList<ComparedSchool> Schools, CompareCounts Counts, bool SameResult, int Kept, string? SameNote);

/// <summary>[UC-76] So danh sách của con với danh sách theo cha mẹ, cả hai cùng khuôn ItemsJson của RecommendationService.
/// Lớp thuần: không gọi DB, không gọi LLM. Lý do lệch dựng từ số thật của trường.</summary>
public static class PreferenceComparer
{
    // Thứ tự khối như trang Gợi ý trường
    private static readonly string[] Groups = ["reach", "match", "safety", "aid", "other"];

    // Thứ tự 3 thanh ưu tiên [chi phí, học bổng, chất lượng] và khóa điểm tương ứng trong scored
    private static readonly string[] FactorKeys = ["f", "h", "q"];

    // Chênh dưới mức này coi như không đáng kể (điểm f/h/q thang 0..1)
    private const double MinContribution = 0.01;

    public static PreferenceComparison Compare(
        JsonElement studentItems, JsonElement parentItems,
        IReadOnlyList<int> studentPref, IReadOnlyList<int> parentPref, decimal? parentBudgetUsd)
    {
        var student = Positions(studentItems);
        var parent = Positions(parentItems);
        var studentScored = Scored(studentItems);
        var parentScored = Scored(parentItems);
        var diff = Enumerable.Range(0, 3).Select(i => parentPref[i] - studentPref[i]).ToArray();

        var schools = new List<ComparedSchool>();

        // Danh sách của con trước (đúng thứ tự con thấy), rồi các trường chỉ cha mẹ có
        foreach (var (code, s) in student)
        {
            var name = NameOf(code, studentScored, parentScored);
            if (parent.TryGetValue(code, out var p))
            {
                var label = BothLabel(s, p);
                var reason = label switch
                {
                    CompareLabels.Both => null,
                    CompareLabels.ParentHigher => FactorReason(code, up: true, p.Group, parent, parentScored, diff, parentBudgetUsd),
                    CompareLabels.ParentLower => FactorReason(code, up: false, p.Group, parent, parentScored, diff, parentBudgetUsd),
                    CompareLabels.MovedToAid => BudgetReason(Entry(code, parentScored), parentBudgetUsd)
                                                ?? "Theo ngân sách cha mẹ, trường cần học bổng lớn mới đi được",
                    CompareLabels.MovedOutOfAid => "Ngân sách cha mẹ đủ cho trường này, không cần học bổng lớn",
                    _ => "Theo ngân sách cha mẹ, trường chuyển nhóm",
                };
                schools.Add(new(code, name, s.Group, s.Rank, p.Group, p.Rank, label, reason));
            }
            else
            {
                var over = OverParentBudget(Entry(code, studentScored), parentBudgetUsd) && !parentScored.ContainsKey(code);
                var label = over ? CompareLabels.DroppedOverBudget : CompareLabels.ParentDrops;
                var reason = over
                    ? BudgetReason(Entry(code, studentScored), parentBudgetUsd)
                    : FactorReason(code, up: false, s.Group, student, studentScored, diff, parentBudgetUsd);
                schools.Add(new(code, name, s.Group, s.Rank, null, null, label, reason));
            }
        }

        foreach (var (code, p) in parent)
        {
            if (student.ContainsKey(code)) continue;
            var reason = FactorReason(code, up: true, p.Group, parent, parentScored, diff, parentBudgetUsd);
            schools.Add(new(code, NameOf(code, studentScored, parentScored), null, null, p.Group, p.Rank, CompareLabels.ParentAdds, reason));
        }

        var studentOnly = schools.Count(x => x.ParentGroup is null);
        var parentOnly = schools.Count(x => x.StudentGroup is null);
        var counts = new CompareCounts(schools.Count - studentOnly - parentOnly, studentOnly, parentOnly);
        var same = studentOnly == 0 && parentOnly == 0;
        var kept = studentScored.Count;
        var note = same
            ? $"Ý cha mẹ và con cho ra cùng danh sách. Hồ sơ của con còn {kept} trường sau bộ lọc (bang, loại trường, ngân sách), nên ưu tiên ít ảnh hưởng."
            : null;
        return new PreferenceComparison(schools, counts, same, kept, note);
    }

    // ---------------------------------------------------------------- đọc khuôn ItemsJson

    private sealed record Position(string Group, int Rank);

    // Mã trường → khối và hạng (từ 1) theo thứ tự các khối. Thiếu khối hay sai kiểu thì bỏ qua.
    private static Dictionary<string, Position> Positions(JsonElement items)
    {
        var map = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase);
        if (items.ValueKind != JsonValueKind.Object) return map;
        foreach (var group in Groups)
        {
            if (!items.TryGetProperty(group, out var block) || block.ValueKind != JsonValueKind.Array) continue;
            var rank = 0;
            foreach (var e in block.EnumerateArray())
            {
                rank++;
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("code", out var c) && c.GetString() is { } code)
                    map.TryAdd(code, new Position(group, rank));
            }
        }
        return map;
    }

    private static Dictionary<string, JsonElement> Scored(JsonElement items)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (items.ValueKind != JsonValueKind.Object || !items.TryGetProperty("scored", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return map;
        foreach (var e in arr.EnumerateArray())
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("code", out var c) && c.GetString() is { } code)
                map.TryAdd(code, e);
        return map;
    }

    private static JsonElement? Entry(string code, Dictionary<string, JsonElement> scored) =>
        scored.TryGetValue(code, out var e) ? e : null;

    private static string? NameOf(string code, Dictionary<string, JsonElement> a, Dictionary<string, JsonElement> b) =>
        Str(Entry(code, a), "name") ?? Str(Entry(code, b), "name");

    private static string? Str(JsonElement? e, string key) =>
        e is { } x && x.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement? e, string key) =>
        e is { } x && x.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    // ---------------------------------------------------------------- nhãn và lý do

    private static string BothLabel(Position s, Position p)
    {
        if (s.Group == p.Group)
            return p.Rank == s.Rank ? CompareLabels.Both : p.Rank < s.Rank ? CompareLabels.ParentHigher : CompareLabels.ParentLower;
        if (p.Group == "aid") return CompareLabels.MovedToAid;
        if (s.Group == "aid") return CompareLabels.MovedOutOfAid;
        return CompareLabels.GroupChanged;
    }

    private static bool OverParentBudget(JsonElement? entry, decimal? parentBudgetUsd) =>
        parentBudgetUsd is decimal b && Num(entry, "cost") is double cost && (decimal)cost > b;

    private static string? BudgetReason(JsonElement? entry, decimal? parentBudgetUsd) =>
        parentBudgetUsd is decimal b && Num(entry, "cost") is double cost && (decimal)cost > b
            ? $"Tổng chi phí {Money((decimal)cost)} USD/năm, vượt ngân sách cha mẹ chấp nhận {Money(b)} USD"
            : null;

    // Chọn yếu tố cha mẹ và con chênh nhau nhiều nhất mà làm trường lên (up) hoặc xuống:
    // đóng góp = (mức cha mẹ − mức con) × (điểm của trường − điểm trung bình khối). Lên cần đóng góp dương, xuống cần âm.
    private static string FactorReason(
        string code, bool up, string group, Dictionary<string, Position> list, Dictionary<string, JsonElement> scored,
        int[] diff, decimal? parentBudgetUsd)
    {
        var entry = Entry(code, scored);
        var peers = list.Where(kv => kv.Value.Group == group).Select(kv => Entry(kv.Key, scored)).ToList();

        int best = -1;
        double bestSize = MinContribution;
        for (var i = 0; i < 3; i++)
        {
            if (diff[i] == 0 || Num(entry, FactorKeys[i]) is not double value) continue;
            var vals = peers.Select(p => Num(p, FactorKeys[i])).OfType<double>().ToList();
            if (vals.Count == 0) continue;
            var contribution = diff[i] * (value - vals.Average());
            var signed = up ? contribution : -contribution;
            if (signed > bestSize) (best, bestSize) = (i, signed);
        }

        // Chiều của trường so với khối: điểm trên trung bình khối = "tốt hơn" ở yếu tố đó
        var better = best >= 0 && (diff[best] > 0) == up;
        return best switch
        {
            0 => CostReason(entry, better, parentBudgetUsd),
            1 => AidReason(entry),
            2 => better ? "Điểm chất lượng cao hơn các trường cùng nhóm" : "Điểm chất lượng thấp hơn các trường cùng nhóm",
            _ => "Thứ tự trong nhóm đổi nhẹ theo ưu tiên",
        };
    }

    private static string CostReason(JsonElement? entry, bool cheaper, decimal? parentBudgetUsd)
    {
        if (Num(entry, "cost") is not double cost) return cheaper ? "Rẻ hơn các trường cùng nhóm" : "Đắt hơn các trường cùng nhóm";
        var total = $"Tổng chi phí {Money((decimal)cost)} USD/năm";
        if (cheaper) return $"{total}, rẻ hơn, trong ngân sách";
        if (BudgetReason(entry, parentBudgetUsd) is { } over) return over;
        if (Num(entry, "gap") is double gap && gap > 0) return $"{total}, vượt ngân sách {Money((decimal)(cost - gap))} USD";
        return $"{total}, đắt hơn các trường cùng nhóm";
    }

    private static string AidReason(JsonElement? entry)
    {
        if (string.Equals(Str(entry, "aidPolicy"), "none", StringComparison.OrdinalIgnoreCase))
            return "Trường không cấp học bổng cho du học sinh";
        if (Num(entry, "aidAvg") is double avg && avg > 0)
            return $"Học bổng du học sinh trung bình {Money((decimal)avg)} USD/năm";
        return "Mức học bổng cho du học sinh khác các trường cùng nhóm";
    }

    private static string Money(decimal v) =>
        ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
}
