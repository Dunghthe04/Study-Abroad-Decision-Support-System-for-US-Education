using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    // [USAS-362] Request đăng ký từ client: Phone là thông tin liên hệ không bắt buộc
    public record RegisterRequest(string Email, string Password, string FullName, string Role, bool? ParentAcknowledged = null, string? Phone = null);

    // [USAS-362] Thông tin người dùng trả về cho client (không chứa password_hash)
    public record UserDto(Guid Id, string Email, string FullName, string Role, string Status, string? Phone = null);

    // [USAS-362] Response trả về khi đăng nhập thành công: bao gồm JWT token và thông tin người dùng
    public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

    public record LoginRequest(string Email, string Password);

    // [USAS-12] Requests cho quy trình OTP & Quên mật khẩu
    public record VerifyEmailRequest(string Email, string OtpCode);
    public record ResendOtpRequest(string Email, string Purpose);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);

    public enum AuthError {
        None,
        Validation,
        EmailTaken,
        PhoneTaken,           // trùng SĐT khi đăng ký → 409
        InvalidCredentials,   // sai tài khoản hoặc mật khẩu → 401
        Locked,               // tài khoản bị khóa bởi Admin (vĩnh viễn) → 423
        TempLocked,           // tài khoản bị tạm khóa sau 5 lần sai mật khẩu → 423
        Unverified,           // tài khoản chưa kích hoạt email OTP → 403
        OtpInvalid,           // mã OTP không đúng hoặc đã bị vô hiệu hóa sau 5 lần nhập sai → 400
        OtpExpired,           // mã OTP đã hết hạn (> 5 phút) → 400
    };

    // [USAS-362] Hộp trả kết quả từ AuthService
    public record AuthResult(UserDto? User, AuthResponse? Response = null, AuthError Error = AuthError.None, string? Message = null)
    {
        public static AuthResult Ok(UserDto user, string? message = null) => new(user, null, AuthError.None, message);
        public static AuthResult Ok(AuthResponse response) => new(response.User, response);
        public static AuthResult Ok(string message) => new(null, null, AuthError.None, message);
        public static AuthResult Fail(AuthError error, string message) => new(null, null, error, message);
    }
}
