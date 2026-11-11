namespace StudyAbroad.Application.Admin.AiUsage;

public interface IAiUsageReportService
{
    /// <summary>
    /// Tổng quan token trong khoảng from đến to (tính cả hai đầu, theo ngày UTC). Bỏ trống to thì lấy hôm nay,
    /// bỏ trống from thì lấy 30 ngày kết thúc ở to. Khoảng sai (from sau to, hoặc dài quá 366 ngày) thì ném ArgumentException.
    /// </summary>
    Task<AiUsageOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public class AiUsageReportService(IAiUsageReportRepository repository, TimeProvider? clock = null) : IAiUsageReportService
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    public async Task<AiUsageOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxDays} ngày.");

        var fromUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var kindPurpose = await repository.ByKindAndPurposeAsync(fromUtc, toUtcExclusive, ct);
        var dayKind = await repository.ByDayAndKindAsync(fromUtc, toUtcExclusive, ct);

        return new AiUsageOverviewDto(
            start,
            end,
            Metrics(kindPurpose),
            kindPurpose
                .GroupBy(row => row.Kind)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new AiUsageByKind(group.Key, Metrics(group)))
                .ToList(),
            kindPurpose
                .OrderByDescending(row => row.InputTokens + row.OutputTokens)
                .ThenBy(row => row.Kind, StringComparer.Ordinal)
                .ThenBy(row => row.Purpose, StringComparer.Ordinal)
                .Select(row => new AiUsageByPurpose(row.Kind, row.Purpose, Metrics([row])))
                .ToList(),
            dayKind
                .OrderBy(row => row.Day)
                .ThenBy(row => row.Kind, StringComparer.Ordinal)
                .Select(row => new AiUsageByDay(DateOnly.FromDateTime(row.Day!.Value), row.Kind, Metrics([row])))
                .ToList());
    }

    // Cộng các dòng thô rồi mới chia: trung bình thời gian của cả nhóm = tổng thời gian chia tổng số lần gọi
    private static AiUsageMetrics Metrics(IEnumerable<AiUsageGroupRow> rows)
    {
        var list = rows as IReadOnlyCollection<AiUsageGroupRow> ?? rows.ToList();
        var calls = list.Sum(row => row.Calls);
        return new AiUsageMetrics(
            calls,
            list.Sum(row => row.FailedCalls),
            list.Sum(row => row.InputTokens),
            list.Sum(row => row.OutputTokens),
            list.Sum(row => row.CachedTokens),
            calls == 0 ? 0 : Math.Round((double)list.Sum(row => row.LatencySumMs) / calls, 1));
    }
}
