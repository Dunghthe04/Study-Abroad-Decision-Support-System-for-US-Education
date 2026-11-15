namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Số đo của MỘT lần gọi AI (Claude hoặc Ollama) do advisor đo, nằm trong khóa aiUsage của phản hồi advisor
/// (advisor/app/schemas/chat.py: AiUsageCall). Backend ghi mỗi phần tử thành một dòng ai_calls.
/// Source: claude | ollama. Purpose: tên file skill (slm_intents, skill_tra_loi...), rỗng với embedding.
/// SkillVersion: số phiên bản ở dòng đầu file skill. CachedTokens: token Claude đọc từ cache (0 với Ollama).
/// Ok = false là lần gọi lỗi (Error có lý do ngắn, token bằng 0). Phản hồi của advisor cũ không có khóa này thì để trống.
/// </summary>
public record AiUsageDto(
    string Source = "",
    string Model = "",
    string Purpose = "",
    string SkillVersion = "",
    int InputTokens = 0,
    int OutputTokens = 0,
    int CachedTokens = 0,
    bool Ok = true,
    string? Error = null,
    int LatencyMs = 0);
