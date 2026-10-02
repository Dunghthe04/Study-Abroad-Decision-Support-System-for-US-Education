using System.Net.Http.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.Infrastructure.Advisor;

public class AdvisorClient(HttpClient http) : IAdvisorClient
{
    public async Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/chat", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdvisorChatResponse>(ct)
               ?? throw new InvalidOperationException("Advisor service returned an empty response.");
    }
}
