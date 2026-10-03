using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    //Dùng record vì DTO chỉ chứa dữ liệu, không thay đổi sau khi tạo
    //Body FE
    public record RegisterRequest(string Email, string Password, string FullName, string Role, bool? ParentAcknowledged = null);

    //Return FE (except password)
    public record UserDto(Guid Id, string Email, string FullName, string Role, string Status);

    public enum AuthError {
        None,
        Validation,
        EmailTaken,
    };

    //Hộp trả kết quả
    public record AuthResult(UserDto? User, AuthError Error = AuthError.None, string? Message = null)
    {
        public static AuthResult Ok(UserDto user) => new(user);
        public static AuthResult Fail(AuthError error, string message) => new(null, error, message);
    }

}
