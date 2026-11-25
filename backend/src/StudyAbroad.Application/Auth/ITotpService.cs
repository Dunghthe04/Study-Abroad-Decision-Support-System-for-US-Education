namespace StudyAbroad.Application.Auth;

/// <summary>
/// [UC-03 3d] Xác thực hai lớp TOTP cho Moderator, Administrator.
/// Tính và kiểm mã dùng thư viện Otp.NET (RFC 6238: 6 số, bước 30 giây), không tự viết thuật toán.
/// Khóa bí mật lưu DB đã mã hóa AES-GCM; Protect/Unprotect chuyển giữa bản rõ Base32 và bản mã.
/// </summary>
public interface ITotpService
{
    /// <summary>Sinh khóa bí mật mới (160 bit), trả dạng Base32 để người dùng nhập tay vào ứng dụng xác thực.</summary>
    string GenerateSecret();

    /// <summary>Chuỗi otpauth:// để frontend vẽ QR (ứng dụng xác thực quét chuỗi này).</summary>
    string BuildOtpAuthUri(string base32Secret, string accountEmail);

    /// <summary>Kiểm mã 6 số tại thời điểm <paramref name="nowUtc"/>, cho lệch ±1 bước 30 giây do đồng hồ điện thoại lệch.</summary>
    bool VerifyCode(string base32Secret, string code, DateTime nowUtc);

    /// <summary>Mã hóa khóa Base32 để lưu cột users.totp_secret.</summary>
    string Protect(string base32Secret);

    /// <summary>Giải mã cột users.totp_secret về khóa Base32. Bản mã hỏng hoặc sai khóa mã hóa thì trả null.</summary>
    string? Unprotect(string protectedSecret);
}
