using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [USAS-362] Quản lý phiên làm việc của người dùng.
/// Cho phép thu hồi phiên ngay lập tức khi đăng xuất (đáp ứng tiêu chí AC-3).
/// </summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Hash của access token đang dùng. Mỗi lần gia hạn thì đổi, nên access token cũ hết tác dụng ngay.</summary>
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Hạn của cả phiên (7 ngày từ lúc đăng nhập). Gia hạn không kéo dài mốc này.</summary>
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    /// <summary>Hash SHA-256 của refresh token hiện tại.</summary>
    public string? RefreshTokenHash { get; set; }
    /// <summary>Hash của refresh token vừa bị đổi. Dùng lại token này sau 30 giây thì coi là bị lộ, thu hồi phiên.</summary>
    public string? PreviousRefreshTokenHash { get; set; }
}
