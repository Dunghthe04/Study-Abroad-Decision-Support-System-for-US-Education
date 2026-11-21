using System.Text.Json;

namespace StudyAbroad.Application.Billing.Payments;

/// <summary>Đọc body webhook payOS <c>{ code, desc, success, data, signature }</c> thành <see cref="VerifiedWebhook"/>.</summary>
public static class PayOsWebhook
{
    /// <summary>Kiểm chữ ký bằng checksum key rồi đọc. Null nếu body hỏng hoặc chữ ký sai.</summary>
    public static VerifiedWebhook? Verify(string rawBody, string checksumKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;
            var signature = root.TryGetProperty("signature", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            return PayOsSignature.Verify(data, signature, checksumKey) ? Read(root, data) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Đọc không kiểm chữ ký: chỉ gọi sau khi chữ ký đã được kiểm bằng cách khác (SDK payOS).
    /// </summary>
    public static VerifiedWebhook? ReadAlreadyVerified(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                ? Read(root, data)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static VerifiedWebhook? Read(JsonElement root, JsonElement data)
    {
        if (Integer(data, "orderCode") is not { } orderCode || Integer(data, "amount") is not { } amountVnd)
            return null;

        // Chữ ký chỉ phủ data nên "thành công" cần cả mã ngoài lẫn mã trong data đều "00" (không để mã ngoài tự đổi thành 00)
        var outer = Text(root, "code");
        var inner = Text(data, "code");
        var success = outer == VerifiedWebhook.SuccessCode && inner == VerifiedWebhook.SuccessCode;
        var resultCode = success ? VerifiedWebhook.SuccessCode : (outer != VerifiedWebhook.SuccessCode ? outer : inner) ?? string.Empty;
        return new VerifiedWebhook(orderCode, amountVnd, resultCode, Text(data, "reference"), PayOsSignature.Sanitize(data));
    }

    // TryGetInt64 ném lỗi khi giá trị không phải số, nên kiểm kiểu trước
    private static long? Integer(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

    private static string? Text(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
