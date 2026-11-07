using StudyAbroad.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Audit;

/// <summary>Ghi nhật ký thao tác admin (UC-61, UC-62, ...).</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default);
    Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default);
}
