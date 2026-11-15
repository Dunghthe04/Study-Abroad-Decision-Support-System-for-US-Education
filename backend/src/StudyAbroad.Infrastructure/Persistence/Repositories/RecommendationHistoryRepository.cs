using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

// [UC-87] Lịch sử gợi ý: chỉ đọc bảng recommendations mà RecommendationRepository (của luồng gợi ý) ghi vào.
public class RecommendationHistoryRepository(AppDbContext db) : IRecommendationHistoryRepository
{
    public Task<List<Recommendation>> ListByUserAsync(Guid userId, int take, CancellationToken ct = default) =>
        db.Recommendations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Recommendations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
}
