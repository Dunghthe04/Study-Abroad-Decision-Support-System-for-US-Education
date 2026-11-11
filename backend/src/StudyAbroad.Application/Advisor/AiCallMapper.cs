using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Đổi số đo một lần gọi AI do advisor đo (AiUsageDto) thành dòng nhật ký ai_calls. Là logic thuần để dễ kiểm.
/// Cắt chữ theo độ rộng cột để một giá trị dài bất thường không làm hỏng cả lần ghi.
/// </summary>
public static class AiCallMapper
{
    private const int KindMax = 16;       // khớp HasMaxLength của cột kind
    private const int PurposeMax = 64;    // khớp cột purpose
    private const int ModelMax = 64;      // khớp cột model
    private const int ErrorMax = 1000;    // khớp cột error

    public static AiCall ToEntity(AiUsageDto usage, Guid? userId, Guid? chatMessageId, AiPricing pricing) => new()
    {
        UserId = userId,
        ChatMessageId = chatMessageId,
        Kind = Cut(usage.Source, KindMax) ?? string.Empty,
        Purpose = Cut(usage.Purpose, PurposeMax),
        SkillVersion = Blank(usage.SkillVersion),
        Model = Cut(usage.Model, ModelMax),
        PromptTokens = usage.InputTokens,
        CompletionTokens = usage.OutputTokens,
        CachedTokens = usage.CachedTokens,
        TotalTokens = usage.InputTokens + usage.OutputTokens,   // token vào cộng token ra
        LatencyMs = usage.LatencyMs,
        Success = usage.Ok,
        Error = Cut(usage.Error, ErrorMax),
        // Tiền tính và lưu ngay lúc gọi: đổi giá sau này không làm lệch dòng cũ. Model Claude lạ thì null (chưa biết giá).
        // Lần gọi lỗi chưa tiêu token nên tiền là 0, không phải "chưa biết giá"
        CostUsd = usage.Ok
            ? pricing.Cost(usage.Source, usage.Model, usage.InputTokens, usage.OutputTokens)
            : 0m,
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static string? Cut(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= max ? text : text[..max];
}
