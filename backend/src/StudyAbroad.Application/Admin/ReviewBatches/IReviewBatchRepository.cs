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

    /// <summary>Các đợt mà người dùng đã tạo hoặc được giao duyệt, mới nhất trước, kèm số câu và tiến độ từng người duyệt. Không có nội dung chat.</summary>
    Task<IReadOnlyList<ReviewBatchListRow>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Các câu của một đợt cho người duyệt (theo thứ tự đọc). Null nếu đợt không có HOẶC người này không được giao duyệt đợt đó:
    /// hai trường hợp không phân biệt để không lộ đợt nào tồn tại. Chưa có nội dung chat.
    /// </summary>
    Task<ReviewItemsRows?> ListItemsAsync(Guid batchId, Guid reviewerUserId, CancellationToken ct = default);

    /// <summary>
    /// Một câu của đợt CÓ NỘI DUNG chat (câu hỏi, câu trả lời, vài tin trước) kèm kết luận của CHÍNH người này (nếu có). Null nếu người này không được
    /// giao đợt, câu không thuộc đợt, hoặc tin gốc đã bị xóa. Bên gọi phải ghi nhật ký người xem trước khi trả nội dung cho ai.
    /// </summary>
    Task<ReviewItemDetailRows?> FindItemDetailAsync(Guid batchId, Guid itemId, Guid reviewerUserId, int contextCount, CancellationToken ct = default);

    /// <summary>
    /// Lưu (thêm hoặc ghi đè) kết luận của người duyệt cho một câu. Trả kết luận đã lưu; null nếu người này không được giao đợt
    /// hoặc câu không thuộc đợt.
    /// </summary>
    Task<ReviewVerdict?> UpsertVerdictAsync(
        Guid batchId, Guid itemId, Guid reviewerUserId, string labelVerdict, string? correctIntent, string answerVerdict, string? cause,
        CancellationToken ct = default);

    /// <summary>
    /// Mọi kết luận của một đợt (của tất cả người duyệt) kèm số liệu nhóm lúc bốc, để tính báo cáo. Null nếu đợt không có HOẶC người gọi
    /// không phải người tạo cũng không được giao duyệt đợt đó (không phân biệt hai trường hợp). Không đọc nội dung chat.
    /// </summary>
    Task<ReviewReportRows?> GetReportRowsAsync(Guid batchId, Guid userId, CancellationToken ct = default);
}
