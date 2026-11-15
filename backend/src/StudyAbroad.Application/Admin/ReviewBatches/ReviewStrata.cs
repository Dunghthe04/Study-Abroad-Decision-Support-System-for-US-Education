using System.Text.Json;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>
/// Nhóm (tầng) của một câu trả lời khi bốc mẫu, lấy từ skill ghi trong meta_json của tin trả lời. Chia nhóm để nhóm ít gặp
/// (ví dụ chuyển trung tâm) vẫn có mặt trong mẫu thay vì bị nhóm lớn lấn át.
/// </summary>
public static class ReviewStrata
{
    /// <summary>Nhóm của câu mà meta_json không có skill nào (lượt cũ, hoặc không qua skill).</summary>
    public const string Unlabeled = "unlabeled";

    /// <summary>Khớp độ rộng cột review_batch_items.stratum.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Các skill của lượt, bỏ trùng, sắp theo tên, nối bằng "+" (lượt chạy hai skill là một nhóm riêng, ví dụ "knowledge+scenario").
    /// Không có skill, meta hỏng hoặc khác khuôn thì là Unlabeled; không bao giờ ném lỗi (một dòng cũ không được làm hỏng cả đợt bốc).
    /// </summary>
    public static string Of(string? replyMetaJson)
    {
        if (string.IsNullOrWhiteSpace(replyMetaJson))
            return Unlabeled;

        try
        {
            using var doc = JsonDocument.Parse(replyMetaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("skills", out var skills)
                || skills.ValueKind != JsonValueKind.Array)
                return Unlabeled;

            var names = skills.EnumerateArray()
                .Where(s => s.ValueKind == JsonValueKind.String)
                .Select(s => s.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            if (names.Count == 0)
                return Unlabeled;

            var joined = string.Join("+", names);
            return joined.Length <= MaxLength ? joined : joined[..MaxLength];
        }
        catch (JsonException)
        {
            return Unlabeled;
        }
    }
}
