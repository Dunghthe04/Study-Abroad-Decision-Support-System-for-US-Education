using System.Net.Http.Json;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Infrastructure.Advisor;

/// <summary>Gọi advisor (Python) để LLM viết giải thích cho tập ứng viên CRM đã xếp hạng.</summary>
public class RecommendationAiClient(HttpClient http) : IRecommendationAi
{
    public async Task<AiRankResponse> RankAsync(AiRankRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/recommendations/explain", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiRankResponse>(ct)
               ?? throw new InvalidOperationException("Advisor service returned an empty response.");
    }
}
