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

    public const string Busy = "Phần gợi ý trường đang bận, bạn thử lại sau ít phút hoặc mở trang Gợi ý trường nhé.";
    public const string Invalid = "Mình chưa tạo được danh sách trường hợp lệ, bạn thử lại sau ít phút nhé.";
    public const string Empty =
        "Mình chưa tìm được trường nào khớp với hồ sơ hiện tại. Bạn mở trang Gợi ý trường để xem lý do và điều chỉnh tiêu chí nhé.";
    public const string More = "Bạn xem chi tiết và lý do từng trường ở trang Gợi ý trường.";

    // Nhóm theo đúng thứ tự và tên trên trang Gợi ý trường (RecommendationView.tsx)
    private static readonly (string Key, string Title, int Max)[] Groups =
    [
        ("reach", "Khó vào", 3),
        ("match", "Vừa sức", 5),
        ("safety", "An toàn hơn", 4),
        ("aid", "Chỉ đi được nếu có học bổng lớn", 3),
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

        var lines = new List<string>();
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
            var shown = codes.Take(max).Select(c => names.GetValueOrDefault(c, c));
            var rest = codes.Count - max;
            lines.Add($"- {title}: {string.Join(", ", shown)}" + (rest > 0 ? $" và {rest} trường khác" : ""));
        }

        if (lines.Count == 0)
            return Empty;
        return "Mình đã tìm được các trường phù hợp với hồ sơ của bạn:\n" + string.Join("\n", lines) + "\n" + More;
    }

    /// <summary>
    /// Các trường học sinh đang thấy (4 nhóm hiển thị, đủ danh sách không cắt), theo thứ tự trang Gợi ý trường.
    /// Gửi cho advisor mỗi lượt để skill_explain biết học sinh hỏi trường nào.
    /// </summary>
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
