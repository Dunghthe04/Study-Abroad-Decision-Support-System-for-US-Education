using System.Text.Json;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Đổi một lượt chat đã lưu (tin trả lời và tin hỏi của nó) thành AiQualityTurn để tính báo cáo.
/// Đọc meta_json do ChatTurn ghi: tin trả lời có turnMs, skills, failedSteps; tin hỏi có slm.fallback.
/// Meta thiếu hoặc hỏng thì dùng giá trị mặc định (không đo được, không có tính năng), không ném lỗi: một dòng cũ không được làm hỏng cả báo cáo.
/// </summary>
public static class AiQualityMetaReader
{
    /// <param name="replyMetaJson">meta_json của tin trả lời của trợ lý.</param>
    /// <param name="questionMetaJson">meta_json của tin hỏi mà tin trả lời đáp lại; null nếu lượt không có tin hỏi.</param>
    /// <param name="usedFallback">Lượt có lần gọi AI nào dùng đường dự phòng (ai_calls.fallback_used), do nơi lấy dữ liệu tính.</param>
    public static AiQualityTurn ToTurn(
        DateTime createdAt, string? outcome, string? replyMetaJson, string? questionMetaJson, bool usedFallback)
    {
        var reply = Parse(replyMetaJson);
        var question = Parse(questionMetaJson);
        return new AiQualityTurn(
            createdAt,
            outcome,
            reply is { } r ? Long(r, "turnMs") : null,
            reply is { } r2 ? Strings(r2, "skills").FirstOrDefault() : null,
            reply is { } r3 ? Strings(r3, "failedSteps") : [],
            question is { } q && q.TryGetProperty("slm", out var slm) && slm.ValueKind == JsonValueKind.Object
                && slm.TryGetProperty("fallback", out var fallback) && fallback.ValueKind == JsonValueKind.True,
            usedFallback);
    }

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? Long(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;

    private static IReadOnlyList<string> Strings(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
}
