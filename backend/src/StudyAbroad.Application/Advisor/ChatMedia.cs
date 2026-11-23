using System.Text.Json;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Ảnh minh họa của câu trả lời. Link do advisor lấy từ cơ sở dữ liệu nhưng .NET vẫn kiểm lại trước khi lưu và trả trình duyệt
/// (lịch sử chat phát lại đúng dữ liệu này): chỉ https, có tên miền, không khoảng trắng, không trùng, tối đa MaxItems ảnh.
/// </summary>
public static class ChatMedia
{
    public const int MaxItems = 3;
    public const int MaxUrlLength = 2000;
    public const int MaxAltLength = 300;
    public const string ImageKind = "image";
    public const string DefaultAlt = "Ảnh minh họa";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Danh sách đã kiểm; null nếu không còn ảnh hợp lệ nào (trình duyệt không nhận mảng rỗng khác nghĩa).</summary>
    public static IReadOnlyList<MediaItem>? Clean(IReadOnlyList<MediaItem>? items)
    {
        if (items is null || items.Count == 0)
            return null;
        var clean = new List<MediaItem>();
        foreach (var item in items)
        {
            if (item is null || !string.Equals(item.Kind, ImageKind, StringComparison.Ordinal) || !IsHttps(item.Url))
                continue;
            var alt = (item.Alt ?? "").Trim();
            alt = alt.Length == 0 ? DefaultAlt : alt.Length > MaxAltLength ? alt[..MaxAltLength] : alt;
            var cleaned = new MediaItem(ImageKind, item.Url.Trim(), alt);
            if (!clean.Any(c => c.Url == cleaned.Url))
                clean.Add(cleaned);
            if (clean.Count == MaxItems)
                break;
        }
        return clean.Count == 0 ? null : clean;
    }

    /// <summary>JSON để lưu vào chat_messages.media_json; null nếu không có ảnh.</summary>
    public static string? ToJson(IReadOnlyList<MediaItem>? items) =>
        Clean(items) is { } clean ? JsonSerializer.Serialize(clean.Select(m => new { kind = m.Kind, url = m.Url, alt = m.Alt }), Json) : null;

    /// <summary>Đọc lại từ media_json. Dòng hỏng thì bỏ ảnh chứ không làm hỏng cả lịch sử; link đã lưu vẫn được kiểm lại.</summary>
    public static IReadOnlyList<MediaItem> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            var items = JsonSerializer.Deserialize<List<MediaItem>>(json, Json);
            return Clean(items) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsHttps(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.Length <= MaxUrlLength
        && !url.Any(char.IsWhiteSpace)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);
}
