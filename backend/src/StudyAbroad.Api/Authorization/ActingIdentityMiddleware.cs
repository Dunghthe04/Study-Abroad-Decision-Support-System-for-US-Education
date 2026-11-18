using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Controllers;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Api.Authorization;

/// <summary>1.5b: khi phụ huynh đang nhập cho con managed, chỉ các API dùng hồ sơ mới chạy dưới tên con.
/// Con managed chỉ là "hồ sơ nào đang được dùng"; còn lại (trường, trung tâm, tài khoản, thông báo...) vẫn là phụ huynh.</summary>
public static class ActingScope
{
    // API đọc / ghi hồ sơ hoặc kết quả tính từ hồ sơ: dữ liệu thuộc con
    public static readonly IReadOnlySet<string> ChildControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ProfileFlow", "ProfileMetadata", "AcademicProfile", "AcademicAnalysis", "FinancialProfile",
        "Recommendations", "RecommendationHistory", "Scenarios", "TargetSchools",
        "Advisor",   // chat: phần hồ sơ dùng hồ sơ của con, câu hỏi khác trả lời như thường
        "Acting",    // đọc thanh "Đang nhập cho ..." từ token của con
    };

    public static bool UsesChildIdentity(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName is { } name && ChildControllers.Contains(name);

    // Cùng các claim TokenService ghi cho phụ huynh khi đăng nhập
    public static ClaimsPrincipal ParentPrincipal(User parent) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, parent.Id.ToString()),
            new Claim(ClaimTypes.Email, parent.Email),
            new Claim(ClaimTypes.Name, parent.FullName),
            new Claim(ClaimTypes.Role, parent.Role),
            new Claim("status", parent.Status),
        ], JwtBearerDefaults.AuthenticationScheme));
}

/// <summary>Đặt giữa UseAuthentication và UseAuthorization: API ngoài nhóm hồ sơ thấy phụ huynh, không thấy con.
/// Phiên làm thay đã được kiểm tra ở OnTokenValidated (phụ huynh còn quyền, liên kết còn hiệu lực).</summary>
public sealed class ActingIdentityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserRepository users)
    {
        if (ActingSessionGuard.ReadActingParentId(context.User) is { } parentId && !ActingScope.UsesChildIdentity(context.GetEndpoint()))
        {
            var parent = await users.GetUserById(parentId);
            if (parent is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            context.User = ActingScope.ParentPrincipal(parent);
        }

        await next(context);
    }
}
