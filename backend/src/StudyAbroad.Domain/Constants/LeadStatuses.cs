namespace StudyAbroad.Domain.Constants;

/// <summary>Trạng thái yêu cầu liên hệ (leads.status).</summary>
public static class LeadStatuses
{
    /// <summary>Mới gửi, trung tâm chưa trả lời.</summary>
    public const string New = "new";
    /// <summary>[UC-73] Trung tâm đã chấp nhận yêu cầu, mở nhắn tin (UC-74).</summary>
    public const string Accepted = "accepted";
    /// <summary>[UC-73] Trung tâm từ chối yêu cầu; người gửi nhận thông báo.</summary>
    public const string Rejected = "rejected";
    /// <summary>Trung tâm đã trả lời, đang trao đổi (trạng thái cũ, coi như đã chấp nhận).</summary>
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
    /// <summary>Quá 7 ngày phụ huynh chưa trả lời hoặc quá 7 ngày trung tâm chưa phản hồi.</summary>
    public const string Expired = "expired";
    /// <summary>[UC-57] Tài khoản trung tâm bị khóa bởi quản trị viên; các yêu cầu đang mở tạm dừng.</summary>
    public const string CenterInactive = "center_inactive";

    /// <summary>[UC-116] Lead trung tâm chưa nhận (chờ phụ huynh / phụ huynh từ chối / hết hạn): ẩn khỏi mọi màn hình phía trung tâm.</summary>
    public static bool IsHiddenFromCenter(string? status) =>
        status is PendingParent or Cancelled or Expired;

    /// <summary>[UC-73] Đã chấp nhận (gồm contacted cũ): điều kiện để nhắn tin ở UC-74.</summary>
    public static bool IsAccepted(string? status) =>
        string.Equals(status, Accepted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Contacted, StringComparison.OrdinalIgnoreCase);
}
