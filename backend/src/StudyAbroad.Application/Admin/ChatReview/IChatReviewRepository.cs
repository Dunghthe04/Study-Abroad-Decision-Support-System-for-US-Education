namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>Đọc hàng đợi câu cần xem. Khoảng thời gian là [fromUtc, toUtcExclusive); null nghĩa là không giới hạn đầu đó.</summary>
public interface IChatReviewRepository
{
    /// <summary>Một trang hàng đợi (mới nhất trước) và tổng số câu thỏa bộ lọc. status, outcome null là không lọc.</summary>
    Task<(IReadOnlyList<ChatReviewRow> Rows, int Total)> QueuePageAsync(
        string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive, int skip, int take, CancellationToken ct = default);

    /// <summary>Số câu theo (trạng thái xử lý, kết quả) trong khoảng.</summary>
    Task<IReadOnlyList<ChatReviewCountRow>> CountsAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default);
}
