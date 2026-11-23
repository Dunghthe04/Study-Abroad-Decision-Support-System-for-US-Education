using StudyAbroad.Application.Admin.AiUsage;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Khoảng ngày của báo cáo chất lượng AI, cùng quy tắc với trang token AI (AiUsageReportService): tính cả hai đầu theo ngày UTC,
/// bỏ trống to thì lấy hôm nay, bỏ trống from thì lấy 30 ngày kết thúc ở to, tối đa 366 ngày. Khoảng sai thì ném ArgumentException.
/// </summary>
public static class AiQualityRange
{
    public const int DefaultDays = AiUsageReportService.DefaultDays;
    public const int MaxDays = AiUsageReportService.MaxDays;

    public static (DateOnly Start, DateOnly End) Resolve(DateOnly? from, DateOnly? to, TimeProvider? clock = null)
    {
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxDays} ngày.");
        return (start, end);
    }

    public static DateTime FromUtc(DateOnly start) => start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static DateTime ToUtcExclusive(DateOnly end) => end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
