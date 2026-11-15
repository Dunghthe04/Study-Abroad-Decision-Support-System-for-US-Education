using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CenterVerificationRepository(AppDbContext db) : ICenterVerificationRepository
{
    public async Task<CenterVerificationRequest?> GetCurrentByCenterIdAsync(Guid centerId, CancellationToken ct = default)
    {
        return await db.CenterVerificationRequests
            .Include(x => x.Documents)
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(x => x.CenterId == centerId, ct);
    }

    public async Task<CenterVerificationRequest?> GetByIdWithDocumentsAsync(Guid id, CancellationToken ct = default)
    {
        return await db.CenterVerificationRequests
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<PagedResult<CenterVerificationRequest>> SearchAsync(CenterVerificationQuery query, CancellationToken ct = default)
    {
        var q = db.CenterVerificationRequests
            .Include(x => x.Documents)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalizedStatus = query.Status.Trim().ToLowerInvariant();
            q = q.Where(x => x.Status.ToLower() == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.LegalName, pattern) ||
                             EF.Functions.ILike(x.TaxCode, pattern) ||
                             EF.Functions.ILike(x.LegalRepresentative, pattern) ||
                             db.StudyCenters.Any(c => c.Id == x.CenterId && EF.Functions.ILike(c.Name, pattern)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Status == CenterVerificationStatuses.PendingReview ? 0 : 1)
            .ThenByDescending(x => x.SubmittedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<CenterVerificationRequest>(items, query.Page, query.PageSize, total);
    }

    public async Task AddRequestAsync(CenterVerificationRequest request, CancellationToken ct = default)
    {
        await db.CenterVerificationRequests.AddAsync(request, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRequestAsync(CenterVerificationRequest request, CancellationToken ct = default)
    {
        request.UpdatedAt = DateTime.UtcNow;
        db.CenterVerificationRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CenterVerificationDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
    {
        return await db.CenterVerificationDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId, ct);
    }

    public async Task<bool> ExistsVerifiedTaxCodeAsync(string taxCode, Guid? excludeCenterId = null, CancellationToken ct = default)
    {
        var normalized = taxCode.Trim().ToLowerInvariant();
        var q = db.StudyCenters.AsNoTracking()
            .Where(x => x.VerificationStatus == CenterVerificationStatuses.Verified &&
                        x.TaxCode != null &&
                        x.TaxCode.ToLower() == normalized);

        if (excludeCenterId.HasValue)
            q = q.Where(x => x.Id != excludeCenterId.Value);

        return await q.AnyAsync(ct);
    }
}

