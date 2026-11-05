namespace StudyAbroad.Application.ParentLinks;

/// <summary>Nội dung trang xác nhận của phụ huynh (tra theo token trong link mời, không cần đăng nhập).</summary>
public record ParentInviteInfo(string StudentName, int? StudentAge, string ParentEmail, DateTime ExpiresAt, string PolicyVersion);

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
    DateTime? AcceptedAt,
    DateTime? LastInviteSentAt,
    DateTime? TokenExpiresAt,
    bool CanRevoke);

// Token đi trong body (không nằm trên URL của API) để không lọt vào log truy cập
public record InviteTokenRequest(string Token);
public record ChangeParentEmailRequest(string ParentEmail);

public enum ParentLinkError
{
    None,
    NotFound,         // không có / không thuộc người này / token sai hoặc đã dùng → 404
    Expired,          // lời mời quá hạn → 410
    Forbidden,        // sai vai trò, sai email phụ huynh, học sinh dưới 16 tự rút đồng ý → 403
    Validation,       // dữ liệu không hợp lệ → 400
    Conflict,         // request khác vừa sửa liên kết, hoặc email đã liên kết → 409
    TooManyRequests,  // gửi lại quá nhanh / quá nhiều trong ngày → 429
}

public record ParentLinkResult<T>(T? Value, ParentLinkError Error = ParentLinkError.None, string? Message = null)
{
    public static ParentLinkResult<T> Ok(T value, string? message = null) => new(value, ParentLinkError.None, message);
    public static ParentLinkResult<T> Fail(ParentLinkError error, string message) => new(default, error, message);
}
