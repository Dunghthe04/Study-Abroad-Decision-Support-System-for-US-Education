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
    /// <summary>Kết quả lượt chat (ChatOutcomes), chỉ đặt ở tin trả lời của trợ lý. Null ở tin của học sinh và tin cũ.</summary>
    public string? Outcome { get; set; }
    /// <summary>Tóm tắt lượt cho admin: các ý SLM tách, hành động đã gọi, độ tin cậy, tổng thời gian và token. Chỉ ở tin trả lời của trợ lý.</summary>
    public string? MetaJson { get; set; }
}
