using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-58] Đề nghị tạm ngừng một trung tâm bị gỡ tin nhắn lặp lại nhiều lần. Moderator gửi, Admin quyết định
/// (đặc tả 5.2 và 5.4). Mỗi trung tâm chỉ có tối đa một đề nghị đang chờ.
/// </summary>
public class CenterSuspensionProposal : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>pending | approved | rejected</summary>
    public string Status { get; set; } = "pending";
    /// <summary>Số tin của trung tâm đã bị gỡ tại lúc gửi đề nghị, để Admin thấy căn cứ.</summary>
    public int RemovedMessageCount { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}
