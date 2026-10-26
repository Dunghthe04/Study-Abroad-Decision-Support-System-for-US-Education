using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ParentLinkRepository(AppDbContext db) : IParentLinkRepository
{
    public async Task AddAsync(ParentStudentLink link, CancellationToken ct = default)
    {
        db.ParentStudentLinks.Add(link);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ParentStudentLink link, CancellationToken ct = default)
    {
        db.ParentStudentLinks.Update(link);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
                entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException("Liên kết phụ huynh đã được cập nhật bởi một request khác.", ex);
        }
    }

    public Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.StudentUserId == studentUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
}
