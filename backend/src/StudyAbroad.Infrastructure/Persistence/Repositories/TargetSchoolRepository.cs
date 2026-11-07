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

    public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
        db.Universities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == universityId && x.IsActive, ct);

    public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) =>
        db.UniversityOfferings.AsNoTracking()
            .Where(x => x.UniversityId == universityId && !string.IsNullOrEmpty(x.DeadlinesJson))
            .Select(x => x.DeadlinesJson)
            .FirstOrDefaultAsync(ct);
}
