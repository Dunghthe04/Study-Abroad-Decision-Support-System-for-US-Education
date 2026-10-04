using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Nhật ký mỗi lần gọi AI: độ trễ, token, lỗi (backlog #7, #23, #29).</summary>
public class AiCall : BaseEntity
{
    public Guid? UserId { get; set; }
    /// <summary>chat | recommend | analyze | embed | search</summary>
    public string Kind { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}
