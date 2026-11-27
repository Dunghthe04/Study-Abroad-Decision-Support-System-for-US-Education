using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Mail chờ gửi (hàng đợi bền, thay hàng đợi trong RAM). Người nhận, tiêu đề, nội dung nằm hết trong
/// <see cref="Payload"/> đã mã hóa AES-GCM; các cột rõ không cho biết mail gửi ai, loại gì.
/// Gửi xong hoặc bỏ thì xóa hẳn dòng, không giữ lịch sử.
/// Bảng riêng vì Notification bắt buộc UserId (mail hay gửi người chưa có tài khoản) và lưu chữ rõ,
/// còn AuditLog chỉ ghi thêm, không có trạng thái gửi/thử lại.
/// </summary>
public class EmailOutbox : BaseEntity
{
    /// <summary>base64(nonce 12 byte + bản mã + tag 16 byte) của người nhận, tiêu đề, nội dung.</summary>
    public string Payload { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256(khóa mail, email + loại mail), chỉ có ở loại mail mới làm mail cũ hết giá trị (OTP, lời mời).</summary>
    public string? DedupeKey { get; set; }
    /// <summary>pending | sending</summary>
    public string Status { get; set; } = EmailOutboxStatuses.Pending;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    /// <summary>Dòng sending quá giờ này mà còn thì coi như job trước đã chết, được lấy lại.</summary>
    public DateTime? LockedUntil { get; set; }
    /// <summary>Mail có hạn (OTP): quá hạn thì bỏ, không gửi.</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Chỉ tên loại lỗi và mã SMTP, không có thông điệp lỗi (có thể lẫn email).</summary>
    public string? LastError { get; set; }
}

public static class EmailOutboxStatuses
{
    public const string Pending = "pending";
    public const string Sending = "sending";
}
