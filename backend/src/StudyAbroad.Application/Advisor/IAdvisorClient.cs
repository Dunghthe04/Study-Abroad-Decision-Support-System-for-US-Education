namespace StudyAbroad.Application.Advisor;

/// <summary>Client for the Python FastAPI advisor service (RAG + LLM).</summary>
public interface IAdvisorClient
{
    Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default);
    Task<EcResponseDto> ReadEcAsync(EcRequestDto request, CancellationToken ct = default);
    Task<ScoreResponseDto> ScoreAsync(ScoreRequestDto request, CancellationToken ct = default);
    Task<PickResponseDto> PickAsync(PickRequestDto request, CancellationToken ct = default);
}
