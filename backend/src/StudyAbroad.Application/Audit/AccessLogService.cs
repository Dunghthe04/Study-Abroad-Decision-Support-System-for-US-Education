using StudyAbroad.Application.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Audit;

public class AccessLogService(IAuditLogRepository auditLogs) : IAccessLogService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(
        string? category = null,
        string? action = null,
        string? search = null,
        Guid? actorUserId = null,
        string? actorRole = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (page > 100_000) page = 100_000;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Phòng thủ: hoán đổi nếu ngày bắt đầu lớn hơn ngày kết thúc
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            (from, to) = (to, from);
        }

        var fromUtc = StartOfDayUtc(from);
        var toUtc = StartOfDayUtc(to?.AddDays(1));

        var filter = new AccessLogFilter(
            Category: string.IsNullOrWhiteSpace(category) ? "all_sensitive" : category.Trim().ToLowerInvariant(),
            Action: string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
            Search: string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ActorUserId: actorUserId,
            ActorRole: string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim().ToLowerInvariant(),
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Page: page,
            PageSize: pageSize
        );

        return await auditLogs.GetAccessLogsAsync(filter, ct);
    }

    public async Task<AccessLogSummaryDto> GetSummaryAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        // Phòng thủ: hoán đổi nếu ngày bắt đầu lớn hơn ngày kết thúc
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            (from, to) = (to, from);
        }

        var fromUtc = StartOfDayUtc(from);
        var toUtc = StartOfDayUtc(to?.AddDays(1));

        return await auditLogs.GetAccessLogSummaryAsync(fromUtc, toUtc, ct);
    }

    private static DateTime? StartOfDayUtc(DateOnly? day) =>
        day is { } d ? DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc) : null;
}

