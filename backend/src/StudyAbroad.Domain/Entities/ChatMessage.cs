using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Tin nhắn trong cuộc chat (backlog #10, #11, #12, #23).</summary>
public class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    /// <summary>user | assistant | system</summary>
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Step { get; set; }
    /// <summary>nguồn trích dẫn: title, url</summary>
    public string? SourcesJson { get; set; }
    /// <summary>trung tâm AI gợi ý khi chuyển sang trung tâm</summary>
    public string? SuggestedCentersJson { get; set; }
    /// <summary>null nếu AI trả lời được; open | resolved | ignored nếu chưa, để admin bổ sung tài liệu</summary>
    public string? ReviewStatus { get; set; }
}
