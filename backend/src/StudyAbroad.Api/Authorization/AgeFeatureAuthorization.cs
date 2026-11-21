using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
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
/// Sai vai trò → 403 kèm câu nói rõ chức năng dành cho loại tài khoản nào.
/// Cần Premium (PremiumFeatureHandler) → 403 problem+json { code: "premium_required", feature }.
/// Thứ tự khi nhiều lý do cùng chặn: tuổi, rồi sai vai trò, rồi Premium.
/// Các trường hợp khác giữ nguyên hành vi mặc định.</summary>
public sealed class AgeRestrictionResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string ErrorCode = "age_restricted";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var failure = authorizeResult.Forbidden ? authorizeResult.AuthorizationFailure : null;
        var reason = failure?.FailureReasons.OfType<AgeRestrictedFailureReason>().FirstOrDefault();
        if (reason is null)
        {
            // Sai vai trò (vd. tài khoản trung tâm mở form hồ sơ học sinh, hay phụ huynh Free gọi route chỉ của học sinh): nói rõ chức năng dành cho ai.
            // Mặc định ASP.NET chỉ trả 403 "Forbidden", người dùng không hiểu vì sao bị chặn. Đi trước premium_required
            // vì nâng cấp gói không giúp được người sai loại tài khoản
            // Có handler đã gọi Fail() (như PremiumFeatureHandler) thì ASP.NET bỏ danh sách yêu cầu chưa đạt (FailedRequirements rỗng),
            // nên ngoài FailedRequirements còn xét lại vai trò của người gọi trên chính policy
            var wrongRole = authorizeResult.Forbidden
                ? failure?.FailedRequirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault()
                    ?? policy.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault(r => !r.AllowedRoles.Any(context.User.IsInRole))
                : null;
            if (wrongRole is not null)
            {
                await WriteForbiddenAsync(context, "Không đúng loại tài khoản", DefaultErrorMessages.WrongRole(wrongRole.AllowedRoles));
                return;
            }

            // Tuổi và vai trò đã xét trước (đến đây không bị chặn theo tuổi, đúng loại tài khoản); chỉ còn thiếu gói Premium thì báo mã premium_required
            if (failure?.FailureReasons.OfType<PremiumRequiredFailureReason>().FirstOrDefault() is { } premium)
            {
                await WriteForbiddenAsync(context, "Cần tài khoản USDSS Premium", premium.Message, new Dictionary<string, object?>
                {
                    ["code"] = PremiumRequiredFailureReason.ErrorCode,
                    ["feature"] = premium.Feature,
                });
                return;
            }

            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        await WriteForbiddenAsync(context, "Chức năng bị giới hạn theo độ tuổi", reason.Message, new Dictionary<string, object?>
        {
            ["code"] = ErrorCode,
            ["feature"] = reason.Block.Feature,
            ["needParentConsent"] = reason.Block.NeedParentConsent,
        });
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string title, string detail, Dictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = title,
            Detail = detail,
        };
        foreach (var (key, value) in extensions ?? [])
            problem.Extensions[key] = value;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
