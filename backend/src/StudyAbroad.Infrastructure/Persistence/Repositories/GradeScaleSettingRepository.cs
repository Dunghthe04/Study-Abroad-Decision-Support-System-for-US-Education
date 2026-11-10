using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class GradeScaleSettingRepository(AppDbContext db) : IGradeScaleSettingRepository
{
    public Task<AppSetting?> GetAsync(CancellationToken ct = default) =>
        db.AppSettings.FirstOrDefaultAsync(s => s.Key == AcademicConstants.GradeScale10AppSettingKey, ct);

    public async Task<IReadOnlyList<(StudentProfile Profile, IReadOnlyList<TranscriptScore> Scores)>> GetTranscriptGpaProfilesAsync(
        CancellationToken ct = default)
    {
        var profiles = await db.StudentProfiles
            .Where(p => p.OverallGpa != null && p.GradeScale == GradeScales.Scale10)
            .Where(p => db.TranscriptScores.Any(s => s.StudentProfileId == p.Id && s.Semester != null))
            .ToListAsync(ct);
        var ids = profiles.Select(p => p.Id).ToList();
        var scores = await db.TranscriptScores.AsNoTracking()
            .Where(s => ids.Contains(s.StudentProfileId) && s.Semester != null)
            .ToListAsync(ct);
        var byProfile = scores.ToLookup(s => s.StudentProfileId);
        return profiles.Select(p => (p, (IReadOnlyList<TranscriptScore>)byProfile[p.Id].ToList())).ToList();
    }

    public async Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default)
    {
        if (isNew) db.AppSettings.Add(setting);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct); // bảng, GPA hồ sơ và lịch sử cùng lưu hoặc cùng không
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
