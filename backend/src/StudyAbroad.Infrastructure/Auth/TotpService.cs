using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpNet;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>
/// [UC-03 3d] TOTP bằng Otp.NET và mã hóa khóa bí mật bằng AES-GCM (System.Security.Cryptography).
/// Khóa mã hóa lấy từ TOTP_ENCRYPTION_KEY; thiếu thì dẫn xuất từ JWT_SECRET_KEY bằng HKDF và ghi cảnh báo,
/// để môi trường dev chạy được mà không phải thêm biến. Đổi khóa mã hóa thì mọi khóa TOTP đã lưu không đọc được,
/// người dùng phải được Admin đặt lại xác thực hai lớp.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    private readonly byte[] _key;
    private readonly string _issuer;

    public TotpService(IOptions<TotpOptions> options, IOptions<JwtOptions> jwt, ILogger<TotpService> logger)
    {
        _issuer = options.Value.Issuer;
        var configured = options.Value.EncryptionKey;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _key = Convert.FromBase64String(configured.Trim());
            if (_key.Length != 32)
                throw new InvalidOperationException("TOTP_ENCRYPTION_KEY phải là base64 của đúng 32 byte (openssl rand -base64 32).");
        }
        else
        {
            var jwtSecret = jwt.Value.SecretKey;
            if (string.IsNullOrWhiteSpace(jwtSecret))
                throw new InvalidOperationException("Thiếu TOTP_ENCRYPTION_KEY và JWT_SECRET_KEY để mã hóa khóa TOTP.");
            logger.LogWarning("TOTP_ENCRYPTION_KEY chưa đặt, đang dẫn xuất khóa mã hóa TOTP từ JWT_SECRET_KEY. Nên đặt biến riêng ở Production.");
            _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSecret), 32,
                info: Encoding.UTF8.GetBytes("usas-totp-secret-encryption"));
        }
    }

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildOtpAuthUri(string base32Secret, string accountEmail) =>
        new OtpUri(OtpType.Totp, base32Secret, accountEmail, _issuer).ToString();

    public bool VerifyCode(string base32Secret, string code, DateTime nowUtc)
    {
        code = (code ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            return false;
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(nowUtc, code, out _, Window);
    }

    public string Protect(string base32Secret)
    {
        var plain = Encoding.UTF8.GetBytes(base32Secret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. cipher, .. tag]);
    }

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            var data = Convert.FromBase64String(protectedSecret);
            if (data.Length <= NonceSize + TagSize)
                return null;
            var nonce = data.AsSpan(0, NonceSize);
            var cipher = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);
            var tag = data.AsSpan(data.Length - TagSize);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
