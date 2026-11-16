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
}
