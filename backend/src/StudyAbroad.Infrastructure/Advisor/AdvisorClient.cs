using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.Infrastructure.Advisor;

public class AdvisorClient(HttpClient http) : IAdvisorClient
{
    private static readonly JsonSerializerOptions AdvisorJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/chat", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdvisorChatResponse>(ct)
               ?? throw new InvalidOperationException("Advisor service returned an empty response.");
    }

    public Task<EcResponseDto> ReadEcAsync(EcRequestDto request, CancellationToken ct = default) =>
        PostAsync<EcRequestDto, EcResponseDto>("api/v1/recommend/ec", request, ct);

    public Task<ScoreResponseDto> ScoreAsync(ScoreRequestDto request, CancellationToken ct = default) =>
        PostAsync<ScoreRequestDto, ScoreResponseDto>("api/v1/recommend/score", request, ct);

    public Task<PickResponseDto> PickAsync(PickRequestDto request, CancellationToken ct = default) =>
        PostAsync<PickRequestDto, PickResponseDto>("api/v1/recommend/pick", request, ct);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, request, AdvisorJson, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(AdvisorJson, ct)
               ?? throw new InvalidOperationException("Advisor trả về rỗng.");
    }
}