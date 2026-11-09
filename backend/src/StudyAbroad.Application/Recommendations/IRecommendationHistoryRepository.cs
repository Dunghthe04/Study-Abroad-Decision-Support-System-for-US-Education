using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

/// <summary>Đọc các lần chạy gợi ý đã lưu (bảng recommendations, UC-87). Chỉ đọc, không ghi.</summary>
public interface IRecommendationHistoryRepository
{
    /// <summary>Các lần chạy gần nhất của một học sinh, mới nhất trước, tối đa <paramref name="take"/> lần.</summary>
    Task<List<Recommendation>> ListByUserAsync(Guid userId, int take, CancellationToken ct = default);

    Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
