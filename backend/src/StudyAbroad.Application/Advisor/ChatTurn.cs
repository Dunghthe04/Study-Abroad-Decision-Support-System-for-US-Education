using System.Text.Json;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Đánh giá một lượt chat để ghi vào chat_messages: kết quả (outcome), có cần admin xem không (review_status) và bản tóm tắt cho admin (meta_json).
/// Là logic thuần (không đụng database) để dễ kiểm. Quy tắc outcome:
///   failed    : có bước AI mà MỌI lần gọi đều lỗi (hết thời gian, bị từ chối, Ollama tắt). Gọi lại thành công thì không tính.
///   no_answer : ý ngoài phạm vi loại ambiguous (không hiểu) hoặc exception (ngoài chuyện du học).
///   answered  : còn lại, kể cả câu bị chặn có chủ ý (sensitive), chào, hỏi về hệ thống, xin gặp người thật.
/// failed được xét trước no_answer: Ollama tắt làm SLM dùng kết quả dự phòng thì lỗi do hệ thống, không phải câu khó hiểu.
/// </summary>
public static class ChatTurn
{
    /// <summary>Phiên bản khuôn meta_json, để đọc được các dòng cũ khi khuôn đổi.</summary>
    public const int MetaVersion = 1;

    private static readonly string[] UnansweredSubtypes = ["ambiguous", "exception"];

    private static readonly JsonSerializerOptions MetaJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Outcome(AdvisorSlmDto? slm, IReadOnlyList<AiUsageDto>? usage)
    {
        if (FailedSteps(usage).Count > 0)
            return ChatOutcomes.Failed;

        var outOfScope = slm?.Intents?.Contains("out_of_scope") == true ? slm.OutOfScope : null;
        if (outOfScope is not null && UnansweredSubtypes.Contains(outOfScope))
            return ChatOutcomes.NoAnswer;

        return ChatOutcomes.Answered;
    }

    /// <summary>Lượt không trả lời được hoặc lỗi thì vào hàng đợi của admin (open); lượt trả lời được thì null.</summary>
    public static string? ReviewStatus(string outcome) =>
        outcome == ChatOutcomes.Answered ? null : ChatReviewStatuses.Open;

    /// <summary>Các bước AI (nguồn + skill) có ít nhất một lần lỗi và không lần nào thành công.</summary>
    public static IReadOnlyList<string> FailedSteps(IReadOnlyList<AiUsageDto>? usage) =>
        (usage ?? [])
            .GroupBy(call => (call.Source, call.Purpose))
            .Where(group => group.Any(call => !call.Ok) && !group.Any(call => call.Ok))
            .Select(group => string.IsNullOrEmpty(group.Key.Purpose) ? group.Key.Source : group.Key.Purpose)
            .ToList();

    /// <summary>
    /// Tóm tắt lượt cho admin (cột meta_json). turnMs là thời gian thật của cả lượt; aiLatencyMs là tổng thời gian các lần gọi AI
    /// (lớn hơn turnMs khi có các lần gọi chạy song song). Có chữ của học sinh (nhãn model gán) nên chỉ lưu ở bảng dữ liệu cá nhân.
    /// </summary>
    public static string Meta(
        AdvisorSlmDto? slm, IReadOnlyList<AiUsageDto>? usage, string? action, IReadOnlyList<string>? skills, long turnMs)
    {
        var calls = usage ?? [];
        return JsonSerializer.Serialize(new
        {
            v = MetaVersion,
            slm,
            action,
            skills,
            turnMs,
            aiCalls = calls.Count,
            aiLatencyMs = calls.Sum(c => c.LatencyMs),
            inputTokens = calls.Sum(c => c.InputTokens),
            outputTokens = calls.Sum(c => c.OutputTokens),
            cachedTokens = calls.Sum(c => c.CachedTokens),
            failedSteps = FailedSteps(calls),
        }, MetaJson);
    }
}
