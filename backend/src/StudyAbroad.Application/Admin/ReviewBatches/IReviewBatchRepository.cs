using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ReviewBatches;

public interface IReviewBatchRepository
{
    /// <summary>
    /// Các câu trả lời có thể bốc: tin trợ lý đã trả lời được (outcome answered, không có trạng thái xử lý), có tin hỏi đi kèm
    /// (không có câu hỏi thì không chấm được nhãn ý), trong khoảng [fromUtc, toUtcExclusive); null là không giới hạn đầu đó.
    /// Chỉ mã và meta_json, KHÔNG đọc nội dung chat.
    /// </summary>
    Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default);

    /// <summary>Trong các mã đã cho, mã nào là tài khoản Admin đang hoạt động (chỉ những người này được giao duyệt).</summary>
    Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Lưu đợt, các câu, người được giao và dòng nhật ký trong CÙNG MỘT lần lưu: hoặc có đủ, hoặc không có gì.</summary>
    Task AddAsync(
        ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit,
        CancellationToken ct = default);
}
