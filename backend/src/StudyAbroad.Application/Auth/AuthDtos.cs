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

    public enum AuthError {
        None,
        Validation,
        EmailTaken,
        InvalidCredentials,   // sai tài khoản hoặc mật khẩu → 401
        Locked,               // tài khoản bị khóa → 423
    };

    // [USAS-362] Hộp trả kết quả từ AuthService
    public record AuthResult(UserDto? User, AuthResponse? Response = null, AuthError Error = AuthError.None, string? Message = null)
    {
        public static AuthResult Ok(UserDto user) => new(user);
        public static AuthResult Ok(AuthResponse response) => new(response.User, response);
        public static AuthResult Fail(AuthError error, string message) => new(null, null, error, message);
    }

    public record LoginRequest(string Email, string Password);
}
