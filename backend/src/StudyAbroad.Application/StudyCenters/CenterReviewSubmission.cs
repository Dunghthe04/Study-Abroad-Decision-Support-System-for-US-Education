using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-45] Nội dung đánh giá trung tâm: số sao 1 đến 5 và nhận xét tùy chọn (tối đa 2000 ký tự).</summary>
public record CreateCenterReviewRequest(int Rating, string? Comment = null);

/// <summary>[UC-45] Người dùng hiện tại có được đánh giá trung tâm này không. <paramref name="Reason"/> giải thích khi không được.</summary>
public record CenterReviewEligibilityDto(bool CanReview, bool HasReviewed, string? Reason);

/// <summary>[UC-45] Truy vấn và lưu đánh giá do học sinh / phụ huynh gửi.</summary>
public interface ICenterReviewSubmissionRepository
{
    /// <summary>Yêu cầu liên hệ gần nhất mà người này gửi tới trung tâm, hoặc yêu cầu của chính học sinh này (phụ huynh gửi thay).</summary>
    Task<Lead?> FindLatestLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default);

    /// <summary>Yêu cầu liên hệ gần nhất đã được trung tâm tiếp nhận (accepted/contacted/closed).</summary>
    Task<Lead?> FindLatestAcceptedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default);

    /// <summary>
    /// Yêu cầu liên hệ gần nhất đã được tiếp nhận của người này tới trung tâm mà CHƯA có đánh giá nào.
    /// Mỗi yêu cầu chỉ được một đánh giá, dù do học sinh hay phụ huynh gửi.
    /// </summary>
    Task<Lead?> FindLatestUnreviewedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default);

    /// <summary>
    /// Lưu đánh giá. Trả <c>false</c> nếu yêu cầu liên hệ này vừa được đánh giá bởi một lượt gửi khác
    /// (index duy nhất trên lead_id chặn ở DB khi hai lượt gửi trùng nhau).
    /// </summary>
    Task<bool> TryAddAsync(CenterReview review, CancellationToken ct = default);

    Task<CenterReviewDto?> GetPublicDtoAsync(Guid reviewId, CancellationToken ct = default);
}

public interface ICenterReviewService
{
    Task<CenterReviewEligibilityDto> GetEligibilityAsync(Guid userId, Guid centerId, CancellationToken ct = default);

    Task<CenterReviewDto> CreateAsync(Guid userId, Guid centerId, CreateCenterReviewRequest request, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2070 / UC-45] Học sinh / phụ huynh đã gửi yêu cầu liên hệ tới trung tâm thì được đánh giá 1 đến 5 sao.
/// Mỗi yêu cầu liên hệ chỉ được một đánh giá (đặc tả UC-45); đánh giá gắn dấu "đã xác thực" vì người đánh giá đã thật sự liên hệ trung tâm.
/// </summary>
public class CenterReviewService(
    ICenterReviewSubmissionRepository reviews,
    IStudyCenterRepository centers,
    IUserRepository users,
    IAuditLogRepository auditLogs) : ICenterReviewService
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 2000;
    public const string AlreadyReviewedMessage = "Yêu cầu liên hệ này đã được đánh giá.";

    public async Task<CenterReviewEligibilityDto> GetEligibilityAsync(Guid userId, Guid centerId, CancellationToken ct = default)
    {
        var (eligibility, _) = await EvaluateAsync(userId, centerId, ct);
        return eligibility;
    }

    public async Task<CenterReviewDto> CreateAsync(
        Guid userId,
        Guid centerId,
        CreateCenterReviewRequest request,
        CancellationToken ct = default)
    {
        if (request.Rating is < MinRating or > MaxRating)
            throw new ArgumentException($"Số sao phải từ {MinRating} đến {MaxRating}.");

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment is { Length: > MaxCommentLength })
            throw new ArgumentException($"Nhận xét tối đa {MaxCommentLength} ký tự.");

        var (eligibility, lead) = await EvaluateAsync(userId, centerId, ct);
        if (!eligibility.CanReview || lead is null)
            throw new ArgumentException(eligibility.Reason ?? "Bạn chưa thể đánh giá trung tâm này.");

        var review = new CenterReview
        {
            StudyCenterId = centerId,
            UserId = userId,
            LeadId = lead.Id,
            Rating = request.Rating,
            Comment = comment,
            IsVerified = true,
            Status = "visible",
        };
        if (!await reviews.TryAddAsync(review, ct))
            throw new ArgumentException(AlreadyReviewedMessage);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center_review.create",
            EntityType = "center_review",
            EntityId = review.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = centerId, leadId = lead.Id, rating = review.Rating, hasComment = comment is not null }),
        }, ct);

        return await reviews.GetPublicDtoAsync(review.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá vừa tạo.");
    }

    private async Task<(CenterReviewEligibilityDto Eligibility, Lead? Lead)> EvaluateAsync(Guid userId, Guid centerId, CancellationToken ct)
    {
        // Trung tâm chưa xác minh hoặc đã ngừng thì coi như không tồn tại, giống trang chi tiết công khai
        var center = await centers.GetByIdAsync(centerId, ct);
        if (center is null || !center.IsActive || center.VerificationStatus != CenterVerificationStatuses.Verified)
            throw new KeyNotFoundException("Trung tâm không tồn tại hoặc không nhận đánh giá.");

        var user = await users.GetUserById(userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        if (user.Role is not (UserRoles.Student or UserRoles.Parent))
            return (new CenterReviewEligibilityDto(false, false, "Chỉ học sinh hoặc phụ huynh được đánh giá trung tâm."), null);

        // Mỗi yêu cầu liên hệ một đánh giá: chỉ đánh giá khi yêu cầu liên hệ đã được trung tâm tiếp nhận (SC-41 / UC-45)
        var lead = await reviews.FindLatestUnreviewedLeadAsync(userId, centerId, ct);
        if (lead is not null)
            return (new CenterReviewEligibilityDto(true, false, null), lead);

        var acceptedLead = await reviews.FindLatestAcceptedLeadAsync(userId, centerId, ct);
        if (acceptedLead is not null)
            return (new CenterReviewEligibilityDto(false, true, AlreadyReviewedMessage), null);

        var anyLead = await reviews.FindLatestLeadAsync(userId, centerId, ct);
        if (anyLead is not null)
            return (new CenterReviewEligibilityDto(false, false, "Yêu cầu tư vấn của bạn cần được trung tâm tiếp nhận trước khi đánh giá."), null);

        return (new CenterReviewEligibilityDto(false, false, "Bạn cần gửi yêu cầu liên hệ tới trung tâm trước khi đánh giá."), null);
    }
}
