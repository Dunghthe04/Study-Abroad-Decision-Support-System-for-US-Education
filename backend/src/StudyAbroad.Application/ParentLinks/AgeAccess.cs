using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Trạng thái đồng ý của phụ huynh trả cho client.</summary>
public static class ParentConsentStatuses
{
    public const string Granted = "granted";
    /// <summary>Có lời mời đang chờ phụ huynh xác nhận.</summary>
    public const string Pending = "pending";
    public const string None = "none";
    /// <summary>Phụ huynh, center, admin, học sinh từ 18 hoặc chưa có ngày sinh.</summary>
    public const string NotRequired = "not_required";
}

public sealed record AgeAccessInfo(string? AgeGroup, string ParentConsentStatus, List<string> BlockedFeatures);

/// <summary>[Q11] Tính nhóm tuổi và các chức năng đang bị chặn theo <see cref="AgeFeatures.Rules"/>.
/// Dùng chung cho UserDto (frontend ẩn nút) và policy ở Api (chặn thật).</summary>
public static class AgeAccess
{
    /// <summary>1.5: con managed chỉ do phụ huynh (người lớn) thao tác, không phải trẻ tự dùng.
    /// Luật dưới 16 / dưới 18 chỉ áp cho tài khoản của chính trẻ. Ngày sinh vẫn dùng để phân tích hồ sơ.</summary>
    public static bool IsOperatedByParent(User user) => user.Status == UserStatuses.Managed;

    public static bool IsParentConsentRequired(User user, DateOnly today) =>
        user.Role == UserRoles.Student && user.DateOfBirth is not null
        && AgePolicy.GetAge(user.DateOfBirth.Value, today) < AgeGroups.AdultAge;

    public static async Task<AgeAccessInfo> EvaluateAsync(User user, IParentLinkRepository parentLinks, DateTime utcNow, CancellationToken ct = default)
    {
        var today = AgePolicy.TodayInVietnam(utcNow);
        var ageGroup = user.Role == UserRoles.Student ? AgePolicy.GetAgeGroup(user.DateOfBirth, today) : null;

        if (IsOperatedByParent(user))
            return new AgeAccessInfo(ageGroup, ParentConsentStatuses.NotRequired, []);

        if (!IsParentConsentRequired(user, today))
            return new AgeAccessInfo(ageGroup, ParentConsentStatuses.NotRequired,
                AgeFeatures.GetBlocked(user.Role, user.DateOfBirth, today, hasParentConsent: false));

        var granted = await parentLinks.HasGrantedConsentAsync(user.Id, ct);
        var status = ParentConsentStatuses.Granted;
        if (!granted)
        {
            var links = await parentLinks.GetByStudentAsync(user.Id, ct);
            // Lời mời quá 7 ngày (UC-115: Hết hạn) không còn tính là đang chờ, dù chưa ai mở link
            status = links.Any(l => l.Status == ParentLinkStatuses.Pending && !(l.TokenExpiresAt <= utcNow))
                ? ParentConsentStatuses.Pending : ParentConsentStatuses.None;
        }
        return new AgeAccessInfo(ageGroup, status, AgeFeatures.GetBlocked(user.Role, user.DateOfBirth, today, granted));
    }

    /// <summary>Kiểm tra một chức năng; chỉ tra DB khi đồng ý của phụ huynh có thể đổi kết quả.</summary>
    public static async Task<AgeBlock?> CheckAsync(string feature, User user, IParentLinkRepository parentLinks, DateTime utcNow, CancellationToken ct = default)
    {
        if (IsOperatedByParent(user))
            return null;
        var today = AgePolicy.TodayInVietnam(utcNow);
        var block = AgeFeatures.Check(feature, user.Role, user.DateOfBirth, today, hasParentConsent: false);
        if (block is null || !block.NeedParentConsent)
            return block;
        return await parentLinks.HasGrantedConsentAsync(user.Id, ct) ? null : block;
    }
}
