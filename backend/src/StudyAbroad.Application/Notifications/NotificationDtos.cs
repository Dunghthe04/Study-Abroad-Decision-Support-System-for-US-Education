using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Notifications;

/// <summary>[UC-110] Một thông báo của chính người dùng đăng nhập.</summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? LinkUrl,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

/// <summary>[UC-110] Số thông báo chưa đọc (hiện trên chuông ở thanh đầu trang).</summary>
public record UnreadCountDto(int Count);

/// <summary>[UC-110] Các loại thông báo (cột Type tối đa 32 ký tự).</summary>
public static class NotificationTypes
{
    public const string LeadNew = "lead_new";                       // UC-42: trung tâm có yêu cầu liên hệ mới
    public const string LeadStatusChanged = "lead_status_changed";  // UC-73: trung tâm đổi trạng thái yêu cầu
    public const string ParentLinkLinked = "parent_link_linked";    // UC-75: phụ huynh xác nhận liên kết
    public const string ParentLinkRevoked = "parent_link_revoked";  // UC-75: liên kết bị hủy
    public const string VerificationResult = "verification_result"; // UC-59: kết quả duyệt hồ sơ xác minh
    public const string LeadMessageReport = "lead_message_report";  // UC-58: tin nhắn bị báo cáo (gửi Admin)
}

public static class NotificationMapper
{
    public static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type, n.Title, n.Body, n.LinkUrl, n.ReadAt.HasValue, n.ReadAt, n.CreatedAt);
}
