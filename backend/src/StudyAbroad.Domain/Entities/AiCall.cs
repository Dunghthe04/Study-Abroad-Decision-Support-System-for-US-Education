using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Nhật ký mỗi lần gọi AI: độ trễ, token, lỗi (backlog #7, #23, #29).</summary>
public class AiCall : BaseEntity
{
    public Guid? UserId { get; set; }
    /// <summary>Nguồn gọi: claude | ollama (kiểu cũ chat | recommend | analyze | embed | search vẫn hợp lệ)</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Công việc của lần gọi = tên file skill (slm_intents, skill_tra_loi...), để báo cáo token theo tính năng. Null với dòng cũ.</summary>
    public string? Purpose { get; set; }
    /// <summary>Tin nhắn trả lời của trợ lý sinh ra từ lần gọi này (lượt chat). Null khi gọi ngoài chat hoặc dòng cũ.</summary>
    public Guid? ChatMessageId { get; set; }
    public string? Model { get; set; }
    public string? SkillVersion { get; set; }
    public string? ModelVersion { get; set; }
    public decimal? CostUsd { get; set; }
    public string? PlanCode { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    /// <summary>Token đọc từ cache của Claude (tính tiền khác token thường). 0 với Ollama.</summary>
    public int? CachedTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int? LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public bool FallbackUsed { get; set; } = false;
}
