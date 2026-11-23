using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Authorization;

/// <summary>
/// [UC-104] Chặn Ở SERVER mọi API cần đăng nhập khi người dùng chưa đồng ý bản Điều khoản hoặc Chính sách bảo mật bắt buộc:
/// 403 problem+json { code: "policy_acceptance_required" }. Chặn ở giao diện thôi thì gọi API trực tiếp vẫn lọt, bằng chứng đồng ý mất ý nghĩa.
/// Đặt sau UseAuthorization, nên chỉ xét các API đã qua kiểm tra quyền; API công khai (AllowAnonymous) không bị chặn.
/// </summary>
public sealed class PolicyAcceptanceMiddleware(RequestDelegate next)
{
    public const string ErrorCode = "policy_acceptance_required";

    /// <summary>
    /// Luôn mở để người bị chặn vẫn: đọc văn bản và đồng ý (policies, policy-acceptance), biết mình là ai và đăng xuất (auth),
    /// và TỰ XÓA TÀI KHOẢN (auth/me, nằm trong auth): người không đồng ý phải có đường rời đi.
    /// </summary>
    public static readonly string[] ExemptPrefixes = ["/api/v1/auth", "/api/v1/policy-acceptance", "/api/v1/policies"];

    public async Task InvokeAsync(HttpContext context, IPolicyAcceptanceService acceptance)
    {
        if (RequiresCheck(context) && SubjectOf(context.User) is { } subject
            && await acceptance.MustAcceptAsync(subject.UserId, subject.Role, context.RequestAborted))
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Cần đồng ý Điều khoản và Chính sách",
                Detail = "Điều khoản sử dụng hoặc Chính sách bảo mật đã được cập nhật. Vui lòng đọc và đồng ý để tiếp tục sử dụng.",
            };
            problem.Extensions["code"] = ErrorCode;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", context.RequestAborted);
            return;
        }

        await next(context);
    }

    // Chỉ API cần đăng nhập, của người đã đăng nhập, ngoài danh sách luôn mở
    private static bool RequiresCheck(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAuthorizeData>() is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return false;

        var path = context.Request.Path;
        return !ExemptPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ai là người phải đồng ý. Phụ huynh đang nhập hồ sơ thay con managed (claim act) thì chính phụ huynh mới là người
    /// phải đồng ý, không phải tài khoản con: con managed không bao giờ tự đăng nhập để đồng ý.
    /// </summary>
    private static (Guid UserId, string Role)? SubjectOf(System.Security.Claims.ClaimsPrincipal principal)
    {
        if (ActingSessionGuard.ReadActingParentId(principal) is { } parentId)
            return (parentId, UserRoles.Parent);
        return principal.GetUserId() is { } id ? (id, principal.GetRole() ?? string.Empty) : null;
    }
}
