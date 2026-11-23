namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>Đọc các lượt chat thô cho báo cáo chất lượng AI. Khoảng thời gian là [fromUtc, toUtcExclusive).</summary>
public interface IAiQualityRepository
{
    Task<IReadOnlyList<AiQualityTurnRow>> TurnsAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);
}
