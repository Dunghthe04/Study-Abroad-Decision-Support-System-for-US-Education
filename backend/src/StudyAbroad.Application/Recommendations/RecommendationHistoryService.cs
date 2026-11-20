using System.Globalization;
using System.Text.Json.Nodes;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

// Số trường mỗi nhóm của một lần chạy
public record RunGroupCounts(int Reach, int Match, int Safety, int Aid);

// Một dòng trong danh sách lịch sử: ngày chạy, nút ưu tiên, ngành, số trường mỗi nhóm
public record RecommendationRunSummaryDto(
    Guid Id, DateTime CreatedAt, IReadOnlyList<int>? Pref, string? Major, RunGroupCounts Counts);

// Một trường thay đổi giữa hai lần chạy. Group null = không có trong lần đó (mới vào / rơi ra). Rank tính từ 1 trong nhóm.
public record RunSchoolChange(string Code, string Name, string? FromGroup, string? ToGroup, int? FromRank, int? ToRank);

// Một mục đầu vào khác nhau giữa hai lần chạy (đọc từ criteria_json). Giá trị đã ghi thành chữ để hiện thẳng; lần chạy cũ thiếu khóa thì "Không rõ".
public record RunInputChange(string Key, string Label, string From, string To);

public record RunComparisonDto(
    RecommendationRunSummaryDto From, RecommendationRunSummaryDto To, IReadOnlyList<RunSchoolChange> Changes,
    IReadOnlyList<RunInputChange> InputChanges);

/// <summary>Xem lịch sử gợi ý (UC-87): các lần chạy trước và so sánh hai lần. Chỉ đọc bảng recommendations.
/// Học sinh xem của mình; phụ huynh có liên kết đang hiệu lực xem của con. Không tìm thấy hoặc không có quyền đều trả null (controller trả 404).</summary>
public class RecommendationHistoryService(IRecommendationHistoryRepository history, IParentLinkRepository links)
{
    public const int MaxRuns = 50;

    // Thứ tự nhóm khi hiển thị và so sánh. "other" không tính: đó là trường ngoài 4 nhóm chính.
    private static readonly string[] Groups = ["reach", "match", "safety", "aid"];

    // studentId bỏ trống: xem của chính mình. Có studentId của người khác: chỉ phụ huynh có liên kết đang hiệu lực.
    public async Task<IReadOnlyList<RecommendationRunSummaryDto>?> ListAsync(Guid viewerId, Guid? studentId, CancellationToken ct)
    {
        var ownerId = studentId ?? viewerId;
        if (!await CanViewAsync(viewerId, ownerId, ct))
            return null;

        var rows = await history.ListByUserAsync(ownerId, MaxRuns, ct);
        return rows.Select(Parse).OfType<ParsedRun>().Select(r => r.Summary).ToList();
    }

    // Một lần chạy đầy đủ (controller trả cùng khuôn với "latest"). Dòng khuôn cũ coi như không có.
    public async Task<Recommendation?> GetAsync(Guid viewerId, Guid id, CancellationToken ct)
    {
        var row = await history.GetByIdAsync(id, ct);
        return row is not null && Parse(row) is not null && await CanViewAsync(viewerId, row.UserId, ct) ? row : null;
    }

    // So sánh hai lần chạy của cùng một học sinh. Chỉ trả các trường có thay đổi.
    public async Task<RunComparisonDto?> CompareAsync(Guid viewerId, Guid fromId, Guid toId, CancellationToken ct)
    {
        var fromRow = await history.GetByIdAsync(fromId, ct);
        var toRow = await history.GetByIdAsync(toId, ct);
        if (fromRow is null || toRow is null || fromRow.UserId != toRow.UserId)
            return null;
        if (!await CanViewAsync(viewerId, fromRow.UserId, ct))
            return null;
        if (Parse(fromRow) is not ParsedRun from || Parse(toRow) is not ParsedRun to)
            return null;

        var codes = from.Places.Keys.Union(to.Places.Keys, StringComparer.OrdinalIgnoreCase);
        var changes = new List<RunSchoolChange>();
        foreach (var code in codes)
        {
            var before = from.Places.GetValueOrDefault(code);
            var after = to.Places.GetValueOrDefault(code);
            if (before == after)
                continue;   // cùng nhóm, cùng hạng: không đổi
            var name = to.Names.GetValueOrDefault(code) ?? from.Names.GetValueOrDefault(code) ?? code;
            changes.Add(new RunSchoolChange(code, name, before?.Group, after?.Group, before?.Rank, after?.Rank));
        }

        // Xếp theo nhóm và hạng ở lần sau; trường rơi ra để cuối, theo nhóm và hạng ở lần trước
        var ordered = changes
            .OrderBy(c => c.ToGroup is null)
            .ThenBy(c => Array.IndexOf(Groups, c.ToGroup ?? c.FromGroup))
            .ThenBy(c => c.ToRank ?? c.FromRank)
            .ToList();
        return new RunComparisonDto(from.Summary, to.Summary, ordered, InputChanges(from.Criteria, to.Criteria));
    }

    private async Task<bool> CanViewAsync(Guid viewerId, Guid ownerId, CancellationToken ct) =>
        viewerId == ownerId
        || (await links.GetActiveByParentAsync(viewerId, ct)).Any(l => l.StudentUserId == ownerId);

    // Vị trí của một trường trong một lần chạy
    private record Place(string Group, int Rank);

    private record ParsedRun(
        RecommendationRunSummaryDto Summary,
        Dictionary<string, Place> Places,
        Dictionary<string, string> Names,
        JsonObject? Criteria);

    // Đọc một dòng. Trả null với dòng khuôn cũ (items là mảng, lưu trước PR #19) hoặc JSON hỏng.
    private static ParsedRun? Parse(Recommendation row)
    {
        JsonObject? items, criteria;
        try
        {
            items = JsonNode.Parse(row.ItemsJson) as JsonObject;
            criteria = JsonNode.Parse(row.CriteriaJson) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        if (items is null)
            return null;

        var places = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
        var counts = new int[Groups.Length];
        for (var g = 0; g < Groups.Length; g++)
        {
            if (items[Groups[g]] is not JsonArray block)
                continue;
            var rank = 0;
            foreach (var entry in block)
            {
                if (Text(entry?["code"]) is not { Length: > 0 } code)
                    continue;
                rank++;
                places.TryAdd(code, new Place(Groups[g], rank));
            }
            counts[g] = rank;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (items["scored"] is JsonArray scored)
            foreach (var s in scored)
                if (Text(s?["code"]) is { Length: > 0 } code && Text(s?["name"]) is { Length: > 0 } name)
                    names.TryAdd(code, name);

        var pref = criteria?["pref"] is JsonArray p
            ? p.Select(n => n is JsonValue v && v.TryGetValue<int>(out var level) ? level : 0).ToList()
            : null;
        var major = Text(criteria?["major"]);

        var summary = new RecommendationRunSummaryDto(
            row.Id, row.CreatedAt, pref, major, new RunGroupCounts(counts[0], counts[1], counts[2], counts[3]));
        return new ParsedRun(summary, places, names, criteria);
    }

    // ------------------------------------------------------------ đầu vào đã đổi (UC-76 2h-2)

    private const string Unknown = "Không rõ";
    private static readonly string[] PrefNames = ["chi phí thấp", "học bổng nhiều", "danh tiếng"];
    private static readonly string[] PrefLevels = ["Không quan tâm", "Ít quan tâm", "Bình thường", "Quan tâm nhiều", "Quan tâm nhất"];
    private static readonly Dictionary<string, string> AidNeedLabels = new()
    {
        ["mandatory"] = "Bắt buộc", ["preferred"] = "Nên có", ["optional"] = "Không cần",
    };
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    // So các mục đầu vào mà lần chạy đã lưu, theo thứ tự hồ sơ. Chỉ trả mục khác nhau.
    // Điểm tiếng Anh không được lưu trong criteria_json nên không so được.
    private static List<RunInputChange> InputChanges(JsonObject? a, JsonObject? b)
    {
        var list = new List<RunInputChange>();
        void Add(string key, string label, Func<JsonObject, string> read)
        {
            var from = a is null ? Unknown : read(a);
            var to = b is null ? Unknown : read(b);
            if (from != to)
                list.Add(new RunInputChange(key, label, from, to));
        }
        // Thiếu khóa (lần chạy lưu trước khi có khóa đó) khác với khóa có mà rỗng
        static Func<JsonObject, string> Field(string key, Func<JsonNode?, string> fmt) =>
            o => o.ContainsKey(key) ? fmt(o[key]) : Unknown;

        Add("studentGpa", "GPA", Field("studentGpa", n => Number(n) is double g ? g.ToString("0.##", Vi) : "Chưa có"));
        Add("studentSat", "SAT", Field("studentSat", n => Number(n) is double v ? ((long)v).ToString(CultureInfo.InvariantCulture) : "Chưa có"));
        Add("tier", "Ngoại khóa", Field("tier", n => Number(n) is double t ? $"Bậc {(int)t}" : "Chưa có"));
        Add("major", "Ngành", Field("major", n => Text(n) is { Length: > 0 } m ? m : "Chưa chọn"));
        Add("states", "Bang", Field("states", n =>
            n is JsonArray arr && arr.Select(Text).OfType<string>().Order(StringComparer.Ordinal).ToList() is { Count: > 0 } st
                ? string.Join(", ", st) : "Mọi bang"));
        Add("budget", "Ngân sách", Field("budget", n => Number(n) is double v
            ? ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " USD/năm" : "Không đặt"));
        Add("scholarshipNeed", "Nhu cầu học bổng", Field("scholarshipNeed",
            n => Text(n) is { } need ? AidNeedLabels.GetValueOrDefault(need, need) : "Chưa chọn"));
        for (var i = 0; i < PrefNames.Length; i++)
        {
            var at = i;
            Add($"pref.{at}", $"Ưu tiên {PrefNames[at]}", o =>
                o["pref"] is JsonArray p && p.Count == PrefNames.Length && Number(p[at]) is double lv && lv >= 0 && lv < PrefLevels.Length
                    ? PrefLevels[(int)lv] : Unknown);
        }
        return list;
    }

    private static double? Number(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
