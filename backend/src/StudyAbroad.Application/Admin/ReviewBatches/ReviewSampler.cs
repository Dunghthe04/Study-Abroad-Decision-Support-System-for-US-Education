using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>Một câu được chọn vào mẫu: tin nào, thuộc nhóm nào, đứng thứ mấy trong đợt (từ 1).</summary>
public record SampledItem(Guid MessageId, string Stratum, int Position);

/// <summary>Một nhóm lúc bốc: có bao nhiêu câu (Population) và chọn được bao nhiêu (Picked).</summary>
public record StratumCount(string Stratum, int Population, int Picked);

public record SampleResult(IReadOnlyList<SampledItem> Items, IReadOnlyList<StratumCount> Strata);

/// <summary>
/// Bốc mẫu ngẫu nhiên có chia tầng và LẶP LẠI ĐƯỢC. Hàm thuần: không đọc database, không dùng số ngẫu nhiên của hệ thống.
/// Mỗi câu có một "khóa" lấy từ băm SHA-256 của (seed, mục đích, mã câu); sắp theo khóa là ra thứ tự trông như xáo trộn, nhưng cùng seed
/// và cùng tập câu thì luôn ra đúng kết quả đó, dù thứ tự đầu vào khác nhau. Không ai chọn tay được câu nào.
/// Hai thứ tự độc lập (mục đích "pick" và "order") để câu được chọn không đoán được thứ tự xuất hiện của nó, và người duyệt
/// không phải đọc liền một mạch hết nhóm này đến nhóm khác.
/// </summary>
public static class ReviewSampler
{
    /// <summary>
    /// candidates: (mã câu, nhóm). Mỗi nhóm chọn tối đa sizePerStratum câu (nhóm ít hơn thì lấy hết). Câu trùng mã chỉ tính một lần.
    /// Kết quả: danh sách câu chọn theo thứ tự đọc (Position từ 1), và số liệu từng nhóm theo thứ tự tên nhóm.
    /// </summary>
    public static SampleResult Draw(IEnumerable<(Guid MessageId, string Stratum)> candidates, string seed, int sizePerStratum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentOutOfRangeException.ThrowIfLessThan(sizePerStratum, 1);

        var groups = candidates
            .DistinctBy(c => c.MessageId)
            .GroupBy(c => c.Stratum, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var picked = new List<(Guid MessageId, string Stratum)>();
        var counts = new List<StratumCount>();
        foreach (var group in groups)
        {
            var chosen = group
                .OrderBy(c => Key(seed, "pick", c.MessageId), StringComparer.Ordinal)
                .ThenBy(c => c.MessageId)
                .Take(sizePerStratum)
                .ToList();
            picked.AddRange(chosen);
            counts.Add(new StratumCount(group.Key, group.Count(), chosen.Count));
        }

        var items = picked
            .OrderBy(c => Key(seed, "order", c.MessageId), StringComparer.Ordinal)
            .ThenBy(c => c.MessageId)
            .Select((c, index) => new SampledItem(c.MessageId, c.Stratum, index + 1))
            .ToList();
        return new SampleResult(items, counts);
    }

    /// <summary>Số nhóm lúc bốc dưới dạng JSON để lưu (camelCase), đọc lại bằng ReadStrata.</summary>
    public static string StrataToJson(IReadOnlyList<StratumCount> strata) => JsonSerializer.Serialize(strata, JsonOptions);

    /// <summary>Đọc lại số nhóm đã lưu. JSON hỏng hoặc rỗng thì trả danh sách rỗng, không ném lỗi.</summary>
    public static IReadOnlyList<StratumCount> ReadStrata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<StratumCount>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // SHA-256 của "seed|mục đích|mã câu" dưới dạng chữ số thập lục phân: so sánh theo thứ tự chữ cho ra thứ tự ổn định ở mọi máy
    private static string Key(string seed, string purpose, Guid messageId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{purpose}|{messageId:D}")));
}
