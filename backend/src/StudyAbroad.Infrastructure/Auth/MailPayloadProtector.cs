using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>Một mail đã dựng xong, chỉ tồn tại dạng rõ trong RAM.</summary>
public sealed record OutgoingMail(string To, string Subject, string HtmlBody);

public sealed class MailOutboxOptions
{
    /// <summary>base64 của đúng 32 byte (biến MAIL_ENCRYPTION_KEY). Trống thì dẫn xuất từ JWT_SECRET_KEY.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>
/// Mã hóa mail nằm trong bảng email_outbox bằng AES-256-GCM (cùng cách <see cref="TotpService"/> mã hóa khóa TOTP)
/// và tính dedupe_key bằng HMAC-SHA256. Khóa riêng cho mail, không lưu trong DB: có DB mà không có khóa thì
/// không đọc được người nhận, tiêu đề, OTP hay token trong mail, cũng không đoán được dedupe_key thuộc email nào.
/// </summary>
public sealed class MailPayloadProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Lazy<(byte[] Encryption, byte[] Dedupe)> _keys;

    /// <summary>Khóa dựng ở lần dùng đầu: thiếu cấu hình thì lỗi đúng lúc ghi mail, không làm hỏng việc dựng các service khác.</summary>
    public MailPayloadProtector(IOptions<MailOutboxOptions> options, IOptions<JwtOptions> jwt, ILogger<MailPayloadProtector> logger)
    {
        _keys = new Lazy<(byte[], byte[])>(() => DeriveKeys(options.Value.EncryptionKey, jwt.Value.SecretKey, logger));
    }

    private static (byte[], byte[]) DeriveKeys(string? configured, string? jwtSecret, ILogger logger)
    {
        byte[] master;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            master = Convert.FromBase64String(configured.Trim());
            if (master.Length != 32)
                throw new InvalidOperationException("MAIL_ENCRYPTION_KEY phải là base64 của đúng 32 byte (openssl rand -base64 32).");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(jwtSecret))
                throw new InvalidOperationException("Thiếu MAIL_ENCRYPTION_KEY và JWT_SECRET_KEY để mã hóa hàng đợi mail.");
            logger.LogWarning("MAIL_ENCRYPTION_KEY chưa đặt, đang dẫn xuất khóa mã hóa hàng đợi mail từ JWT_SECRET_KEY. Nên đặt biến riêng ở Production.");
            // Nhãn khác nhãn TOTP nên ra khóa khác khóa TOTP
            master = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSecret), 32,
                info: Encoding.UTF8.GetBytes("usas-mail-outbox-encryption"));
        }

        // Khóa HMAC tách khỏi khóa mã hóa: mỗi việc một khóa
        var dedupe = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: Encoding.UTF8.GetBytes("usas-mail-outbox-dedupe"));
        return (master, dedupe);
    }

    public string Protect(OutgoingMail mail)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(mail);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_keys.Value.Encryption, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);
        CryptographicOperations.ZeroMemory(plain);
        return Convert.ToBase64String([.. nonce, .. cipher, .. tag]);
    }

    /// <summary>Null khi bản mã bị sửa, hỏng hoặc mã bằng khóa khác (tag GCM không khớp).</summary>
    public OutgoingMail? Unprotect(string payload)
    {
        try
        {
            var data = Convert.FromBase64String(payload);
            if (data.Length <= NonceSize + TagSize)
                return null;
            var nonce = data.AsSpan(0, NonceSize);
            var cipher = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);
            var tag = data.AsSpan(data.Length - TagSize);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(_keys.Value.Encryption, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain);
            var mail = JsonSerializer.Deserialize<OutgoingMail>(plain);
            CryptographicOperations.ZeroMemory(plain);
            return mail;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Cùng email (không phân biệt hoa thường) và cùng loại thì ra cùng khóa.</summary>
    public string DedupeKey(string email, string kind)
    {
        var input = Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}\n{kind}");
        return Convert.ToHexString(HMACSHA256.HashData(_keys.Value.Dedupe, input)).ToLowerInvariant();
    }
}

/// <summary>Che email trong log: "vietpt@gmail.com" → "v***@gmail.com".</summary>
public static class MailLog
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(trống)";
        var at = email.LastIndexOf('@');
        if (at <= 0)
            return "***";
        return $"{email[0]}***{email[at..]}";
    }
}
