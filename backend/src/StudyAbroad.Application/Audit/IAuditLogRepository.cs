using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Audit;

/// <summary>Ghi và đọc nhật ký thao tác và truy cập hệ thống (UC-61, UC-62, UC-97, ...).</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default);
    Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>[UC-62] Chi tiết (JSON) của lần khóa tài khoản gần nhất, để mở khóa trung tâm khôi phục đúng trạng thái trước khi Tạm ngừng.</summary>
    Task<string?> GetLatestLockDetailJsonAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(AccessLogFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<AccessLogDto>([], filter.Page, filter.PageSize, 0));

    Task<AccessLogSummaryDto> GetAccessLogSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccessLogSummaryDto(0, 0, 0, 0));
}
