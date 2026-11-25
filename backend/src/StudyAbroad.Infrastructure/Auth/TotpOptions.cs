namespace StudyAbroad.Infrastructure.Auth;

/// <summary>[UC-03 3d] Cấu hình TOTP. EncryptionKey nạp từ biến môi trường TOTP_ENCRYPTION_KEY (base64 của 32 byte).</summary>
public class TotpOptions
{
    /// <summary>Khóa AES-256 mã hóa cột users.totp_secret, base64 32 byte (openssl rand -base64 32). Trống thì dẫn xuất từ JWT_SECRET_KEY.</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Tên hiện trong ứng dụng xác thực (Google Authenticator, Microsoft Authenticator...).</summary>
    public string Issuer { get; set; } = "USAS";
}
