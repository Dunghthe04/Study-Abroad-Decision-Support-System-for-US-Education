using System.Security.Cryptography;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Auth;

/// <summary>Refresh token: chuỗi ngẫu nhiên 256 bit, chỉ lưu hash trong user_sessions.
/// Phiên sống 7 ngày (Moderator/Admin 8 giờ, UC-03) tính từ lúc đăng nhập, mỗi lần gia hạn thì đổi refresh token mới (rotation).</summary>
public static class RefreshTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>[UC-03] Moderator và Admin: phiên 8 giờ tính từ lúc đăng nhập, gia hạn không kéo dài mốc này.</summary>
    public static readonly TimeSpan StaffLifetime = TimeSpan.FromHours(8);

    /// <summary>[UC-03] Thời hạn phiên theo vai trò: Học sinh, Phụ huynh, Trung tâm 7 ngày; Moderator, Admin 8 giờ.</summary>
    public static TimeSpan LifetimeFor(string? role) =>
        role is UserRoles.Moderator or UserRoles.Admin ? StaffLifetime : Lifetime;

    /// <summary>Hai tab cùng gia hạn: tab chậm hơn gửi token vừa bị đổi trong khoảng này thì chỉ báo thử lại, không thu hồi phiên.</summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    public static string Generate() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
