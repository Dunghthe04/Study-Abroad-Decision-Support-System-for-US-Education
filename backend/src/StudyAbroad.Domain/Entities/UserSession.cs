using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [USAS-362] Quản lý phiên làm việc của người dùng.
/// Cho phép thu hồi phiên ngay lập tức khi đăng xuất (đáp ứng tiêu chí AC-3).
/// </summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
