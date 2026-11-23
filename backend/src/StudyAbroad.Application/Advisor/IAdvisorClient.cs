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
    Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default);
    Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default);
    Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default);
    Task<RecommendNoteResponseDto> WriteRecommendNoteAsync(RecommendNoteRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Dựng lại nút và công cụ của câu hồ sơ đang chờ khi mở lại phiên chat cũ. Chỉ đọc, advisor không gọi AI (không có token để gom).
    /// Mặc định (bộ giả trong test, bản advisor chưa có API này) là không có câu nào đang chờ.
    /// </summary>
    Task<PendingQuestionDto> PendingQuestionAsync(AdvisorPendingQuestionRequest request, CancellationToken ct = default) =>
        Task.FromResult(PendingQuestionDto.None);

}
