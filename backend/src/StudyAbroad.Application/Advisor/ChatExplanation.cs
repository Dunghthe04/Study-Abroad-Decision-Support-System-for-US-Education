using System.Text.Json;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Hỏi giải thích một trường trong chat: advisor trả action "explain" kèm mã trường (ActionData.code),
/// ChatService gọi phần tư vấn từng trường có sẵn (RecommendationService.ExplainAsync) rồi dùng các hàm ở đây dựng câu trả lời.
/// </summary>
public static class ChatExplanation
{
    public const string Action = "explain";

    public const string Busy = "Phần tư vấn trường đang bận, bạn thử lại sau ít phút nhé.";
    public const string Failed = "Mình chưa viết được lời tư vấn cho trường này, bạn thử lại sau ít phút nhé.";

    /// <summary>Mã trường advisor chọn (ActionData.code), null nếu không có.</summary>
    public static string? CodeOf(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } d
        && d.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;

    public const string WidgetType = "explain";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Thẻ giải thích do code dựng (nhóm, lý do, số liệu) cho khung chat; lời tư vấn do AI viết hiện sau, bên dưới thẻ.
    /// card là số đã lưu của trường (camelCase): g2, blk, why, acc, sat25, sat75, pg, ps, cost, gap.
    /// </summary>
    // basis: gốc của lời giải thích. Mô tả giả định ("GPA 3.6, SAT 1500") nếu học sinh đang xem giả định, null nếu là hồ sơ thật
    public static Widget WidgetOf(string code, ExplainSchoolDto card, string? basis = null) =>
        new(WidgetType, JsonSerializer.SerializeToElement(
            new { code, card, basis = new { kind = basis is null ? "real" : "simulated", label = basis } }, WebJson));

    public static string Format(SchoolExplanation explanation)
    {
        var lines = new List<string> { $"{explanation.Name}: {explanation.Summary}" };
        if (explanation.Strengths.Count > 0)
        {
            lines.Add("");
            lines.Add("Điểm mạnh của hồ sơ với trường này:");
            lines.AddRange(explanation.Strengths.Select(s => $"- {s}"));
        }
        if (explanation.Improvements.Count > 0)
        {
            lines.Add("");
            lines.Add("Bạn có thể cải thiện:");
            lines.AddRange(explanation.Improvements.Select(s => $"- {s}"));
        }
        return string.Join("\n", lines);
    }
}
