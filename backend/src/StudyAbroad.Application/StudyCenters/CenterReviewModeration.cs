using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-57] Lý do gỡ đánh giá (bắt buộc, tối đa 500 ký tự), lưu vào nhật ký.</summary>
public record RemoveCenterReviewRequest(string? Reason = null);

/// <summary>[UC-57] Kết quả gỡ: đánh giá và trạng thái mới (<c>removed</c>).</summary>
public record CenterReviewRemovalDto(Guid ReviewId, Guid StudyCenterId, string Status);

/// <summary>[UC-57] Lấy và lưu đánh giá để Moderator gỡ.</summary>
public interface ICenterReviewModerationRepository
{
    /// <summary>Đánh giá theo đúng trung tâm (đánh giá của trung tâm khác coi như không có), mọi trạng thái.</summary>
    Task<CenterReview?> GetForUpdateAsync(Guid centerId, Guid reviewId, CancellationToken ct = default);

    Task SaveAsync(CenterReview review, CancellationToken ct = default);
}

public interface ICenterReviewModerationService
{
    Task<CenterReviewRemovalDto> RemoveAsync(Guid moderatorUserId, Guid centerId, Guid reviewId, string? reason, CancellationToken ct = default);
    /// <summary>[UC-53 2b / UC-152] Moderator gỡ phản hồi vi phạm của trung tâm (đánh giá gốc vẫn hiển thị).</summary>
    Task<CenterReviewRemovalDto> RemoveReplyAsync(Guid moderatorUserId, Guid centerId, Guid reviewId, string? reason, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

/// <summary>
/// [USAS-2079 / UC-57] Moderator gỡ đánh giá trung tâm vi phạm quy tắc. Đánh giá chuyển sang <c>removed</c> nên biến khỏi danh sách công khai
/// và không còn tính vào điểm trung bình (điểm tính trực tiếp từ các đánh giá đang hiển thị). Nội dung gốc vẫn được giữ trong DB.
/// Gỡ lại đánh giá đã gỡ không làm gì thêm và không ghi nhật ký lần hai. Quyền Moderator do controller kiểm tra.
/// </summary>
public class CenterReviewModerationService(
    ICenterReviewModerationRepository reviews,
    IAuditLogRepository auditLogs,
    ICenterReviewReportRepository? reviewReports = null) : ICenterReviewModerationService
{
    public const int MaxReasonLength = 500;
    public const string RemovedStatus = "removed";

    public async Task<CenterReviewRemovalDto> RemoveAsync(
        Guid moderatorUserId,
        Guid centerId,
        Guid reviewId,
        string? reason,
        CancellationToken ct = default)
    {
        if (centerId == Guid.Empty || reviewId == Guid.Empty)
            throw new ArgumentException("Mã trung tâm hoặc mã đánh giá không hợp lệ.");

        // Jira UC-57: gỡ đánh giá phải kèm lý do
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
            throw new ArgumentException("Vui lòng nhập lý do gỡ đánh giá.");
        if (trimmedReason.Length > MaxReasonLength)
            throw new ArgumentException($"Lý do gỡ tối đa {MaxReasonLength} ký tự.");

        var review = await reviews.GetForUpdateAsync(centerId, reviewId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá của trung tâm này.");

        if (!string.Equals(review.Status, RemovedStatus, StringComparison.OrdinalIgnoreCase))
        {
            var previousStatus = review.Status;
            var now = DateTime.UtcNow;
            review.Status = RemovedStatus;
            review.UpdatedAt = now;
            await reviews.SaveAsync(review, ct);

            // [UC-151 / UC-53] Đóng mọi báo cáo đang mở của đánh giá này
            if (reviewReports is not null)
                await reviewReports.ResolveOpenReportsForRemovedReviewAsync(review.Id, moderatorUserId, trimmedReason, now, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = moderatorUserId,
                Action = "center_review.remove",
                EntityType = "center_review",
                EntityId = review.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    studyCenterId = centerId,
                    reviewerUserId = review.UserId,
                    rating = review.Rating,
                    hadComment = review.Comment is not null,
                    previousStatus,
                    reason = trimmedReason,
                }),
            }, ct);
        }

        return new CenterReviewRemovalDto(review.Id, review.StudyCenterId, review.Status);
    }

    public async Task<CenterReviewRemovalDto> RemoveReplyAsync(
        Guid moderatorUserId,
        Guid centerId,
        Guid reviewId,
        string? reason,
        CancellationToken ct = default)
    {
        if (centerId == Guid.Empty || reviewId == Guid.Empty)
            throw new ArgumentException("Mã trung tâm hoặc mã đánh giá không hợp lệ.");

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
            throw new ArgumentException("Vui lòng nhập lý do gỡ phản hồi.");
        if (trimmedReason.Length > MaxReasonLength)
            throw new ArgumentException($"Lý do gỡ tối đa {MaxReasonLength} ký tự.");

        var review = await reviews.GetForUpdateAsync(centerId, reviewId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá của trung tâm này.");

        if (!review.CenterReplyCreatedAt.HasValue)
            throw new KeyNotFoundException("Đánh giá này chưa có phản hồi của trung tâm.");

        if (!string.Equals(review.CenterReplyStatus, RemovedStatus, StringComparison.OrdinalIgnoreCase))
        {
            review.CenterReplyStatus = RemovedStatus;
            review.CenterReplyRemovedByUserId = moderatorUserId;
            review.CenterReplyRemoveReason = trimmedReason;
            review.UpdatedAt = DateTime.UtcNow;
            await reviews.SaveAsync(review, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = moderatorUserId,
                Action = "center_review.reply_remove",
                EntityType = "center_review",
                EntityId = review.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    studyCenterId = centerId,
                    replyAuthorUserId = review.CenterReplyAuthorUserId,
                    reason = trimmedReason,
                }),
            }, ct);
        }

        return new CenterReviewRemovalDto(review.Id, review.StudyCenterId, review.CenterReplyStatus ?? RemovedStatus);
    }
}
