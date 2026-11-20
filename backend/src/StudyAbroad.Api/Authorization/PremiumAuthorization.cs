using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Authorization;

/// <summary>Tên policy chặn tính năng Premium (gắn bằng [Authorize(Policy = ...)]).</summary>
public static class PremiumPolicies
{
    /// <summary>Tạo, đổi ưu tiên và mô phỏng gợi ý trường; tạo kịch bản what-if. Chỉ xem lịch sử, đổi tên, xóa thì không chặn.</summary>
    public const string Recommendations = "premium_recommendations";
}

/// <summary>Yêu cầu của policy Premium; feature là tên tính năng trả về cho frontend ({ code: "premium_required", feature }).</summary>
public sealed class PremiumFeatureRequirement(string feature) : IAuthorizationRequirement
{
    public string Feature { get; } = feature;
}

/// <summary>Lý do bị chặn, để <see cref="AgeRestrictionResultHandler"/> trả 403 kèm mã premium_required cho frontend.</summary>
public sealed class PremiumRequiredFailureReason(IAuthorizationHandler handler, string feature)
    : AuthorizationFailureReason(handler, "Tính năng này cần tài khoản USDSS Premium.")
{
    public const string ErrorCode = "premium_required";

    public string Feature { get; } = feature;
}

/// <summary>
/// Đọc gói từ DB ở mỗi request (không tin token): có kỳ trả phí còn hạn thì qua, nhân viên luôn qua.
/// Phiên phụ huynh làm thay mang id và vai trò học sinh của con nên dùng gói của con (phiên đó là tài khoản của con).
/// Chỉ xét tier, không xét còn token hay không: Premium hết token vẫn mở gợi ý trường (O2, không tốn AI).
/// </summary>
public sealed class PremiumFeatureHandler(IEntitlementService entitlements) : AuthorizationHandler<PremiumFeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PremiumFeatureRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return; // chưa đăng nhập: RequireAuthenticatedUser xử lý

        if (await entitlements.IsPremiumAsync(userId.Value, context.User.GetRole() ?? UserRoles.Student, DateTime.UtcNow))
            context.Succeed(requirement);
        else
            context.Fail(new PremiumRequiredFailureReason(this, requirement.Feature));
    }
}
