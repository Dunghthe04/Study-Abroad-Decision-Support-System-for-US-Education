using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.Scholarships;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AdminScholarshipRepository(AppDbContext db) : IAdminScholarshipRepository
{
    public async Task<PagedResult<AdminScholarshipDto>> SearchAsync(AdminScholarshipQuery query, CancellationToken ct = default)
    {
        var q = db.Scholarships.AsNoTracking();

        if (query.UniversityId is { } universityId)
            q = q.Where(s => s.UniversityId == universityId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(s => EF.Functions.ILike(s.Name, pattern)
                || (s.Provider != null && EF.Functions.ILike(s.Provider, pattern))
                || db.Universities.Any(u => u.Id == s.UniversityId && EF.Functions.ILike(u.Name, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(query.StudyLevel))
        {
            var level = query.StudyLevel.Trim().ToLowerInvariant();
            q = q.Where(s => s.StudyLevel == level);
        }
        if (query.IsActive is not null)
            q = q.Where(s => s.IsActive == query.IsActive);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(s => s.Deadline == null)
            .ThenBy(s => s.Deadline)
            .ThenBy(s => s.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new AdminScholarshipDto(
                s.Id,
                s.UniversityId,
                db.Universities.Where(u => u.Id == s.UniversityId).Select(u => u.Name).FirstOrDefault(),
                s.Name,
                s.Provider,
                s.StudyLevel,
                s.AmountUsd,
                s.CoverageType,
                s.MinGpa4,
                s.MinIelts,
                s.Deadline,
                s.EligibilityNotes,
                s.SourceUrl,
                s.RetrievedAt,
                s.SourceReliability,
                s.IsActive,
                s.UpdatedAt,
                db.Users.Where(u => u.Id == s.LastEditedByUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<AdminScholarshipDto>(items, query.Page, query.PageSize, total);
    }

    public Task<Scholarship?> GetAsync(Guid scholarshipId, CancellationToken ct = default) =>
        db.Scholarships.FirstOrDefaultAsync(s => s.Id == scholarshipId, ct);

    public async Task<string?> GetUniversityNameAsync(Guid? universityId, CancellationToken ct = default)
    {
        if (universityId is null) return null;
        return await db.Universities.AsNoTracking()
            .Where(u => u.Id == universityId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null) return null;
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
    }

    public void Add(Scholarship scholarship) => db.Scholarships.Add(scholarship);

    public void AddAudit(AuditLog entry) => db.AuditLogs.Add(entry);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
