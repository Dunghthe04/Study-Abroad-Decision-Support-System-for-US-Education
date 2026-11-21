using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Gợi ý trường trong chat: advisor trả action "recommend" khi lộ trình là đại học thẳng,
/// ChatService chạy pipeline gợi ý có sẵn rồi dùng các hàm ở đây để tóm tắt kết quả và bật hasResults.
/// </summary>
public static class ChatRecommendation
{
    public const string Action = "recommend";
    /// <summary>Widget hiện nút "Xem đầy đủ" mở trang Gợi ý trường.</summary>
    public const string WidgetType = "recommend";

    public const string More = "Bạn xem chi tiết và lý do từng trường ở trang Gợi ý trường.";

    // Nhóm theo đúng thứ tự và tên trên trang Gợi ý trường (RecommendationView.tsx)
    private static readonly (string Key, string Title, int Max)[] Groups =
    [
        ("reach", "Khó vào", 4),
        ("match", "Vừa sức", 4),
        ("safety", "An toàn hơn", 4),
        ("aid", "Chỉ đi được nếu có học bổng lớn", 4),
    ];


    /// Nhận kết quả recommendation trả về chuỗi hiển thị chat
    public static string Summarize(Recommendation recommendation)
    {
        // Đọc kết quả
        var items = JsonNode.Parse(recommendation.ItemsJson) as JsonObject;
        // Lấy ra scored chấm các trường
        var names = (items?["scored"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);

        var lines = ShownGroups(items, names).Select(g => $"- {GroupLine(g)}").ToList();

        if (lines.Count == 0)
            return AdvisorMessages.RecommendationEmpty;
        return "Mình đã tìm được các trường phù hợp với hồ sơ của bạn:\n" + string.Join("\n", lines) + "\n" + More;
    }

    // Một nhóm trên màn hình: tên nhóm, các trường đang hiện (tối đa Max) và số trường còn lại chưa hiện
    private sealed record ShownGroup(string Title, IReadOnlyList<string> Shown, IReadOnlyList<string> Hidden, int Total)
    {
        public int Rest => Hidden.Count;
    }

    private static List<ShownGroup> ShownGroups(JsonObject? items, IReadOnlyDictionary<string, string> names)
    {
        var groups = new List<ShownGroup>();
        foreach (var (key, title, max) in Groups)
        {
            // Lấy từng nhóm ra
            var codes = (items?[key] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(e => e["code"]?.GetValue<string>())
                .OfType<string>()
                .ToList();
            if (codes.Count == 0)
                continue;
            var shown = codes.Take(max).Select(c => names.GetValueOrDefault(c, c)).ToList();
            var hidden = codes.Skip(max).Select(c => names.GetValueOrDefault(c, c)).ToList();
            groups.Add(new ShownGroup(title, shown, hidden, codes.Count));
        }
        return groups;
    }

    private static string GroupLine(ShownGroup g) =>
        $"{g.Title}: {string.Join(", ", g.Shown)}" + (g.Rest > 0 ? $" và {g.Rest} trường khác" : "");

    /// <summary>
    /// Advisor đặt action_data.note = true khi học sinh chỉ xin danh sách trường, tức chưa có lời nào do Claude viết.
    /// Lúc đó danh sách được gửi cho Claude viết lời. Câu có cả đánh giá hồ sơ thì lời đã do Claude viết, danh sách chỉ nối thêm.
    /// </summary>
    public static bool NoteRequested(JsonElement? actionData) =>
        actionData is { ValueKind: JsonValueKind.Object } data
        && data.TryGetProperty("note", out var note)
        && note.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Dữ kiện gửi Claude viết lời cho danh sách: tổng số trường, mỗi nhóm một dòng (đúng chữ như câu ghép bằng code),
    /// tên các trường đang hiện, và tên các trường đã chấm nhưng không hiện để chặn nhắc tên lạ. Null khi chưa có trường nào.
    /// </summary>
    public static RecommendNoteRequestDto? Note(Recommendation recommendation)
    {
        var items = JsonNode.Parse(recommendation.ItemsJson) as JsonObject;
        var names = (items?["scored"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var groups = ShownGroups(items, names);
        if (groups.Count == 0)
            return null;

        var facts = new List<string> { $"Tổng cộng {groups.Sum(g => g.Total)} trường phù hợp với hồ sơ của bạn." };
        facts.AddRange(groups.Select(GroupLine));
        var schools = groups.SelectMany(g => g.Shown).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Chặn nhắc tên: trường cùng nhóm nhưng chưa hiện (lời chỉ nói "và N trường khác") và trường không nằm trong nhóm nào
        var others = groups.SelectMany(g => g.Hidden).Concat(OtherSchools(recommendation).Select(s => s.Name))
            .Where(n => !schools.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxOthers)
            .ToList();
        return new RecommendNoteRequestDto(schools, facts, others);
    }

    private const int MaxOthers = 400; // trần của advisor (RecommendNoteRequest.others)

    /// <summary>
    /// Các trường học sinh đang thấy (4 nhóm hiển thị, đủ danh sách không cắt), theo thứ tự trang Gợi ý trường.
    /// Gửi cho advisor mỗi lượt để skill_explain biết học sinh hỏi trường nào.
    /// </summary>
    /// <summary>
    /// Các trường đã chấm nhưng KHÔNG nằm trong khối nào đang hiện (mỗi nhóm chỉ hiện tối đa vài trường). Advisor chỉ dùng
    /// để khớp tên khi học sinh hỏi một trường cụ thể ("vì sao Texas Austin Khó vào"), không dùng để đếm vị trí ("trường thứ ba").
    /// </summary>
    public static IReadOnlyList<ChatSchool> OtherSchools(Recommendation recommendation)
    {
        var shown = Schools(recommendation).Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = JsonNode.Parse(recommendation.ItemsJson) as JsonObject;
        return (items?["scored"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .Select(s => new ChatSchool(s["code"]!.GetValue<string>(), s["name"]!.GetValue<string>(), s["g2"]?.GetValue<string>()))
            .Where(s => shown.Add(s.Code))
            .ToList();
    }

    public static IReadOnlyList<ChatSchool> Schools(Recommendation recommendation)
    {
        var items = JsonNode.Parse(recommendation.ItemsJson) as JsonObject;
        var scored = (items?["scored"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<ChatSchool>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, _, _) in Groups)
        {
            var codes = (items?[key] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(e => e["code"]?.GetValue<string>())
                .OfType<string>();
            foreach (var code in codes)
                if (scored.TryGetValue(code, out var row) && seen.Add(code))
                    result.Add(new ChatSchool(code, row["name"]!.GetValue<string>(), row["g2"]?.GetValue<string>()));
        }
        return result;
    }

    /// <summary>Bật hasResults trong trạng thái chat, để skill_explain, skill_kich_ban được chạy ở lượt sau.</summary>
    public static JsonElement? WithResults(JsonElement? state)
    {
        if (state is not { ValueKind: JsonValueKind.Object } s)
            return state;
        var node = JsonNode.Parse(s.GetRawText())!.AsObject();
        node["hasResults"] = true;
        return JsonSerializer.SerializeToElement(node);
    }
}
