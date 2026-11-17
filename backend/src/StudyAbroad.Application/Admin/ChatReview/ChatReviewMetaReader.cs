using System.Text.Json;

namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>Đọc nhãn model gán trong meta_json của tin học sinh (do ChatTurn ghi). Meta hỏng hoặc thiếu thì trả null, không ném lỗi.</summary>
public static class ChatReviewMetaReader
{
    /// <summary>
    /// Nhãn model gán ĐẦY ĐỦ, gồm cả đoạn chữ và thực thể của học sinh (modelIntents[].span, entities). CHỈ dùng cho chi tiết một câu,
    /// sau khi đã ghi nhật ký người xem. Null nếu không có khối slm.
    /// </summary>
    public static ChatReviewModelDetail? ReadModelDetail(string? questionMetaJson)
    {
        var root = Parse(questionMetaJson);
        if (root is not { } meta || !meta.TryGetProperty("slm", out var slm) || slm.ValueKind != JsonValueKind.Object)
            return null;

        var intents = new List<ChatReviewModelIntentDto>();
        if (slm.TryGetProperty("modelIntents", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "type") is not { } type)
                    continue;
                JsonElement? entities = item.TryGetProperty("entities", out var e) && e.ValueKind == JsonValueKind.Object ? e : null;
                intents.Add(new ChatReviewModelIntentDto(type, Text(item, "span") ?? "", entities));
            }
        }

        return new ChatReviewModelDetail(
            Number(slm, "confidence"),
            slm.TryGetProperty("fallback", out var fallback) && fallback.ValueKind == JsonValueKind.True,
            Text(slm, "outOfScope"),
            Strings(slm, "intents"),
            intents);
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

    private static double? Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string> Strings(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
}
