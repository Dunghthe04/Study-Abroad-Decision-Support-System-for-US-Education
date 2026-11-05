using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Entities;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var idStr = userId.ToString();
        var detailJson = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Action == "user.lock" && x.EntityId == idStr)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.DetailJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(detailJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(detailJson);
            if (doc.RootElement.TryGetProperty("reason", out var reasonElem))
            {
                return reasonElem.GetString();
            }
        }
        catch
        {
            // Bỏ qua lỗi parse nếu định dạng json cũ
        }
        return null;
    }
}
