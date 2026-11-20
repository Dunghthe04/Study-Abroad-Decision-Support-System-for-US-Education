using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StudyAbroad.Application.Billing.Payments;

/// <summary>
/// Chữ ký webhook payOS: HMAC-SHA256 (hex thường) bằng checksum key trên các trường của <c>data</c> xếp theo tên, nối key=value bằng &amp;.
/// Chỉ <c>data</c> được ký, nên mã thành công phải lấy từ trong data.
/// </summary>
public static class PayOsSignature
{
    private static readonly JavaScriptEncoder Relaxed = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>key=value nối bằng &amp;, key xếp theo thứ tự ordinal; null thành rỗng, bool true/false, số theo invariant, object/mảng là JSON gọn.</summary>
    public static string DataString(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return string.Empty;
        return string.Join('&', data.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={ValueString(p.Value)}"));
    }

    public static string Sign(JsonElement data, string checksumKey) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksumKey), Encoding.UTF8.GetBytes(DataString(data)))).ToLowerInvariant();

    public static bool Verify(JsonElement data, string? signature, string checksumKey)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(checksumKey))
            return false;
        var expected = Encoding.UTF8.GetBytes(Sign(data, checksumKey));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);   // khác độ dài thì false
    }

    /// <summary>data bỏ các trường counterAccount* và virtualAccount* (thông tin tài khoản người chuyển), dạng JSON gọn.</summary>
    public static string Sanitize(JsonElement data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = Relaxed }))
        {
            writer.WriteStartObject();
            if (data.ValueKind == JsonValueKind.Object)
                foreach (var p in data.EnumerateObject())
                    if (!p.Name.StartsWith("counterAccount", StringComparison.Ordinal) && !p.Name.StartsWith("virtualAccount", StringComparison.Ordinal))
                        p.WriteTo(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string ValueString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => v.TryGetInt64(out var l) ? l.ToString(CultureInfo.InvariantCulture) : v.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(v, new JsonSerializerOptions { Encoder = Relaxed }),
    };
}
