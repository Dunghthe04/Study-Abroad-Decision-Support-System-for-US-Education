using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

// ===== [UC-151] Báo cáo đánh giá trung tâm =====

public record CreateCenterReviewReportRequest(string Reason, string? Detail = null);

public record DismissCenterReviewReportRequest(string? Note = null);

public record CenterReviewReportDto(
    Guid Id,
    Guid CenterReviewId,
    Guid StudyCenterId,
    string StudyCenterName,
    int ReviewRating,
    string? ReviewComment,
    string ReviewStatus,
    string ReviewerName,
    Guid ReporterUserId,
    string ReporterName,
    string ReporterRole,
    bool IsCenterSelfReport,
    string Reason,
    string? Detail,
    string Status,
    DateTime CreatedAt,
    Guid? ResolvedByUserId = null,
    DateTime? ResolvedAt = null,
    string? ResolutionNote = null);

public interface ICenterReviewReportRepository
{
    Task<CenterReview?> GetVisibleReviewAsync(Guid centerId, Guid reviewId, CancellationToken ct = default);
    Task<bool> HasOpenReportAsync(Guid reviewId, Guid reporterUserId, CancellationToken ct = default);
    Task<bool> TryAddAsync(CenterReviewReport report, CancellationToken ct = default);
    Task<PagedResult<CenterReviewReportDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<CenterReviewReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default);
    Task UpdateAsync(CenterReviewReport report, CancellationToken ct = default);
    Task ResolveOpenReportsForRemovedReviewAsync(Guid reviewId, Guid moderatorUserId, string? reason, DateTime now, CancellationToken ct = default);
}

public interface ICenterReviewReportService
{
    Task<CenterReviewReportDto> ReportAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid centerId,
        Guid reviewId,
        CreateCenterReviewReportRequest request,
        CancellationToken ct = default);

    Task<PagedResult<CenterReviewReportDto>> SearchForModeratorAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    Task DismissAsync(Guid moderatorUserId, Guid reportId, string? note, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2184 / UC-151] Học sinh, Phụ huynh hoặc Trung tâm (chỉ cho đánh giá về chính trung tâm mình) báo cáo đánh giá vi phạm.
/// Moderator xử lý qua hàng đợi báo cáo (UC-53).
/// </summary>
public class CenterReviewReportService(
    ICenterReviewReportRepository reports,
    IStudyCenterRepository centers,
    IUserRepository users,
    IAuditLogRepository auditLogs,
    IParentLinkRepository? parentLinks = null) : ICenterReviewReportService
{
    public const int MaxDetailLength = 1000;
    public const int MaxResolutionNoteLength = 500;

    public async Task<CenterReviewReportDto> ReportAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid centerId,
        Guid reviewId,
        CreateCenterReviewReportRequest request,
        CancellationToken ct = default)
    {
        if (centerId == Guid.Empty || reviewId == Guid.Empty)
            throw new ArgumentException("Mã trung tâm hoặc mã đánh giá không hợp lệ.");

        var reason = request.Reason?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!CenterReviewReportReasons.All.Contains(reason))
            throw new ArgumentException("Lý do báo cáo không hợp lệ.");

        var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        if (detail is { Length: > MaxDetailLength })
            throw new ArgumentException($"Chi tiết thêm tối đa {MaxDetailLength} ký tự.");

        var user = await users.GetUserById(userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role is not (UserRoles.Student or UserRoles.Parent or UserRoles.Center))
            throw new UnauthorizedAccessException("Chỉ học sinh, phụ huynh hoặc trung tâm được báo cáo đánh giá.");

        // [UC-151 E3] Học sinh dưới 16 tuổi chưa được người đại diện đồng ý không được báo cáo
        if (user.Role == UserRoles.Student && parentLinks is not null
            && await AgeAccess.CheckAsync(AgeFeatures.ShareWithCenter, user, parentLinks, DateTime.UtcNow, ct) is not null)
        {
            throw new UnauthorizedAccessException("Học sinh dưới 16 tuổi cần được người đại diện đồng ý trước khi báo cáo đánh giá.");
        }

        // [UC-151 1a & E4] Trung tâm chỉ được báo cáo đánh giá về chính trung tâm mình
        var isCenterSelfReport = false;
        if (user.Role == UserRoles.Center)
        {
            var ownCenter = await centers.GetByOwnerOrEmailAsync(userId, userEmail ?? user.Email, ct);
            if (ownCenter is null || ownCenter.Id != centerId)
                throw new UnauthorizedAccessException("Trung tâm chỉ được báo cáo đánh giá về chính trung tâm mình.");
            isCenterSelfReport = true;
        }

        var center = await centers.GetByIdAsync(centerId, ct);
        if (center is null)
            throw new KeyNotFoundException("Không tìm thấy trung tâm tư vấn.");

        var review = await reports.GetVisibleReviewAsync(centerId, reviewId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá đang hiển thị của trung tâm này.");

        // [UC-151 E1] Không cho tự báo cáo đánh giá của chính mình
        if (review.UserId == userId)
            throw new ArgumentException("Bạn không thể báo cáo đánh giá do chính mình viết.");

        // [UC-151 E2] Đã có báo cáo đang mở của người dùng này cho đánh giá này
        if (await reports.HasOpenReportAsync(review.Id, userId, ct))
            throw new ArgumentException("Bạn đã gửi báo cáo cho đánh giá này và báo cáo đang chờ xử lý.");

        var entity = new CenterReviewReport
        {
            CenterReviewId = review.Id,
            StudyCenterId = centerId,
            ReporterUserId = userId,
            ReporterRole = user.Role,
            IsCenterSelfReport = isCenterSelfReport,
            Reason = reason,
            Detail = detail,
            Status = CenterReviewReportStatuses.Open,
        };

        if (!await reports.TryAddAsync(entity, ct))
            throw new ArgumentException("Bạn đã gửi báo cáo cho đánh giá này và báo cáo đang chờ xử lý.");

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center_review.report",
            EntityType = "center_review_report",
            EntityId = entity.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                centerReviewId = review.Id,
                studyCenterId = centerId,
                reporterRole = user.Role,
                isCenterSelfReport,
                reason,
                hasDetail = detail is not null,
            }),
        }, ct);

        return new CenterReviewReportDto(
            entity.Id,
            review.Id,
            centerId,
            center.Name,
            review.Rating,
            review.Comment,
            review.Status,
            "Người đánh giá",
            userId,
            user.FullName,
            user.Role,
            isCenterSelfReport,
            reason,
            detail,
            entity.Status,
            entity.CreatedAt);
    }

    public Task<PagedResult<CenterReviewReportDto>> SearchForModeratorAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        var filter = normalized switch
        {
            null or "" or "open" => "open",
            "resolved" => "resolved",
            "all" => "all",
            _ => throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận 'open', 'resolved' hoặc 'all'."),
        };
        return reports.SearchAsync(filter, page, pageSize, ct);
    }

    public async Task DismissAsync(Guid moderatorUserId, Guid reportId, string? note, CancellationToken ct = default)
    {
        if (reportId == Guid.Empty)
            throw new ArgumentException("Mã báo cáo không hợp lệ.");

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > MaxResolutionNoteLength })
            throw new ArgumentException($"Ghi chú tối đa {MaxResolutionNoteLength} ký tự.");

        var report = await reports.GetByIdAsync(reportId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy báo cáo đánh giá.");

        if (report.Status != CenterReviewReportStatuses.Open)
            throw new ArgumentException("Báo cáo này đã được xử lý.");

        report.Status = CenterReviewReportStatuses.Dismissed;
        report.ResolvedByUserId = moderatorUserId;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolutionNote = trimmedNote;
        await reports.UpdateAsync(report, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = moderatorUserId,
            Action = "center_review_report.dismiss",
            EntityType = "center_review_report",
            EntityId = report.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                centerReviewId = report.CenterReviewId,
                studyCenterId = report.StudyCenterId,
                note = trimmedNote,
            }),
        }, ct);
    }
}

// ===== [UC-152] Trung tâm phản hồi đánh giá =====

public record UpsertCenterReviewReplyRequest(string Content);

public interface ICenterReviewReplyService
{
    Task<CenterReviewsDto> GetOwnCenterReviewsAsync(Guid centerUserId, string? userEmail, int page, int pageSize, CancellationToken ct = default);
    Task<CenterReviewDto> CreateReplyAsync(Guid centerUserId, string? userEmail, Guid reviewId, UpsertCenterReviewReplyRequest request, CancellationToken ct = default);
    Task<CenterReviewDto> EditReplyAsync(Guid centerUserId, string? userEmail, Guid reviewId, UpsertCenterReviewReplyRequest request, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2185 / UC-152] Trung tâm viết tối đa một phản hồi công khai dưới đánh giá về trung tâm mình (tối đa 1.000 ký tự,
/// không chứa SĐT, email hay liên kết). Sửa được trong 24 giờ đầu kể từ lúc đăng.
/// </summary>
public partial class CenterReviewReplyService(
    IStudyCenterRepository centers,
    ICenterReviewRepository publicReviews,
    ICenterReviewModerationRepository moderationReviews,
    ICenterReviewSubmissionRepository reviewDtos,
    IAuditLogRepository auditLogs,
    INotificationService? notifications = null) : ICenterReviewReplyService
{
    public const int MaxReplyLength = 1000;
    public const int EditWindowHours = 24;

    [GeneratedRegex(@"(?i)\b[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)(?:https?://\S+|www\.\S+|\b[a-z0-9\-]+\.(?:com|vn|edu|org|net|io|co|me|ly|gl|info|biz|site|online)(?:/\S*)?\b)", RegexOptions.Compiled)]
    private static partial Regex UrlOrDomainRegex();

    public static string ValidateReplyContent(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ArgumentException("Nội dung phản hồi không được để trống.");
        if (trimmed.Length > MaxReplyLength)
            throw new ArgumentException($"Nội dung phản hồi tối đa {MaxReplyLength} ký tự.");
        if (LeadMessageContentFilter.ContainsPhoneNumber(trimmed))
            throw new ArgumentException("Phản hồi không được chứa số điện thoại.");
        if (EmailRegex().IsMatch(trimmed))
            throw new ArgumentException("Phản hồi không được chứa địa chỉ email.");
        if (UrlOrDomainRegex().IsMatch(trimmed))
            throw new ArgumentException("Phản hồi không được chứa liên kết hoặc tên miền website.");
        return trimmed;
    }

    public async Task<CenterReviewsDto> GetOwnCenterReviewsAsync(
        Guid centerUserId,
        string? userEmail,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var center = await centers.GetByOwnerOrEmailAsync(centerUserId, userEmail, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm gắn với tài khoản này.");

        return await publicReviews.GetVisibleAsync(center.Id, page, pageSize, ct);
    }

    public async Task<CenterReviewDto> CreateReplyAsync(
        Guid centerUserId,
        string? userEmail,
        Guid reviewId,
        UpsertCenterReviewReplyRequest request,
        CancellationToken ct = default)
    {
        var content = ValidateReplyContent(request.Content);
        var center = await RequireEligibleCenterAsync(centerUserId, userEmail, ct);

        var review = await moderationReviews.GetForUpdateAsync(center.Id, reviewId, ct);
        if (review is null || !string.Equals(review.Status, "visible", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Không tìm thấy đánh giá đang hiển thị của trung tâm.");

        // [UC-152 E1] Mỗi đánh giá tối đa một phản hồi của trung tâm
        if (review.CenterReplyCreatedAt.HasValue)
            throw new ArgumentException("Đánh giá này đã có phản hồi của trung tâm.");

        var now = DateTime.UtcNow;
        review.CenterReplyContent = content;
        review.CenterReplyAuthorUserId = centerUserId;
        review.CenterReplyCreatedAt = now;
        review.CenterReplyEditedAt = null;
        review.CenterReplyStatus = "visible";
        review.UpdatedAt = now;
        await moderationReviews.SaveAsync(review, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = centerUserId,
            Action = "center_review.reply_create",
            EntityType = "center_review",
            EntityId = review.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = center.Id, length = content.Length }),
        }, ct);

        // [UC-152 / UC-104] Thông báo cho người viết đánh giá (nếu tài khoản còn tồn tại)
        if (notifications is not null && review.UserId is { } reviewerId)
        {
            await notifications.NotifyAsync(
                reviewerId,
                "center_review_reply",
                $"{center.Name} đã phản hồi đánh giá của bạn",
                content.Length > 120 ? content[..120] + "…" : content,
                $"/centers/{center.Id}",
                ct);
        }

        return await reviewDtos.GetPublicDtoAsync(review.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá sau khi lưu phản hồi.");
    }

    public async Task<CenterReviewDto> EditReplyAsync(
        Guid centerUserId,
        string? userEmail,
        Guid reviewId,
        UpsertCenterReviewReplyRequest request,
        CancellationToken ct = default)
    {
        var content = ValidateReplyContent(request.Content);
        var center = await RequireEligibleCenterAsync(centerUserId, userEmail, ct);

        var review = await moderationReviews.GetForUpdateAsync(center.Id, reviewId, ct);
        if (review is null || !string.Equals(review.Status, "visible", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Không tìm thấy đánh giá đang hiển thị của trung tâm.");

        if (!review.CenterReplyCreatedAt.HasValue || !string.Equals(review.CenterReplyStatus, "visible", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Đánh giá này chưa có phản hồi đang hiển thị để chỉnh sửa.");

        // [UC-152 Flow 2a] Chỉ được sửa phản hồi trong 24 giờ đầu
        var now = DateTime.UtcNow;
        if (now > review.CenterReplyCreatedAt.Value.AddHours(EditWindowHours))
            throw new ArgumentException($"Chỉ có thể chỉnh sửa phản hồi trong vòng {EditWindowHours} giờ kể từ lúc đăng.");

        review.CenterReplyContent = content;
        review.CenterReplyEditedAt = now;
        review.UpdatedAt = now;
        await moderationReviews.SaveAsync(review, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = centerUserId,
            Action = "center_review.reply_edit",
            EntityType = "center_review",
            EntityId = review.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = center.Id, length = content.Length }),
        }, ct);

        return await reviewDtos.GetPublicDtoAsync(review.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đánh giá sau khi cập nhật phản hồi.");
    }

    private async Task<StudyCenter> RequireEligibleCenterAsync(Guid centerUserId, string? userEmail, CancellationToken ct)
    {
        var center = await centers.GetByOwnerOrEmailAsync(centerUserId, userEmail, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm gắn với tài khoản này.");

        // [UC-152 E3] Trung tâm đang bị ẩn hoặc bị thu hồi xác minh thì không cho phản hồi
        if (!center.IsActive || center.VerificationStatus != CenterVerificationStatuses.Verified)
            throw new ArgumentException("Trung tâm hiện đang bị ẩn hoặc chưa/bị thu hồi dấu xác minh nên không thể phản hồi đánh giá.");

        return center;
    }
}
