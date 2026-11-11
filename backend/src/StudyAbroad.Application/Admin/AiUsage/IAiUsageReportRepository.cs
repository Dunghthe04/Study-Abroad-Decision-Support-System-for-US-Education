namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>Đọc số liệu token đã gộp từ ai_calls. Khoảng thời gian là [fromUtc, toUtcExclusive).</summary>
public interface IAiUsageReportRepository
{
    Task<IReadOnlyList<AiUsageGroupRow>> ByKindAndPurposeAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    Task<IReadOnlyList<AiUsageGroupRow>> ByDayAndKindAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);
}
