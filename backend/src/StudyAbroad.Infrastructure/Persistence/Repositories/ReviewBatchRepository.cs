using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ReviewBatchRepository(AppDbContext db) : IReviewBatchRepository
{
    public async Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(
        DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) =>
        await ReviewCandidateQuery.Candidates(db.ChatMessages.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.Role == UserRoles.Admin && u.Status == UserStatuses.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task AddAsync(
        ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit,
        CancellationToken ct = default)
    {
        db.ReviewBatches.Add(batch);
        db.ReviewBatchItems.AddRange(items);
        db.ReviewBatchReviewers.AddRange(reviewers);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);   // một lần lưu: đợt, các câu, người được giao và nhật ký
    }
}
