using System.Security.Claims;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Extensions;

/// <summary>
/// [USAS-362] Tiện ích trích xuất thông tin an toàn từ ClaimsPrincipal (JWT Token).
/// Áp dụng nguyên tắc Chống IDOR: Luôn trích xuất UserId từ Token đã ký số, không nhận từ client param.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// [Chống IDOR] Lấy UserId từ Claims của Token hiện tại.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var idStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    /// <summary>
    /// Lấy Email của người dùng hiện tại từ Token.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// Lấy Vai trò (Role) của người dùng hiện tại từ Token.
    /// </summary>
    public static string? GetRole(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role);

    /// <summary>
    /// Kiểm tra người dùng có phải là Quản trị viên (Admin) hay không.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(UserRoles.Admin);
}
