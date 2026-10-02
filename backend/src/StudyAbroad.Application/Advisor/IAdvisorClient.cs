namespace StudyAbroad.Application.Advisor;

/// <summary>Client for the Python FastAPI advisor service (RAG + LLM).</summary>
public interface IAdvisorClient
{
    Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default);
}
