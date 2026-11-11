using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>
/// Truy vấn gộp ai_calls theo nhóm. Viết trên IQueryable nên chạy được hai nơi: EF dịch thành SQL (GROUP BY ở database,
/// không kéo cả bảng về), và test chạy trên danh sách trong bộ nhớ để kiểm số liệu. Token trống (null) tính là 0.
/// Chỉ dùng các biểu thức EF dịch được (Count, Sum, GroupBy theo cột); đã có test kiểm tra dịch ra SQL.
/// </summary>
public static class AiUsageAggregator
{
    /// <summary>Các lần gọi trong khoảng [fromUtc, toUtcExclusive).</summary>
    public static IQueryable<AiCall> InWindow(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        calls.Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toUtcExclusive);

    /// <summary>Gộp theo (nguồn, tính năng): nền để tính tổng và tổng theo nguồn.</summary>
    public static IQueryable<AiUsageGroupRow> ByKindAndPurpose(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .GroupBy(c => new { c.Kind, c.Purpose })
            .Select(g => new AiUsageGroupRow(
                g.Key.Kind,
                g.Key.Purpose,
                null,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0))));

    /// <summary>Gộp theo (ngày UTC, nguồn): để vẽ biểu đồ theo ngày.</summary>
    public static IQueryable<AiUsageGroupRow> ByDayAndKind(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .GroupBy(c => new { Day = c.CreatedAt.Date, c.Kind })
            .Select(g => new AiUsageGroupRow(
                g.Key.Kind,
                null,
                g.Key.Day,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0))));
}
