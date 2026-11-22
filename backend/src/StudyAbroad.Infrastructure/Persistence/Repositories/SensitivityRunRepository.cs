using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.Sensitivity;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class SensitivityRunRepository(AppDbContext db) : ISensitivityRunRepository
{
    public async Task AddAsync(SensitivityRun run, AuditLog audit, CancellationToken ct = default)
    {
        db.SensitivityRuns.Add(run);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);   // một lần lưu: lần chạy và nhật ký
    }

    public async Task<IReadOnlyList<SensitivityRunRecord>> ListAsync(int limit, CancellationToken ct = default)
    {
        // Danh sách không cần kết quả chi tiết: chỉ chọn các cột tóm tắt, bỏ result_json và parameters_json
        var rows = await (
                from r in db.SensitivityRuns.AsNoTracking()
                join u in db.Users.AsNoTracking() on r.CreatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.CreatedAt descending, r.Id
                select new
                {
                    r.Id, r.CreatedAt, r.CreatedByUserId, r.AlgorithmVersion,
                    r.ProfileSetVersion, r.ProfileSetSeed, r.ProfileCount, r.ProfileSetHash,
                    r.SchoolCount, r.SchoolDataHash, r.RequestJson,
                    r.StableCount, r.WatchCount, r.SensitiveCount, r.DurationMs,
                    Name = u == null ? null : u.FullName
                })
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => new SensitivityRunRecord(new SensitivityRun
        {
            Id = r.Id, CreatedAt = r.CreatedAt, CreatedByUserId = r.CreatedByUserId, AlgorithmVersion = r.AlgorithmVersion,
            ProfileSetVersion = r.ProfileSetVersion, ProfileSetSeed = r.ProfileSetSeed, ProfileCount = r.ProfileCount,
            ProfileSetHash = r.ProfileSetHash, SchoolCount = r.SchoolCount, SchoolDataHash = r.SchoolDataHash,
            RequestJson = r.RequestJson, StableCount = r.StableCount, WatchCount = r.WatchCount,
            SensitiveCount = r.SensitiveCount, DurationMs = r.DurationMs
        }, r.Name)).ToList();
    }

    public async Task<SensitivityRunRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await (
                from r in db.SensitivityRuns.AsNoTracking()
                where r.Id == id
                join u in db.Users.AsNoTracking() on r.CreatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                select new { Run = r, Name = u == null ? null : u.FullName })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new SensitivityRunRecord(row.Run, row.Name);
    }

    public async Task<Guid?> FindPreviousIdAsync(Guid id, CancellationToken ct = default)
    {
        var current = await db.SensitivityRuns.AsNoTracking().Where(r => r.Id == id).Select(r => (DateTime?)r.CreatedAt).FirstOrDefaultAsync(ct);
        if (current is not DateTime at) return null;
        return await db.SensitivityRuns.AsNoTracking()
            .Where(r => r.CreatedAt < at)
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
    }
}
