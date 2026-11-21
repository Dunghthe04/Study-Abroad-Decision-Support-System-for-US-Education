namespace StudyAbroad.Application.Billing.Family;

// Phản hồi gói Gia đình cho khách hàng: chỉ phần trăm, không bao giờ có số token (D4). JSON camelCase khớp frontend (types/billing.ts).

/// <summary>Kỳ Gia đình đang chạy: tên gói, ngày hết hạn, phần trăm đã dùng của cả nhóm.</summary>
public sealed record FamilyCycleDto(string PlanName, DateTime? EndsAt, int UsedPercent, int? QuotaPeriodDays = null, DateTimeOffset? QuotaResetsAt = null);

public sealed record FamilySeatCountDto(int Used, int Max);

/// <summary>Chỗ ngồi thêm (chủ gói không chiếm chỗ): đang dùng = thành viên active + lời mời đang chờ.</summary>
public sealed record FamilySeatsDto(FamilySeatCountDto Students, FamilySeatCountDto Parents);

/// <summary>
/// Một dòng thành viên. Chủ gói: MemberId = id người dùng, Role = "owner". Thành viên: MemberId = id dòng family_members, Role = loại chỗ.
/// Chỉ có tên và vai trò, không có email (riêng tư).
/// </summary>
public sealed record FamilyMemberDto(Guid MemberId, string Name, string Role, int SharePercent, DateTime? JoinedAt, bool IsMe);

/// <summary>Lời mời đang chờ, chỉ chủ gói thấy. CanResendAt null = gửi lại được ngay.</summary>
public sealed record FamilyInviteDto(Guid InviteId, string Email, string SeatRole, DateTime ExpiresAt, DateTime? CanResendAt);

/// <summary>GET /api/v1/family. Invites luôn rỗng với thành viên (chỉ chủ gói thấy lời mời).</summary>
public sealed record FamilyDto(
    Guid GroupId, bool IsOwner, string OwnerName, FamilyCycleDto? Cycle, FamilySeatsDto Seats,
    IReadOnlyList<FamilyMemberDto> Members, IReadOnlyList<FamilyInviteDto> Invites);

/// <summary>Trang chấp nhận lời mời (không cần đăng nhập). Status: pending | accepted | declined | cancelled | expired.</summary>
public sealed record FamilyInviteLookupDto(string OwnerName, string SeatRole, DateTime ExpiresAt, string Status);

public static class FamilyInviteStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}

/// <summary>Mọi lỗi nghiệp vụ gói Gia đình ở một chỗ: mã lỗi khớp frontend (family-api.ts), thông báo tiếng Việt ngắn.</summary>
public static class FamilyErrors
{
    public static BillingRuleException NotInFamily() => new("not_in_family", 404, "Bạn không ở trong gói Gia đình nào.");
    public static BillingRuleException NoFamily() => new("no_family", 404, "Bạn chưa có gói Gia đình đang dùng.");
    public static BillingRuleException NotOwner() => new("not_owner", 403, "Chỉ chủ gói mới làm được thao tác này.");
    public static BillingRuleException InvalidSeatRole() => new("invalid_value", 400, "Loại chỗ phải là học sinh hoặc phụ huynh.");
    public static BillingRuleException InvalidEmail() => new("invalid_email", 400, "Email không hợp lệ hoặc là email của chính bạn.");
    public static BillingRuleException InviteExists() => new("invite_exists", 409, "Email này đã có lời mời đang chờ hoặc đã là thành viên. Hãy dùng nút Gửi lại.");
    public static BillingRuleException SeatFull() => new("seat_full", 409, "Loại chỗ này đã đủ người (tính cả lời mời đang chờ).");
    public static BillingRuleException NotPending() => new("not_pending", 409, "Lời mời này không còn ở trạng thái chờ.");
    public static BillingRuleException ResendTooSoon() => new("resend_too_soon", 429, "Bạn vừa gửi lại lời mời. Đợi một lúc rồi thử lại nhé.");
    public static BillingRuleException InviteInvalid() => new("invite_invalid", 404, "Link lời mời không hợp lệ hoặc đã bị hủy.");
    public static BillingRuleException InviteExpired() => new("invite_expired", 410, "Lời mời đã hết hạn. Hãy nhờ chủ gói gửi lại lời mời.");
    public static BillingRuleException WrongAccount() => new("wrong_account", 403, "Bạn đang đăng nhập bằng tài khoản khác với email được mời.");
    public static BillingRuleException RoleMismatch() => new("role_mismatch", 409, "Loại tài khoản của bạn không khớp với chỗ được mời.");
    public static BillingRuleException AlreadyInFamily() => new("already_in_family", 409, "Bạn đang ở trong một gói Gia đình khác còn hạn.");
    public static BillingRuleException PendingFamilyOrder() => new("pending_family_order", 409, "Bạn có đơn mua gói Gia đình chưa thanh toán. Hãy hủy đơn hoặc đợi đơn hết hạn.");
    public static BillingRuleException CannotRemoveOwner() => new("cannot_remove_owner", 409, "Không thể xóa chủ gói khỏi gói.");
    public static BillingRuleException MemberNotFound() => new("member_not_found", 404, "Không tìm thấy thành viên này trong gói.");
    public static BillingRuleException OwnerCannotLeave() => new("owner_cannot_leave", 409, "Chủ gói không thể rời gói của mình.");
}
