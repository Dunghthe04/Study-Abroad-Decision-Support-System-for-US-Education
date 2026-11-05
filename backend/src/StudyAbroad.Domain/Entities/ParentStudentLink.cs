using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Lời mời / liên kết giữa một học sinh và một phụ huynh (nhiều–nhiều: con có thể có bố và mẹ, phụ huynh có thể có nhiều con).</summary>
public class ParentStudentLink : BaseEntity
{
    public Guid StudentUserId { get; set; }
    /// <summary>Email học sinh khai cho phụ huynh, chữ thường.</summary>
    public string ParentEmail { get; set; } = string.Empty;
    /// <summary>Null khi phụ huynh chưa chấp nhận lời mời.</summary>
    public Guid? ParentUserId { get; set; }
    /// <summary>pending | active | revoked | expired</summary>
    public string Status { get; set; } = Constants.ParentLinkStatuses.Pending;
    /// <summary>SHA-256 (hex) của token trong link mời; về null sau khi dùng hoặc thu hồi để token chỉ dùng được 1 lần.</summary>
    public string? TokenHash { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public int InviteSentCount { get; set; }
    public DateTime? LastInviteSentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    /// <summary>Bản ghi đồng ý tạo khi phụ huynh chấp nhận.</summary>
    public Guid? ConsentId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    /// <summary>Concurrency token (cột hệ thống xmin của Postgres): chặn 2 request chấp nhận / rút cùng lúc.</summary>
    public uint Version { get; set; }
}
