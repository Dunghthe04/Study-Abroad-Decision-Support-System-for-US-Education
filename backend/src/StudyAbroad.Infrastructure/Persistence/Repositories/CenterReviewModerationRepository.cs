using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-57] Moderator gỡ đánh giá trong app.center_reviews.</summary>
public class CenterReviewModerationRepository(AppDbContext db) : ICenterReviewModerationRepository
{
    public Task<CenterReview?> GetForUpdateAsync(Guid centerId, Guid reviewId, CancellationToken ct = default) =>
        db.CenterReviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.StudyCenterId == centerId, ct);

    public async Task SaveAsync(CenterReview review, CancellationToken ct = default)
    {
        db.CenterReviews.Update(review);
        await db.SaveChangesAsync(ct);
    }
}
