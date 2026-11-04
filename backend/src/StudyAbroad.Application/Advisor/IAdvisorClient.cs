namespace StudyAbroad.Application.Advisor;

/// <summary>Client for the Python FastAPI advisor service (RAG + LLM).</summary>
public interface IAdvisorClient
{
    Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Như ChatAsync, nhưng advisor gửi từng ý khi xong: mỗi tin tiến độ hoặc câu trả lời một ý gọi onEvent ngay,
    /// kết quả cuối giống ChatAsync. Mặc định (bản advisor không hỗ trợ stream) chỉ gọi ChatAsync, không có tin nào.
    /// </summary>
    Task<AdvisorChatResponse> ChatStreamAsync(
        AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default) =>
        ChatAsync(request, ct);
    Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default);

}
