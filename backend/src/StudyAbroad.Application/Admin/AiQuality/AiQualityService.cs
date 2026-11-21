using StudyAbroad.Application.Admin.AiUsage;

namespace StudyAbroad.Application.Admin.AiQuality;

public interface IAiQualityService
{
    /// <summary>
    /// Báo cáo chất lượng AI cả hệ thống trong khoảng from đến to (tính cả hai đầu, theo ngày UTC). Bỏ trống to thì lấy hôm nay,
    /// bỏ trống from thì lấy 30 ngày kết thúc ở to. Khoảng sai (from sau to, hoặc dài quá 366 ngày) thì ném ArgumentException.
    /// Không có nội dung chat.
    /// </summary>
    Task<AiQualityReport> GetReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public class AiQualityService(IAiQualityRepository repository, TimeProvider? clock = null) : IAiQualityService
{
    // Cùng quy tắc khoảng ngày với trang token AI (AiUsageReportService) để hai trang admin chọn ngày giống nhau
    public const int DefaultDays = AiUsageReportService.DefaultDays;
    public const int MaxDays = AiUsageReportService.MaxDays;

    public async Task<AiQualityReport> GetReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxDays} ngày.");

        var rows = await repository.TurnsAsync(
            start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ct);

        var turns = rows.Select(row => AiQualityMetaReader.ToTurn(
            row.CreatedAt, row.Outcome, row.ReplyMetaJson, row.QuestionMetaJson, row.UsedFallback));
        return AiQualityCalculator.Build(turns, start, end);
    }
}
