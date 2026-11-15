using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-45] Lưu đánh giá do học sinh / phụ huynh gửi vào app.center_reviews.</summary>
public class CenterReviewSubmissionRepository(AppDbContext db) : ICenterReviewSubmissionRepository
{
    public Task<Lead?> FindLatestLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) =>
        db.Leads.AsNoTracking()
            .Where(l => l.StudyCenterId == centerId && (l.UserId == userId || l.SubmittedByUserId == userId))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasReviewedAsync(Guid userId, Guid centerId, CancellationToken ct = default) =>
        db.CenterReviews.AsNoTracking().AnyAsync(r => r.UserId == userId && r.StudyCenterId == centerId, ct);

    public async Task AddAsync(CenterReview review, CancellationToken ct = default)
    {
        db.CenterReviews.Add(review);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CenterReviewDto?> GetPublicDtoAsync(Guid reviewId, CancellationToken ct = default)
    {
        var row = await (
            from r in db.CenterReviews.AsNoTracking()
            where r.Id == reviewId
            join u in db.Users.AsNoTracking() on r.UserId equals u.Id
            select new { r.Id, u.FullName, u.Role, r.IsVerified, r.CreatedAt, r.Rating, r.Comment })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        return new CenterReviewDto(
            row.Id,
            CenterReviewRepository.MaskName(row.FullName),
            row.Role == UserRoles.Parent ? UserRoles.Parent : UserRoles.Student,
            row.IsVerified,
            row.CreatedAt,
            row.Rating,
            row.Comment);
    }
}
