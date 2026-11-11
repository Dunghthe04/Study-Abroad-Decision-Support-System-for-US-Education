namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Giá token của từng model, đọc từ cấu hình (không ghi cứng trong code). Mặc định nằm ở appsettings.json, mục "AiPricing";
/// đổi giá bằng cách sửa file đó hoặc đặt biến môi trường, ví dụ AiPricing__Models__claude-haiku-5-5__InputPerMTok=0.12.
/// </summary>
public class AiPricingOptions
{
    public const string SectionName = "AiPricing";

    /// <summary>Prompt dài hơn mức này (token) thì cả lần gọi tính giá bậc dài.</summary>
    public int LongPromptThreshold { get; set; } = 100_000;

    /// <summary>Giá theo tên model (không phân biệt hoa thường). Model nào không có ở đây thì "chưa biết giá".</summary>
    public Dictionary<string, ModelPrice> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Giá một model, USD cho MỘT TRIỆU token. Giá bậc dài bỏ trống thì dùng giá thường.</summary>
public class ModelPrice
{
    public decimal InputPerMTok { get; set; }
    public decimal OutputPerMTok { get; set; }
    public decimal? LongInputPerMTok { get; set; }
    public decimal? LongOutputPerMTok { get; set; }
}

/// <summary>
/// Công thức tính tiền một lần gọi AI (USD) từ bảng giá cấu hình. Không đụng database.
/// Tiền được tính và lưu ngay lúc gọi, nên đổi giá sau này không làm lệch các dòng đã ghi.
/// </summary>
public sealed class AiPricing
{
    /// <summary>Không có bảng giá nào: mọi lần gọi Claude đều "chưa biết giá" (dùng khi chưa cấu hình, và trong test cũ).</summary>
    public static readonly AiPricing None = new(new AiPricingOptions());

    private const decimal PerMillion = 1_000_000m;
    private const string ClaudeKind = "claude";
    private const string OllamaKind = "ollama";

    /// <summary>Một dòng của bảng giá, để trang admin hiển thị (chỉ đọc).</summary>
    public sealed record PriceInfo(
        string Model, decimal InputPerMTok, decimal OutputPerMTok,
        decimal LongInputPerMTok, decimal LongOutputPerMTok, int LongPromptThreshold);

    private readonly Dictionary<string, ModelPrice> _models;

    public int LongPromptThreshold { get; }

    public AiPricing(AiPricingOptions options)
    {
        LongPromptThreshold = options.LongPromptThreshold;
        // Giá âm hoặc thiếu tên là cấu hình sai: bỏ dòng đó để model bị báo "chưa biết giá" thay vì ra tiền sai
        _models = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (var (model, price) in options.Models)
        {
            if (string.IsNullOrWhiteSpace(model) || price is null)
                continue;
            if (price.InputPerMTok < 0 || price.OutputPerMTok < 0 || price.LongInputPerMTok < 0 || price.LongOutputPerMTok < 0)
                continue;
            _models[model.Trim()] = price;
        }
    }

    /// <summary>Bảng giá đang áp dụng, sắp theo tên model.</summary>
    public IReadOnlyList<PriceInfo> Catalog() => _models
        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
        .Select(entry => new PriceInfo(
            entry.Key, entry.Value.InputPerMTok, entry.Value.OutputPerMTok,
            entry.Value.LongInputPerMTok ?? entry.Value.InputPerMTok,
            entry.Value.LongOutputPerMTok ?? entry.Value.OutputPerMTok,
            LongPromptThreshold))
        .ToList();

    /// <summary>
    /// Tiền của một lần gọi. Ollama chạy máy mình nên 0. Claude có trong bảng giá thì tính theo token.
    /// Claude mà model lạ hoặc thiếu tên thì trả null (chưa biết giá, KHÔNG phải miễn phí) để báo cáo không nhầm là 0 đồng.
    /// </summary>
    public decimal? Cost(string? kind, string? model, int inputTokens, int outputTokens)
    {
        if (string.Equals(kind, OllamaKind, StringComparison.OrdinalIgnoreCase))
            return 0m;
        if (!string.Equals(kind, ClaudeKind, StringComparison.OrdinalIgnoreCase)
            || model is null || !_models.TryGetValue(model.Trim(), out var price))
            return null;

        var input = Math.Max(0, inputTokens);
        var output = Math.Max(0, outputTokens);

        var isLong = input > LongPromptThreshold;
        var inputRate = isLong ? price.LongInputPerMTok ?? price.InputPerMTok : price.InputPerMTok;
        var outputRate = isLong ? price.LongOutputPerMTok ?? price.OutputPerMTok : price.OutputPerMTok;

        return (input * inputRate + output * outputRate) / PerMillion;
    }
}
