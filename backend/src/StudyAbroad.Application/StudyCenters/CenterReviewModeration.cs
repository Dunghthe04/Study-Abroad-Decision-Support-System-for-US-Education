using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-57] Lý do gỡ đánh giá (không bắt buộc, tối đa 500 ký tự), lưu vào nhật ký.</summary>
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
}

/// <summary>
/// [USAS-2079 / UC-57] Moderator gỡ đánh giá trung tâm vi phạm quy tắc. Đánh giá chuyển sang <c>removed</c> nên biến khỏi danh sách công khai
/// và không còn tính vào điểm trung bình (điểm tính trực tiếp từ các đánh giá đang hiển thị). Nội dung gốc vẫn được giữ trong DB.
/// Gỡ lại đánh giá đã gỡ không làm gì thêm và không ghi nhật ký lần hai. Quyền Moderator do controller kiểm tra.
/// </summary>
public class CenterReviewModerationService(
    ICenterReviewModerationRepository reviews,
    IAuditLogRepository auditLogs) : ICenterReviewModerationService
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

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > MaxReasonLength })
            throw new ArgumentException($"Lý do gỡ tối đa {MaxReasonLength} ký tự.");

        var review = await reviews.GetForUpdateAsync(centerId, reviewId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá của trung tâm này.");

        if (!string.Equals(review.Status, RemovedStatus, StringComparison.OrdinalIgnoreCase))
        {
            var previousStatus = review.Status;
            review.Status = RemovedStatus;
            review.UpdatedAt = DateTime.UtcNow;
            await reviews.SaveAsync(review, ct);

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
}
