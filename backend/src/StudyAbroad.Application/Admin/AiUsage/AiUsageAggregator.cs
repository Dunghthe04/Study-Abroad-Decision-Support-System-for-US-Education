using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>
/// Truy vấn gộp ai_calls theo nhóm. Viết trên IQueryable nên chạy được hai nơi: EF dịch thành SQL (GROUP BY ở database,
/// không kéo cả bảng về), và test chạy trên danh sách trong bộ nhớ để kiểm số liệu. Token trống (null) tính là 0.
/// Chỉ dùng các biểu thức EF dịch được (Count, Sum, GroupBy theo cột); đã có test kiểm tra dịch ra SQL.
/// userId: chỉ tính các lần gọi của người đó; bỏ trống là tính tất cả.
/// </summary>
public static class AiUsageAggregator
{
    /// <summary>Các lần gọi trong khoảng [fromUtc, toUtcExclusive), của một người nếu có userId.</summary>
    public static IQueryable<AiCall> InWindow(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null)
    {
        var inWindow = calls.Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toUtcExclusive);
        return userId is { } id ? inWindow.Where(c => c.UserId == id) : inWindow;
    }

    /// <summary>Gộp theo (nguồn, tính năng): nền để tính tổng và tổng theo nguồn.</summary>
    public static IQueryable<AiUsageGroupRow> ByKindAndPurpose(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null) =>
        InWindow(calls, fromUtc, toUtcExclusive, userId)
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
    public static IQueryable<AiUsageGroupRow> ByDayAndKind(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null) =>
        InWindow(calls, fromUtc, toUtcExclusive, userId)
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

    /// <summary>
    /// Một trang xếp hạng người dùng theo tổng token (vào + ra) giảm dần; bằng nhau thì theo mã người dùng để các trang không lặp hay sót.
    /// Bỏ qua các lần gọi không có người dùng (ngoài phiên đăng nhập).
    /// </summary>
    public static IQueryable<AiUsageUserRow> ByUserPage(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .Where(c => c.UserId != null)
            .GroupBy(c => c.UserId!.Value)
            // Sắp xếp và phân trang trên các NHÓM, trước khi dựng bản ghi: sắp sau khi dựng bản ghi bằng hàm khởi tạo thì EF
            // không dịch được (không biết thuộc tính nào ứng với phép cộng nào). Test dịch SQL canh đúng chỗ này
            .OrderByDescending(g => g.Sum(c => (long)(c.PromptTokens ?? 0)) + g.Sum(c => (long)(c.CompletionTokens ?? 0)))
            .ThenBy(g => g.Key)
            .Skip(skip)
            .Take(take)
            .Select(g => new AiUsageUserRow(
                g.Key,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0))));

    /// <summary>Các người dùng có ít nhất một lần gọi trong khoảng (mỗi người một lần), để đếm tổng số người cho phân trang.</summary>
    public static IQueryable<Guid> UsersInWindow(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .Where(c => c.UserId != null)
            .Select(c => c.UserId!.Value)
            .Distinct();
}
