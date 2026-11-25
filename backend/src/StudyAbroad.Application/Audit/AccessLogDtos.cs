namespace StudyAbroad.Application.Audit;

/// <summary>
/// DTO thông tin một dòng nhật ký truy cập dữ liệu nhạy cảm và nhật ký thao tác (UC-91).
/// </summary>
public record AccessLogDto(
    Guid Id,
    DateTime CreatedAt,
    Guid? ActorUserId,
    string? ActorFullName,
    string? ActorEmail,
    string? ActorRole,
    string Action,
    string ActionCategory,
    string ActionDescription,
    string? EntityType,
    string? EntityId,
    string? DetailJson,
    string? IpAddress,
    string? UserAgent
);

/// <summary>
/// Bộ lọc nhật ký truy cập và thao tác (UC-91).
/// </summary>
public record AccessLogFilter(
    string? Category = "all_sensitive",
    string? Action = null,
    string? Search = null,
    Guid? ActorUserId = null,
    string? ActorRole = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// Thống kê tổng quan số lượt truy cập dữ liệu nhạy cảm theo nhóm thao tác.
/// </summary>
public record AccessLogSummaryDto(
    int TotalSensitiveCount,
    int CenterDocumentViews,
    int ReportedMessageViews,
    int SlmSampleExports,
    int TotalAllCount = 0,
    int ChatMessageViews = 0
);

