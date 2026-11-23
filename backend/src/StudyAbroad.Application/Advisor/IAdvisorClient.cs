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

    /// <summary>
    /// Nhờ advisor tạo vector cho các dòng kho tri thức còn thiếu (chạy nền, trả lời ngay). Gọi sau khi moderator nhập hoặc sửa dòng.
    /// Mặc định (bộ giả trong test, bản advisor chưa có API này) ném NotSupportedException; chỗ gọi phải chịu được lỗi.
    /// </summary>
    Task<KnowledgeEmbedDto> EmbedKnowledgeAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("Advisor chưa hỗ trợ tạo vector cho kho tri thức.");

    /// <summary>Hỏi tiến độ tạo vector (chỉ đọc, không bắt đầu gì). Started luôn false. Mặc định ném NotSupportedException như trên.</summary>
    Task<KnowledgeEmbedDto> GetKnowledgeEmbedStatusAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("Advisor chưa hỗ trợ hỏi tiến độ tạo vector.");

}
