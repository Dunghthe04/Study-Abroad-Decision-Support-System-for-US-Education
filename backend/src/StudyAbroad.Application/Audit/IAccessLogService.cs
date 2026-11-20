using StudyAbroad.Application.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Audit;

/// <summary>
/// Dịch vụ nghiệp vụ xem và lọc nhật ký truy cập dữ liệu nhạy cảm (UC-97).
/// </summary>
public interface IAccessLogService
{
    Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(
        string? category = null,
        string? action = null,
        string? search = null,
        Guid? actorUserId = null,
        string? actorRole = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<AccessLogSummaryDto> GetSummaryAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);
}

