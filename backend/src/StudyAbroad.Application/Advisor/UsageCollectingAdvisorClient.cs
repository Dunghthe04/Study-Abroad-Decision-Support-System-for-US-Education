namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Bọc IAdvisorClient: sau mỗi lần gọi advisor thành công, bỏ aiUsage của phản hồi vào giỏ chung (IAiUsageCollector).
/// ChatService và RecommendationService đều nhận IAdvisorClient từ DI nên mọi lần gọi tự được gom mà không phải sửa từng chỗ gọi.
/// Phản hồi được trả nguyên vẹn. Lần gọi lỗi (ném ngoại lệ) không có số đo nên không ghi gì, lỗi vẫn ném tiếp.
/// </summary>
public class UsageCollectingAdvisorClient(IAdvisorClient inner, IAiUsageCollector collector) : IAdvisorClient
{
    public async Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
    {
        var response = await inner.ChatAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    public async Task<AdvisorChatResponse> ChatStreamAsync(
        AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default)
    {
        var response = await inner.ChatStreamAsync(request, onEvent, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    public async Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default)
    {
        var response = await inner.ExplainAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    public async Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default)
    {
        var response = await inner.WriteGoalNoteAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    public async Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default)
    {
        var response = await inner.WriteScenarioNoteAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    public async Task<RecommendNoteResponseDto> WriteRecommendNoteAsync(RecommendNoteRequestDto request, CancellationToken ct = default)
    {
        var response = await inner.WriteRecommendNoteAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }

    // Dựng lại câu đang chờ không gọi AI nên không có số đo để gom; vẫn phải chuyển tiếp, nếu không sẽ rơi vào bản mặc định (không có câu nào)
    public Task<PendingQuestionDto> PendingQuestionAsync(AdvisorPendingQuestionRequest request, CancellationToken ct = default) =>
        inner.PendingQuestionAsync(request, ct);

    // Tạo vector gọi dịch vụ embedding, không có aiUsage trong phản hồi; vẫn phải chuyển tiếp, nếu không sẽ rơi vào bản mặc định (ném lỗi)
    public Task<KnowledgeEmbedDto> EmbedKnowledgeAsync(CancellationToken ct = default) =>
        inner.EmbedKnowledgeAsync(ct);

    public async Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default)
    {
        var response = await inner.WriteCompareNoteAsync(request, ct);
        collector.Add(response.AiUsage);
        return response;
    }
}
