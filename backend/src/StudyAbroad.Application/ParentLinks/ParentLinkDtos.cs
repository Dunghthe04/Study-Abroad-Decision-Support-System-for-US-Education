namespace StudyAbroad.Application.ParentLinks;

/// <summary>[UC-117] Thông tin hiện trên trang rút đồng ý qua link trong mail.</summary>
public record ParentRevokeInfo(string StudentName, DateTime? AcceptedAt);

/// <summary>[UC-75 4b] Hai nút trong mail xác nhận người đại diện yêu cầu xóa tài khoản của con.</summary>
public static class ParentRestoreModes
{
    /// <summary>"Hủy xóa và đồng ý lại cho con": khôi phục và ghi đồng ý mới.</summary>
    public const string Reconsent = "reconsent";
    /// <summary>"Chỉ hủy xóa": khôi phục, đồng ý vẫn đã rút.</summary>
    public const string RestoreOnly = "restore_only";
}

/// <summary>
/// [UC-75 4b] Nội dung trang khôi phục mở từ nút trong mail. <see cref="CanReconsent"/> = false khi con đã đủ 16 tuổi
/// (tự quyết định việc đồng ý, người đại diện chỉ hủy xóa được).
/// </summary>
public record ParentRestoreInfo(string StudentName, DateTime DeletionDate, string Mode, bool CanReconsent, string PolicyVersion);

/// <summary>[UC-75 4b] Người đại diện xác nhận khôi phục; <see cref="IsGuardian"/> và <see cref="PolicyVersion"/> chỉ cần khi đồng ý lại.</summary>
public record ParentRestoreRequest(string Token, bool IsGuardian = false, string? PolicyVersion = null);

/// <summary>[UC-115] Trạng thái link mời nhìn từ trang của phụ huynh.</summary>
public static class ParentInviteStates
{
    /// <summary>Chưa trả lời: hiện ô tick và 2 nút Đồng ý / Từ chối.</summary>
    public const string Pending = "pending";
    /// <summary>Link đã dùng để đồng ý: chỉ hiện kết quả đã ghi nhận.</summary>
    public const string Accepted = "accepted";
    /// <summary>Link đã dùng để từ chối: chỉ hiện kết quả đã ghi nhận.</summary>
    public const string Rejected = "rejected";
}

/// <summary>
/// [UC-115] Nội dung trang phụ huynh mở từ link mời, không cần đăng nhập.
/// <see cref="ExpiresAt"/> chỉ có khi còn chờ; <see cref="DecidedAt"/> chỉ có khi đã trả lời.
/// </summary>
public record ParentInviteInfo(
    string State,
    string StudentName,
    int? StudentAge,
    string ParentEmail,
    DateTime? ExpiresAt,
    DateTime? DecidedAt,
    string PolicyVersion,
    // Email nhận lời mời đã là tài khoản học sinh / trung tâm / quản trị: không đồng ý được, chỉ từ chối.
    // Chỉ báo ở trang này (người có link trong hộp thư), không báo lúc học sinh khai email để không lộ email nào có tài khoản.
    string? BlockedReason = null,
    // Liên kết đã gắn tài khoản phụ huynh: trang đã đồng ý mời đăng nhập thay vì đăng ký.
    bool HasParentAccount = false);

/// <summary>
/// Một liên kết nhìn từ phía người đang đăng nhập: học sinh thấy lời mời đang chờ và phụ huynh đã liên kết,
/// phụ huynh thấy các con đã liên kết. <see cref="CanRevoke"/> do server tính theo luật rút đồng ý.
/// </summary>
public record ParentLinkDto(
    Guid Id,
    string Status,
    string ParentEmail,
    string? ParentName,
    string StudentName,
    int? StudentAge,
    // [UC-42] Phụ huynh chọn con này khi gửi yêu cầu liên hệ trung tâm thay con
    Guid StudentUserId,
    DateTime? AcceptedAt,
    DateTime? LastInviteSentAt,
    DateTime? TokenExpiresAt,
    bool CanRevoke);

// Token đi trong body (không nằm trên URL của API) để không lọt vào log truy cập
public record InviteTokenRequest(string Token);
/// <summary>
/// [UC-115] Phụ huynh đồng ý qua link mời: phải tick "Tôi là cha, mẹ hoặc người giám hộ hợp pháp".
/// PolicyVersion: phiên bản văn bản phụ huynh đã đọc trên trang; nếu khác phiên bản đang hiệu lực (admin vừa công bố bản mới) thì không ghi
/// đồng ý để phụ huynh đọc lại. Bỏ trống thì ghi theo phiên bản đang hiệu lực.
/// </summary>
public record AcceptInviteRequest(string Token, bool IsGuardian, string? PolicyVersion = null);
public record ChangeParentEmailRequest(string ParentEmail);

public enum ParentLinkError
{
    None,
    NotFound,         // không có / không thuộc người này / token sai hoặc đã dùng → 404
    Expired,          // lời mời quá hạn → 410
    Forbidden,        // sai vai trò, học sinh dưới 16 tự rút đồng ý, link thu hồi khi con đã 16 → 403
    Validation,       // dữ liệu không hợp lệ → 400
    Conflict,         // request khác vừa sửa liên kết, email đã liên kết, hoặc link mời đã được trả lời → 409
    TooManyRequests,  // gửi lại quá nhanh / quá nhiều trong ngày → 429
}

public record ParentLinkResult<T>(T? Value, ParentLinkError Error = ParentLinkError.None, string? Message = null)
{
    public static ParentLinkResult<T> Ok(T value, string? message = null) => new(value, ParentLinkError.None, message);
    public static ParentLinkResult<T> Fail(ParentLinkError error, string message) => new(default, error, message);
}
