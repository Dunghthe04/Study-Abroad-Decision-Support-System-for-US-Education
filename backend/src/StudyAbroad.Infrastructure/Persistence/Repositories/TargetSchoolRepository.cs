using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ITargetSchoolRepository thao tác trực tiếp với cơ sở dữ liệu qua EF Core.
/// </summary>
public class TargetSchoolRepository(AppDbContext db) : ITargetSchoolRepository
{
    public Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TargetSchools.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TargetSchools.AsNoTracking()
            .Where(ts => ts.Id == id)
            .Join(db.Universities.AsNoTracking(),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new TargetSchoolDto(
                    ts.Id,
                    ts.UserId,
                    ts.UniversityId,
                    u.Code,
                    u.Name,
                    u.City,
                    u.State,
                    u.Website,
                    u.Control,
                    u.AcceptanceRate,
                    ts.Category,
                    ts.Status,
                    ts.Priority,
                    ts.Notes,
                    ts.ApplicationRound,
                    ts.Deadline,
                    ts.CreatedAt,
                    ts.UpdatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<List<TargetSchoolDto>> GetListByUserAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
    {
        var query = db.TargetSchools.AsNoTracking()
            .Where(ts => ts.UserId == userId);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(ts => ts.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            query = query.Where(ts => ts.Status == st);
        }

        return await query
            .Join(db.Universities.AsNoTracking(),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new TargetSchoolDto(
                    ts.Id,
                    ts.UserId,
                    ts.UniversityId,
                    u.Code,
                    u.Name,
                    u.City,
                    u.State,
                    u.Website,
                    u.Control,
                    u.AcceptanceRate,
                    ts.Category,
                    ts.Status,
                    ts.Priority,
                    ts.Notes,
                    ts.ApplicationRound,
                    ts.Deadline,
                    ts.CreatedAt,
                    ts.UpdatedAt))
            .OrderBy(ts => ts.Priority)
            .ThenBy(ts => ts.Deadline == null)
            .ThenBy(ts => ts.Deadline)
            .ThenByDescending(ts => ts.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
        db.TargetSchools.FirstOrDefaultAsync(x => x.UserId == userId && x.UniversityId == universityId, ct);

    public Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
        db.TargetSchools.AnyAsync(x => x.UserId == userId && x.UniversityId == universityId, ct);

    public async Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        db.TargetSchools.Add(targetSchool);
        await db.SaveChangesAsync(ct);
        return targetSchool;
    }

    public async Task DeleteAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        db.TargetSchools.Remove(targetSchool);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TargetSchool targetSchool, CancellationToken ct = default)
    {
        targetSchool.UpdatedAt = DateTime.UtcNow;
        db.TargetSchools.Update(targetSchool);
        await db.SaveChangesAsync(ct);
    }

    public Task<TargetSchool?> GetByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) =>
        db.TargetSchools
            .Join(db.Universities, ts => ts.UniversityId, u => u.Id, (ts, u) => new { ts, u })
            .Where(x => x.ts.UserId == userId && x.u.Code == code)
            .Select(x => x.ts)
            .FirstOrDefaultAsync(ct);

    public Task<TargetSchoolDto?> GetDtoByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) =>
        db.TargetSchools.AsNoTracking()
            .Where(ts => ts.UserId == userId)
            .Join(db.Universities.AsNoTracking().Where(u => u.Code == code),
                ts => ts.UniversityId,
                u => u.Id,
                (ts, u) => new TargetSchoolDto(
                    ts.Id,
                    ts.UserId,
                    ts.UniversityId,
                    u.Code,
                    u.Name,
                    u.City,
                    u.State,
                    u.Website,
                    u.Control,
                    u.AcceptanceRate,
                    ts.Category,
                    ts.Status,
                    ts.Priority,
                    ts.Notes,
                    ts.ApplicationRound,
                    ts.Deadline,
                    ts.CreatedAt,
                    ts.UpdatedAt))
            .FirstOrDefaultAsync(ct);

    public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
        db.Universities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == universityId && x.IsActive, ct);

    public Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default) =>
        db.Universities.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);

    public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) =>
        db.UniversityOfferings.AsNoTracking()
            .Where(x => x.UniversityId == universityId && x.DeadlinesJson != null)
            .Select(x => x.DeadlinesJson)
            .FirstOrDefaultAsync(ct);
}
