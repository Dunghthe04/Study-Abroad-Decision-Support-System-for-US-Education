using System.Text.Json;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Recommendations
{
    public interface IRecommendationService
    {
        //Tạo 1 recommendation
        Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default);

        //Lấy ra recommendation gần đây nhất
        Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default);
    }

    //Khung giữ lại sau khi bỏ thuật toán SAW cũ. Luồng mới (lọc cứng + 3 lượt AI + công thức file tổng quan) sẽ viết vào CreateAsync.
    public class RecommendationService(IRecommendationRepository repository) : IRecommendationService
    {
        public const string RebuildingWarning = "Chức năng gợi ý trường đang được làm lại theo công thức mới.";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public async Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = await repository.GetProfileAsync(userId, ct);
            if (profile is null) return null;
            if (!StudyLevels.IsValid(profile.TargetLevel))
                return new RecommendationResultDto(Guid.Empty, DateTime.UtcNow, profile.TargetLevel, [],
                    ["Hồ sơ chưa chọn bậc học muốn đi. Hãy cập nhật hồ sơ trước khi gợi ý trường."]);

            return new RecommendationResultDto(Guid.Empty, DateTime.UtcNow, profile.TargetLevel, [], [RebuildingWarning]);
        }

        public async Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default)
        {
            var latest = await repository.GetLatestRecommendationAsync(userId, ct);
            if (latest is null) return null;
            var items = JsonSerializer.Deserialize<List<RecommendationItemDto>>(latest.ItemsJson, Json) ?? [];
            return new RecommendationResultDto(latest.Id, latest.CreatedAt, latest.StudyLevel, items, []);
        }
    }
}
