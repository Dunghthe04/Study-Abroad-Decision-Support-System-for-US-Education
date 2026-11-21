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
    public async Task<AiQualityReport> GetReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var (start, end) = AiQualityRange.Resolve(from, to, clock);
        var rows = await repository.TurnsAsync(AiQualityRange.FromUtc(start), AiQualityRange.ToUtcExclusive(end), ct);

        var turns = rows.Select(row => AiQualityMetaReader.ToTurn(
            row.CreatedAt, row.Outcome, row.ReplyMetaJson, row.QuestionMetaJson, row.UsedFallback));
        return AiQualityCalculator.Build(turns, start, end);
    }
}
