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
