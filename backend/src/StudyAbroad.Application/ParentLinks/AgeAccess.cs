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
    public static bool IsParentConsentRequired(User user, DateOnly today) =>
        user.Role == UserRoles.Student && user.DateOfBirth is not null
        && AgePolicy.GetAge(user.DateOfBirth.Value, today) < AgeGroups.AdultAge;

    public static async Task<AgeAccessInfo> EvaluateAsync(User user, IParentLinkRepository parentLinks, DateTime utcNow, CancellationToken ct = default)
    {
        var today = AgePolicy.TodayInVietnam(utcNow);
        var ageGroup = user.Role == UserRoles.Student ? AgePolicy.GetAgeGroup(user.DateOfBirth, today) : null;

        if (!IsParentConsentRequired(user, today))
            return new AgeAccessInfo(ageGroup, ParentConsentStatuses.NotRequired,
                AgeFeatures.GetBlocked(user.Role, user.DateOfBirth, today, hasParentConsent: false));

        var granted = await parentLinks.HasGrantedConsentAsync(user.Id, ct);
        var status = ParentConsentStatuses.Granted;
        if (!granted)
        {
            var links = await parentLinks.GetByStudentAsync(user.Id, ct);
            status = links.Any(l => l.Status == ParentLinkStatuses.Pending) ? ParentConsentStatuses.Pending : ParentConsentStatuses.None;
        }
        return new AgeAccessInfo(ageGroup, status, AgeFeatures.GetBlocked(user.Role, user.DateOfBirth, today, granted));
    }

    /// <summary>Kiểm tra một chức năng; chỉ tra DB khi đồng ý của phụ huynh có thể đổi kết quả.</summary>
    public static async Task<AgeBlock?> CheckAsync(string feature, User user, IParentLinkRepository parentLinks, DateTime utcNow, CancellationToken ct = default)
    {
        var today = AgePolicy.TodayInVietnam(utcNow);
        var block = AgeFeatures.Check(feature, user.Role, user.DateOfBirth, today, hasParentConsent: false);
        if (block is null || !block.NeedParentConsent)
            return block;
        return await parentLinks.HasGrantedConsentAsync(user.Id, ct) ? null : block;
    }
}
