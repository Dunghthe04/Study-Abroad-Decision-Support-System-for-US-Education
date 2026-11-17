namespace StudyAbroad.Domain.Constants;

public static class ParentLinkStatuses
{
    /// <summary>Đã gửi lời mời, phụ huynh chưa đồng ý.</summary>
    public const string Pending = "pending";
    /// <summary>Phụ huynh đã đồng ý, liên kết có hiệu lực.</summary>
    public const string Active = "active";
    /// <summary>Đã rút đồng ý / hủy liên kết / hủy lời mời. Giữ dòng để làm bằng chứng.</summary>
    public const string Revoked = "revoked";
    /// <summary>Lời mời quá hạn chưa được chấp nhận. Đặt lúc có người mở link hoặc học sinh xem thẻ liên kết (không cần job).</summary>
    public const string Expired = "expired";
    /// <summary>[UC-115] Phụ huynh bấm Từ chối trong link mời. Học sinh gửi lại hoặc đổi email để về chờ xác nhận.</summary>
    public const string Rejected = "rejected";
}
