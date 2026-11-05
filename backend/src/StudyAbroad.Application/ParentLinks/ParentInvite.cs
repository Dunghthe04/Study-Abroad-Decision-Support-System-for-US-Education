using System.Security.Cryptography;
using System.Text;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Token trong link mời phụ huynh: chỉ gửi qua mail, DB lưu SHA-256 (hex) như mã OTP.</summary>
public static class ParentInvite
{
    /// <summary>Lời mời hết hạn sau 7 ngày kể từ lần gửi gần nhất.</summary>
    public const int ExpiryDays = 7;

    /// <summary>Tối đa số lần gửi mail mời cho một link trong 24 giờ (giống giới hạn gửi OTP).</summary>
    public const int MaxSendsPerDay = 5;

    /// <summary>Khoảng cách tối thiểu giữa hai lần gửi lại.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    /// <summary>32 byte ngẫu nhiên, base64url (43 ký tự) để đặt thẳng vào URL.</summary>
    public static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    /// <summary>
    /// Sinh token mới cho link (token cũ hết hiệu lực vì hash bị thay), đặt hạn và đếm số lần gửi.
    /// Bộ đếm reset khi lần gửi trước đã quá 24 giờ. Trả token gốc để gửi mail; caller lưu link.
    /// </summary>
    public static string Issue(ParentStudentLink link, DateTime utcNow)
    {
        if (link.LastInviteSentAt is { } last && utcNow - last >= TimeSpan.FromDays(1))
            link.InviteSentCount = 0;

        var token = GenerateToken();
        link.TokenHash = HashToken(token);
        link.TokenExpiresAt = utcNow.AddDays(ExpiryDays);
        link.InviteSentCount++;
        link.LastInviteSentAt = utcNow;
        return token;
    }
}
