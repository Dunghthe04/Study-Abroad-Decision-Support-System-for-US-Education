using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>Đọc hàng đợi câu cần xem. Khoảng thời gian là [fromUtc, toUtcExclusive); null nghĩa là không giới hạn đầu đó.</summary>
public interface IChatReviewRepository
{
    /// <summary>Một trang hàng đợi (mới nhất trước) và tổng số câu thỏa bộ lọc. status, outcome null là không lọc.</summary>
    Task<(IReadOnlyList<ChatReviewRow> Rows, int Total)> QueuePageAsync(
        string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Chi tiết một câu (tin trả lời, tin hỏi, vài tin ngữ cảnh) kèm NỘI DUNG chat. Null nếu mã không phải một tin của hàng đợi.
    /// Bên gọi phải ghi nhật ký người xem trước khi trả nội dung này cho ai.
    /// </summary>
    Task<ChatReviewDetailRows?> FindDetailAsync(Guid messageId, int contextCount, CancellationToken ct = default);

    /// <summary>Số câu theo (trạng thái xử lý, kết quả) trong khoảng.</summary>
    Task<IReadOnlyList<ChatReviewCountRow>> CountsAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái xử lý (review_status) của một tin thuộc hàng đợi và trả trạng thái TRƯỚC đó; null nếu mã không phải tin của hàng đợi.
    /// Nếu trạng thái mới trùng trạng thái cũ thì không đổi gì, không ghi gì. Ngược lại việc đổi và dòng nhật ký (audit, dựng từ trạng thái cũ)
    /// được lưu trong CÙNG MỘT lần lưu: không có dấu vết thì không có thay đổi.
    /// </summary>
    Task<string?> SetStatusAsync(Guid messageId, string status, Func<string, AuditLog> audit, CancellationToken ct = default);
}
