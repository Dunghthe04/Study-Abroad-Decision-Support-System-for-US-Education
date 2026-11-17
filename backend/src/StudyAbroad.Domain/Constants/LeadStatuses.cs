namespace StudyAbroad.Domain.Constants;

/// <summary>Trạng thái yêu cầu liên hệ (leads.status).</summary>
public static class LeadStatuses
{
    /// <summary>Mới gửi, trung tâm chưa trả lời.</summary>
    public const string New = "new";
    /// <summary>Trung tâm đã trả lời, đang trao đổi.</summary>
    public const string Contacted = "contacted";
    /// <summary>Đã hoàn tất.</summary>
    public const string Closed = "closed";
    /// <summary>UC-80: người gửi xóa tài khoản. Trung tâm chỉ còn thấy dòng "Đã thu hồi", không thấy dữ liệu, không trao đổi tiếp.</summary>
    public const string Withdrawn = "withdrawn";

    // [UC-116] Học sinh dưới 16 gửi: phụ huynh xác nhận qua link mail trước khi trung tâm nhận. Trung tâm không thấy 3 trạng thái này.
    /// <summary>Chờ phụ huynh bấm Đồng ý / Từ chối trong mail (7 ngày).</summary>
    public const string PendingParent = "pending_parent";
    /// <summary>Phụ huynh từ chối, hoặc học sinh xóa tài khoản khi còn chờ.</summary>
    public const string Cancelled = "cancelled";
    /// <summary>Quá 7 ngày phụ huynh chưa trả lời.</summary>
    public const string Expired = "expired";
}
