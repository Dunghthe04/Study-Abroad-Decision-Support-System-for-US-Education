using System.Net.Http.Json;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Infrastructure.Advisor;

/// <summary>Gọi advisor (Python): LLM đọc chữ của từng hoạt động thành thuộc tính, công thức của nhóm tính điểm 0–4.</summary>
public class ExtracurricularAiClient(HttpClient http) : IExtracurricularAi
{
    public async Task<ExtracurricularScoreResponse> ScoreAsync(ExtracurricularScoreRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/profile/extracurricular", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExtracurricularScoreResponse>(ct)
               ?? throw new InvalidOperationException("Advisor service returned an empty response.");
    }
}
