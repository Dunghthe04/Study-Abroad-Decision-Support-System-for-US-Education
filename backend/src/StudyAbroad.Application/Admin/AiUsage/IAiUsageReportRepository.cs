namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>Đọc số liệu token đã gộp từ ai_calls. Khoảng thời gian là [fromUtc, toUtcExclusive).</summary>
public interface IAiUsageReportRepository
{
    /// <summary>Gộp theo (nguồn, tính năng). userId bỏ trống là cả hệ thống, có giá trị là riêng người đó.</summary>
    Task<IReadOnlyList<AiUsageGroupRow>> ByKindAndPurposeAsync(
        DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default);

    /// <summary>Gộp theo (ngày UTC, nguồn). userId bỏ trống là cả hệ thống, có giá trị là riêng người đó.</summary>
    Task<IReadOnlyList<AiUsageGroupRow>> ByDayAndKindAsync(
        DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default);

    /// <summary>Một trang xếp hạng người dùng theo token giảm dần, kèm tổng số người có gọi AI trong khoảng.</summary>
    Task<(IReadOnlyList<AiUsageUserRow> Rows, int TotalUsers)> ByUserPageAsync(
        DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default);

    /// <summary>Họ tên, email của các người dùng còn tồn tại trong số mã được hỏi (tài khoản đã xóa thì không có trong kết quả).</summary>
    Task<IReadOnlyList<AiUsageUserInfo>> UsersByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
