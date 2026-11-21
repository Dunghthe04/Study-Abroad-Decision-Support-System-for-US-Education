using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Lời mời hoặc thành viên của nhóm gia đình. Chủ gói không có dòng ở đây.</summary>
public class FamilyMember : BaseEntity
{
    public Guid FamilyGroupId { get; set; }
    /// <summary>Null tới khi người được mời chấp nhận.</summary>
    public Guid? UserId { get; set; }
    /// <summary>Email được mời, chữ thường.</summary>
    public string InvitedEmail { get; set; } = string.Empty;
    /// <summary>student | parent (FamilySeats)</summary>
    public string SeatRole { get; set; } = string.Empty;
    /// <summary>FamilyMemberStatuses</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>SHA-256 (hex) của token trong link mời; về null khi đã dùng hoặc hủy.</summary>
    public string? TokenHash { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int InviteSentCount { get; set; }
    public DateTime? LastInviteSentAt { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    /// <summary>FamilyMemberEndReasons</summary>
    public string? EndedReason { get; set; }
    /// <summary>Concurrency token (cột hệ thống xmin): chặn 2 request chấp nhận / hủy cùng lúc.</summary>
    public uint Version { get; set; }
}
