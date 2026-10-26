using System.Security.Cryptography;
using System.Text;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Token trong link mời phụ huynh: chỉ gửi qua mail, DB lưu SHA-256 (hex) như mã OTP.</summary>
public static class ParentInvite
{
    /// <summary>Lời mời hết hạn sau 7 ngày kể từ lần gửi gần nhất.</summary>
    public const int ExpiryDays = 7;

    /// <summary>32 byte ngẫu nhiên, base64url (43 ký tự) để đặt thẳng vào URL.</summary>
    public static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
}
