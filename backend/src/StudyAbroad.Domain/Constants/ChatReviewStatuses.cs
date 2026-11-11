namespace StudyAbroad.Domain.Constants;

/// <summary>Admin đã xử lý một lượt chat chưa (cột chat_messages.review_status). Chỉ dùng khi outcome khác answered; lượt trả lời được thì để null.</summary>
public static class ChatReviewStatuses
{
    /// <summary>Mới phát hiện, admin chưa xem.</summary>
    public const string Open = "open";
    /// <summary>Admin đã xử lý (bổ sung tài liệu, sửa nhãn thành ví dụ train...).</summary>
    public const string Resolved = "resolved";
    /// <summary>Admin đã xem và quyết định bỏ qua (ví dụ câu rác).</summary>
    public const string Ignored = "ignored";
}
