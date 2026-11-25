using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CostParametersSettingRepository(AppDbContext db) : ICostParametersSettingRepository
{
    public Task<AppSetting?> GetAsync(CancellationToken ct = default) =>
        db.AppSettings.FirstOrDefaultAsync(s => s.Key == CostConstants.CostParametersAppSettingKey, ct);

    public async Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
    {
        if (isNew) db.AppSettings.Add(setting);
        db.AuditLogs.Add(audit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("Tham số chi phí vừa được người khác tạo.");
        }
    }
}
