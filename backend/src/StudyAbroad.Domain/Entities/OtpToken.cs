using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Mã xác thực một lần: xác minh email, quên mật khẩu (backlog #17).</summary>
public class OtpToken : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>verify_email | reset_password</summary>
    public string Purpose { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int Attempts { get; set; }
}
