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

    /// <summary>Một trang các lượt chat của một người (mới nhất trước) kèm số đo, và tổng số lượt trong khoảng. Không có nội dung chat.</summary>
    Task<(IReadOnlyList<AiUsageTurnRow> Rows, int TotalTurns)> TurnsPageAsync(
        Guid userId, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default);

    /// <summary>Thông tin chung của một lượt chat; null nếu không có tin nhắn đó.</summary>
    Task<AiUsageTurnHeader?> FindTurnAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Các lần gọi AI của một lượt theo thứ tự thời gian.</summary>
    Task<IReadOnlyList<AiUsageCallDetailDto>> CallsOfTurnAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Họ tên, email của các người dùng còn tồn tại trong số mã được hỏi (tài khoản đã xóa thì không có trong kết quả).</summary>
    Task<IReadOnlyList<AiUsageUserInfo>> UsersByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
