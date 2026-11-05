using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
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
        await SaveOrThrowConflictAsync(ct);
    }

    public Task<ParentStudentLink?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<ParentStudentLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking().FirstOrDefaultAsync(l => l.TokenHash == tokenHash, ct);

    public Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.StudentUserId == studentUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public Task<List<ParentStudentLink>> GetActiveByParentAsync(Guid parentUserId, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.ParentUserId == parentUserId && l.Status == ParentLinkStatuses.Active)
            .OrderBy(l => l.AcceptedAt)
            .ToListAsync(ct);

    public Task<Consent?> GetConsentAsync(Guid consentId, CancellationToken ct = default) =>
        db.Consents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == consentId, ct);

    public Task<bool> HasGrantedConsentAsync(Guid studentUserId, CancellationToken ct = default) =>
        db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.StudentUserId == studentUserId && l.Status == ParentLinkStatuses.Active && l.ConsentId != null)
            .Join(db.Consents, l => l.ConsentId, c => (Guid?)c.Id, (l, c) => c)
            .AnyAsync(c => c.Granted, ct);

    public async Task SaveAcceptAsync(ParentStudentLink link, Consent consent, CancellationToken ct = default)
    {
        // Một SaveChanges = một transaction: consent và link cùng thành công hoặc cùng rollback
        db.Consents.Add(consent);
        db.ParentStudentLinks.Update(link);
        await SaveOrThrowConflictAsync(ct);
    }

    public async Task SaveRevokeAsync(ParentStudentLink link, Consent? consent, CancellationToken ct = default)
    {
        db.ParentStudentLinks.Update(link);
        if (consent != null)
            db.Consents.Update(consent);
        await SaveOrThrowConflictAsync(ct);
    }

    private async Task SaveOrThrowConflictAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Bỏ mọi thay đổi đang chờ để DbContext dùng tiếp được (kể cả consent vừa Add)
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("Liên kết phụ huynh đã được cập nhật bởi một request khác.", ex);
        }
    }
}
