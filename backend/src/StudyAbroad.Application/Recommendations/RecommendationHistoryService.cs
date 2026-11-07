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

public record RunComparisonDto(RecommendationRunSummaryDto From, RecommendationRunSummaryDto To, IReadOnlyList<RunSchoolChange> Changes);

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
        return new RunComparisonDto(from.Summary, to.Summary, ordered);
    }

    private async Task<bool> CanViewAsync(Guid viewerId, Guid ownerId, CancellationToken ct) =>
        viewerId == ownerId
        || (await links.GetActiveByParentAsync(viewerId, ct)).Any(l => l.StudentUserId == ownerId);

    // Vị trí của một trường trong một lần chạy
    private record Place(string Group, int Rank);

    private record ParsedRun(
        RecommendationRunSummaryDto Summary,
        Dictionary<string, Place> Places,
        Dictionary<string, string> Names);

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
        return new ParsedRun(summary, places, names);
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
