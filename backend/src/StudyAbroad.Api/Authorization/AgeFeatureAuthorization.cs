using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Authorization;

/// <summary>[Q11] Yêu cầu của policy chặn theo tuổi; tên policy = tên chức năng trong <see cref="AgeFeatures.Rules"/>.</summary>
public sealed class AgeFeatureRequirement(string feature) : IAuthorizationRequirement
{
    public string Feature { get; } = feature;
}

/// <summary>Lý do bị chặn, để <see cref="AgeRestrictionResultHandler"/> trả 403 kèm mã cho frontend.</summary>
public sealed class AgeRestrictedFailureReason(IAuthorizationHandler handler, AgeBlock block)
    : AuthorizationFailureReason(handler, block.NeedParentConsent
        ? "Chức năng này cần phụ huynh xác nhận đồng ý trước khi sử dụng (học sinh dưới 16 tuổi)."
        : "Chức năng này chỉ dành cho người từ 18 tuổi.")
{
    public AgeBlock Block { get; } = block;
}

/// <summary>Đọc ngày sinh từ DB (không lấy từ token) và trạng thái đồng ý của phụ huynh ở mỗi request.</summary>
public sealed class AgeFeatureHandler(IUserRepository users, IParentLinkRepository parentLinks)
    : AuthorizationHandler<AgeFeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AgeFeatureRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return; // chưa đăng nhập: RequireAuthenticatedUser xử lý

        var user = await users.GetUserById(userId.Value);
        if (user is null) return;

        var block = await AgeAccess.CheckAsync(requirement.Feature, user, parentLinks, DateTime.UtcNow);
        if (block is null)
            context.Succeed(requirement);
        else
            context.Fail(new AgeRestrictedFailureReason(this, block));
    }
}

/// <summary>Bị chặn theo tuổi → 403 problem+json { code: "age_restricted", feature, needParentConsent }.
/// Các trường hợp khác giữ nguyên hành vi mặc định.</summary>
public sealed class AgeRestrictionResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string ErrorCode = "age_restricted";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var reason = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailureReasons.OfType<AgeRestrictedFailureReason>().FirstOrDefault()
            : null;
        if (reason is null)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Chức năng bị giới hạn theo độ tuổi",
            Detail = reason.Message,
        };
        problem.Extensions["code"] = ErrorCode;
        problem.Extensions["feature"] = reason.Block.Feature;
        problem.Extensions["needParentConsent"] = reason.Block.NeedParentConsent;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
