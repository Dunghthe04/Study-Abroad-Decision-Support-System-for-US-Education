namespace StudyAbroad.Application.Billing.Family;

/// <summary>
/// Gói Gia đình: mời, hủy mời, chấp nhận / từ chối, danh sách thành viên, gỡ, rời (UC-126..131).
/// Mọi lỗi nghiệp vụ là <see cref="BillingRuleException"/> với mã trong <see cref="FamilyErrors"/>.
/// </summary>
public interface IFamilyService
{
    /// <summary>UC-129. 404 not_in_family. Lời mời và email chỉ có khi người xem là chủ gói.</summary>
    Task<FamilyDto> GetAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-126. Luôn êm lặng thành công dù email có tài khoản hay không (chống dò tài khoản).</summary>
    Task InviteAsync(Guid ownerId, string email, string seatRole, DateTime nowUtc, CancellationToken ct = default);

    Task ResendInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-127.</summary>
    Task CancelInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Không cần đăng nhập: thông tin lời mời cho trang chấp nhận.</summary>
    Task<FamilyInviteLookupDto> LookupAsync(string token, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-130.</summary>
    Task<FamilyDto> AcceptAsync(Guid userId, string role, string token, DateTime nowUtc, CancellationToken ct = default);

    Task DeclineAsync(Guid userId, string token, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-128. memberId là id dòng family_members của thành viên active.</summary>
    Task RemoveMemberAsync(Guid ownerId, Guid memberId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>UC-131.</summary>
    Task LeaveAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Cho job nền: lời mời pending quá hạn → expired. Trả số lời mời đã hết hạn.</summary>
    Task<int> ExpireInvitesAsync(DateTime nowUtc, CancellationToken ct = default);
}
