using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class GradeScaleSettingRepository(AppDbContext db) : IGradeScaleSettingRepository
{
    public Task<AppSetting?> GetAsync(CancellationToken ct = default) =>
        db.AppSettings.FirstOrDefaultAsync(s => s.Key == AcademicConstants.GradeScale10AppSettingKey, ct);

    public async Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
    {
        if (isNew) db.AppSettings.Add(setting);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct); // bảng và lịch sử cùng lưu hoặc cùng không
    }

    public async Task<IReadOnlyList<(AuditLog Log, string? ActorName)>> GetHistoryAsync(int limit, CancellationToken ct = default)
    {
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == GradeScaleSettingService.AuditAction)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                Log = a,
                ActorName = db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => (r.Log, r.ActorName)).ToList();
    }
}
