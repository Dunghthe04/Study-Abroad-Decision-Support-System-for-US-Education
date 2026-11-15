using System.Text.Json;

namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>
/// Đọc meta_json của một lượt chat (do ChatTurn ghi) để lấy các MÃ CỐ ĐỊNH hiện trong hàng đợi. Chỉ đọc đúng những khóa liệt kê dưới đây:
/// tuyệt đối không đọc đoạn chữ hay thực thể của học sinh (modelIntents[].span, modelIntents[].entities), vì danh sách không được lộ nội dung chat.
/// Meta hỏng, thiếu hoặc khác khuôn thì trả giá trị rỗng, không bao giờ ném lỗi (một dòng cũ không được làm hỏng cả danh sách).
/// </summary>
public static class ChatReviewMetaReader
{
    /// <summary>meta_json của tin HỌC SINH: { slm: { modelIntents[].type, intents[], outOfScope, confidence, fallback } }. Null nếu không có khối slm.</summary>
    public static ChatReviewModelInfo? ReadModel(string? questionMetaJson)
    {
        var root = Parse(questionMetaJson);
        if (root is not { } meta || !meta.TryGetProperty("slm", out var slm) || slm.ValueKind != JsonValueKind.Object)
            return null;

        return new ChatReviewModelInfo(
            Number(slm, "confidence"),
            slm.TryGetProperty("fallback", out var fallback) && fallback.ValueKind == JsonValueKind.True,
            TypesOf(slm, "modelIntents"),
            Strings(slm, "intents"),
            Text(slm, "outOfScope"));
    }

    /// <summary>
    /// Nhãn model gán ĐẦY ĐỦ, gồm cả đoạn chữ và thực thể của học sinh (modelIntents[].span, entities). CHỈ dùng cho chi tiết một câu,
    /// sau khi đã ghi nhật ký người xem. Danh sách phải dùng ReadModel (chỉ mã cố định). Null nếu không có khối slm.
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

    /// <summary>meta_json của tin TRỢ LÝ hoặc dòng lỗi: failedSteps[], turnMs, error (tên loại lỗi). Thiếu thì rỗng.</summary>
    public static (IReadOnlyList<string> FailedSteps, long? TurnMs, string? Error) ReadReply(string? replyMetaJson)
    {
        var root = Parse(replyMetaJson);
        if (root is not { } meta)
            return ([], null, null);

        long? turnMs = meta.TryGetProperty("turnMs", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var ms) ? ms : null;
        return (Strings(meta, "failedSteps"), turnMs, Text(meta, "error"));
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

    // Chỉ lấy khóa "type" của từng ý; bỏ qua span và entities
    private static IReadOnlyList<string> TypesOf(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                .Select(e => e.GetProperty("type").GetString()!)
                .ToList()
            : [];
}
