namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Bảng giá và công thức tính tiền một lần gọi AI (USD). Logic thuần, không đụng database.
/// Giá tính theo MỘT TRIỆU token. Khi Anthropic đổi giá hoặc thêm model: sửa Claude ở đây, tiền của các dòng GHI SAU sẽ đúng
/// (dòng đã ghi giữ giá lúc đó, vì tiền được tính và lưu tại thời điểm gọi).
/// </summary>
public static class AiPricing
{
    /// <summary>Giá một model Claude. Prompt dài hơn LongPromptThreshold thì tính giá bậc dài cho cả lần gọi.</summary>
    /// <param name="CacheReadPerMTok">Giá token đọc từ cache. Null = chưa xác nhận: tạm tính bằng giá token vào thường (tính dư còn hơn thiếu).</param>
    public sealed record ClaudePrice(
        decimal InputPerMTok, decimal OutputPerMTok,
        decimal LongInputPerMTok, decimal LongOutputPerMTok,
        decimal? CacheReadPerMTok = null);

    public const int LongPromptThreshold = 100_000;   // prompt tối đa 100K token dùng giá bậc thường (theo trang giá Anthropic)

    private const decimal PerMillion = 1_000_000m;
    private const string ClaudeKind = "claude";
    private const string OllamaKind = "ollama";

    // Haiku 5.5: vào 0,10 USD, ra 0,50 USD cho prompt tới 100K token; trên 100K là 0,50 và 2,50.
    // Giá token đọc từ cache của Haiku 5.5 chưa có trong tài liệu đã xem nên để null (xem ClaudePrice).
    private static readonly Dictionary<string, ClaudePrice> Claude = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-haiku-5-5"] = new(0.10m, 0.50m, 0.50m, 2.50m),
    };

    /// <summary>
    /// Tiền của một lần gọi. Ollama chạy máy mình nên 0. Claude có trong bảng giá thì tính theo token.
    /// Claude mà model lạ hoặc thiếu tên thì trả null (chưa biết giá, KHÔNG phải miễn phí) để báo cáo không nhầm là 0 đồng.
    /// </summary>
    /// <param name="inputTokens">Token vào, chưa gồm phần đọc từ cache (đúng như Anthropic báo input_tokens).</param>
    public static decimal? Cost(string? kind, string? model, int inputTokens, int outputTokens, int cachedTokens)
    {
        if (string.Equals(kind, OllamaKind, StringComparison.OrdinalIgnoreCase))
            return 0m;
        if (!string.Equals(kind, ClaudeKind, StringComparison.OrdinalIgnoreCase)
            || model is null || !Claude.TryGetValue(model.Trim(), out var price))
            return null;

        var input = Math.Max(0, inputTokens);
        var output = Math.Max(0, outputTokens);
        var cached = Math.Max(0, cachedTokens);

        // Bậc giá theo tổng prompt của lần gọi, gồm cả phần đã nằm trong cache
        var isLong = (long)input + cached > LongPromptThreshold;
        var inputRate = isLong ? price.LongInputPerMTok : price.InputPerMTok;
        var outputRate = isLong ? price.LongOutputPerMTok : price.OutputPerMTok;
        var cacheRate = price.CacheReadPerMTok ?? inputRate;

        return (input * inputRate + cached * cacheRate + output * outputRate) / PerMillion;
    }
}
