using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Nhật ký thao tác của admin và sự kiện đăng nhập (backlog #16, #17, #25, #28). Xác minh trung tâm, khóa tài khoản, ẩn bài... đều ghi ở đây.</summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    /// <summary>login_ok | login_failed | user.lock | center.verify | post.hide ...</summary>
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
