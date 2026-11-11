namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>
/// Số đo của một nhóm lần gọi AI. InputTokens chưa gồm token Claude đọc từ cache (CachedTokens tính riêng, tính tiền khác).
/// AvgLatencyMs là thời gian trung bình của MỘT lần gọi (mili giây), làm tròn một chữ số thập phân.
/// </summary>
public record AiUsageMetrics(int Calls, int FailedCalls, long InputTokens, long OutputTokens, long CachedTokens, double AvgLatencyMs);

/// <summary>Theo nguồn: ollama (AI local, không tốn tiền) hoặc claude (AI cloud, trả phí theo token).</summary>
public record AiUsageByKind(string Kind, AiUsageMetrics Metrics);

/// <summary>Theo tính năng. Purpose là tên skill (slm_intents, skill_tra_loi, transcript_ocr_read...), null với dòng cũ chưa có nhãn.</summary>
public record AiUsageByPurpose(string Kind, string? Purpose, AiUsageMetrics Metrics);

/// <summary>Theo ngày (UTC) và nguồn, để vẽ biểu đồ local so với cloud.</summary>
public record AiUsageByDay(DateOnly Date, string Kind, AiUsageMetrics Metrics);

/// <summary>Tổng quan token của admin trong khoảng From đến To (tính cả hai đầu, theo ngày UTC).</summary>
public record AiUsageOverviewDto(
    DateOnly From,
    DateOnly To,
    AiUsageMetrics Total,
    IReadOnlyList<AiUsageByKind> ByKind,
    IReadOnlyList<AiUsageByPurpose> ByPurpose,
    IReadOnlyList<AiUsageByDay> ByDay);

/// <summary>
/// Một dòng thô do truy vấn gộp trả về (chưa tính trung bình): Purpose có khi gộp theo tính năng, Day có khi gộp theo ngày.
/// LatencySumMs là tổng thời gian để cộng dồn các nhóm rồi mới chia cho số lần gọi, tránh trung bình của trung bình.
/// </summary>
public record AiUsageGroupRow(
    string Kind,
    string? Purpose,
    DateTime? Day,
    int Calls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long CachedTokens,
    long LatencySumMs);
