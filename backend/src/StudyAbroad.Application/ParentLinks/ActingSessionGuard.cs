using System.Security.Claims;
using System.Text.Json;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>1.5b: kiểm tra phiên phụ huynh làm thay con managed.
/// Con managed không có mật khẩu nên chỉ vào được qua phụ huynh: token của con phải mang claim <c>act</c> (RFC 8693)
/// chỉ tới phụ huynh còn quyền. Kiểm tra lại ở mỗi request (OnTokenValidated) và mỗi lần gia hạn, nên thu hồi liên kết,
/// khóa phụ huynh hoặc phụ huynh đăng xuất hết thì phiên làm thay mất tác dụng ngay.</summary>
public class ActingSessionGuard(IUserRepository users, IParentLinkRepository links)
{
    /// <summary>Tên claim theo RFC 8693. .NET có thể đổi tên khi đọc token nên đọc cả <see cref="ClaimTypes.Actor"/>.</summary>
    public const string ActorClaim = "act";

    /// <summary>Token có hợp lệ về mặt làm thay không:
    /// tài khoản thường không được mang <c>act</c>; con managed bắt buộc mang <c>act</c> của phụ huynh còn quyền.</summary>
    public async Task<bool> IsValidAsync(User user, ClaimsPrincipal? principal, CancellationToken ct)
    {
        if (user.Status != UserStatuses.Managed)
            return !HasActorClaim(principal);

        return ReadActingParentId(principal) is { } parentId && await CanActAsync(parentId, user, ct);
    }

    /// <summary>Phụ huynh này có được làm thay con này không. Trả con khi được, null khi không (con thật, con của người khác,
    /// liên kết đã thu hồi, phụ huynh bị khóa / xóa / không còn phiên nào).</summary>
    public async Task<User?> FindChildToActAsAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var child = await users.GetUserById(childId, ct);
        return child is not null && await CanActAsync(parentId, child, ct) ? child : null;
    }

    /// <summary>Phụ huynh đang được làm thay con managed này (qua liên kết active). Dùng khi gia hạn phiên của con.</summary>
    public async Task<Guid?> FindActingParentAsync(User child, CancellationToken ct)
    {
        if (child.Status != UserStatuses.Managed)
            return null;

        foreach (var link in await links.GetByStudentAsync(child.Id, ct))
        {
            if (link.Status == ParentLinkStatuses.Active && link.ParentUserId is { } parentId && await CanActAsync(parentId, child, ct))
                return parentId;
        }
        return null;
    }

    /// <summary>Đọc id phụ huynh trong claim <c>act</c> dạng <c>{"sub":"..."}</c>. Không có hoặc sai dạng thì null.</summary>
    public static Guid? ReadActingParentId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ActorClaim)?.Value ?? principal?.FindFirst(ClaimTypes.Actor)?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("sub", out var sub)
                && Guid.TryParse(sub.GetString(), out var id)
                ? id
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Có claim <c>act</c> (kể cả sai dạng). Sai dạng vẫn tính là có để tài khoản thường mang nó bị từ chối.</summary>
    public static bool HasActorClaim(ClaimsPrincipal? principal) =>
        principal?.FindFirst(ActorClaim) is not null || principal?.FindFirst(ClaimTypes.Actor) is not null;

    // Đủ các điều kiện: con Status managed, phụ huynh tồn tại, đúng vai trò, không bị khóa / xóa,
    // liên kết active của đúng phụ huynh đó, và phụ huynh còn ít nhất một phiên đăng nhập.
    private async Task<bool> CanActAsync(Guid parentId, User child, CancellationToken ct)
    {
        if (child.Status != UserStatuses.Managed)
            return false;

        var parent = await users.GetUserById(parentId, ct);
        if (parent is null || parent.Role != UserRoles.Parent
            || parent.Status == UserStatuses.Locked || parent.Status == UserStatuses.Deleted)
            return false;

        var linked = (await links.GetActiveByParentAsync(parentId, ct)).Any(l => l.StudentUserId == child.Id);
        return linked && await users.HasActiveSessionAsync(parentId, ct);
    }
}
