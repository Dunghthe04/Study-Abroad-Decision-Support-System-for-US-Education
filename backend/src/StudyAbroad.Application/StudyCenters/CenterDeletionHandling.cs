using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-93] Một yêu cầu xóa trung tâm trong danh sách của Administrator, kèm thông tin để kiểm tra danh tính người gửi.</summary>
public record AdminCenterDeletionRequestDto(
    Guid Id,
    Guid StudyCenterId,
    string CenterName,
    string? CenterEmail,
    string? CenterPhone,
    string CenterVerificationStatus,
    Guid? RequestedByUserId,
    string? RequesterName,
    string? RequesterEmail,
    /// <summary>Người gửi có đúng là tài khoản chủ trung tâm không: dữ kiện đầu tiên khi kiểm tra danh tính.</summary>
    bool RequesterIsOwner,
    string Reason,
    string Status,
    DateTime CreatedAt,
    Guid? HandledByUserId,
    DateTime? HandledAt,
    string? DecisionNote,
    DateTime? ScheduledPurgeAt,
    DateTime? CompletedAt);

/// <summary>[UC-93] Duyệt yêu cầu xóa: ghi chú kiểm tra danh tính là bắt buộc.</summary>
public record ApproveCenterDeletionRequest(string? IdentityCheckNote);

/// <summary>[UC-93] Từ chối yêu cầu xóa: lý do bắt buộc, gửi cho trung tâm qua email.</summary>
public record RejectCenterDeletionRequest(string? Reason);

public record CenterDeletionActionResult(AdminCenterDeletionRequestDto? Request, string? Error = null, bool NotFound = false)
{
    public bool Success => Error is null && Request is not null;
    public static CenterDeletionActionResult Ok(AdminCenterDeletionRequestDto request) => new(request);
    public static CenterDeletionActionResult Fail(string error) => new(null, error);
    public static CenterDeletionActionResult Missing() => new(null, "Không tìm thấy yêu cầu xóa.", true);
}

/// <summary>[UC-93] Người nhận thông báo của một lead bị đóng: học sinh và người bấm gửi (phụ huynh gửi thay).</summary>
public record ClosedLeadRecipients(Guid LeadId, Guid StudentUserId, Guid? SubmittedByUserId);

public record CenterPurgeFailure(Guid RequestId, Exception Error);

public record CenterPurgeSummary(int Purged, IReadOnlyList<CenterPurgeFailure> Failures);

/// <summary>[UC-93] Các câu lệnh dữ liệu riêng cho việc xóa trung tâm, chạy trong transaction do service mở.</summary>
public interface ICenterDeletionWorkRepository
{
    Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<AdminCenterDeletionRequestDto?> GetViewAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đóng các lead còn mở của trung tâm; lead chờ phụ huynh xác nhận thì hủy. Trả về người cần thông báo.</summary>
    Task<IReadOnlyList<ClosedLeadRecipients>> CloseOpenLeadsAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default);

    Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    /// <summary>Id các yêu cầu đã duyệt và đã tới hạn xóa hẳn.</summary>
    Task<List<Guid>> GetDuePurgeRequestIdsAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Xóa dữ liệu của trung tâm: hồ sơ xác minh và tài liệu, lịch sử thay đổi hồ sơ, rồi ẩn danh hóa dòng trung tâm
    /// (giữ dòng để lead và đánh giá cũ của người dùng không mất, nhưng không còn thông tin nhận diện).
    /// </summary>
    Task PurgeCenterDataAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default);

    /// <summary>Xóa tệp tài liệu xác minh trên ổ đĩa. Gọi sau khi DB đã lưu.</summary>
    Task DeleteCenterFilesAsync(Guid studyCenterId, CancellationToken ct = default);
}

public interface ICenterDeletionService
{
    Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<CenterDeletionActionResult> ApproveAsync(Guid adminUserId, Guid requestId, string? identityCheckNote, CancellationToken ct = default);
    Task<CenterDeletionActionResult> RejectAsync(Guid adminUserId, Guid requestId, string? reason, CancellationToken ct = default);
    /// <summary>Xóa hẳn các trung tâm đã duyệt xóa và quá thời hạn chờ (cùng thời hạn UC-80). Gọi từ job nền.</summary>
    Task<CenterPurgeSummary> PurgeDueAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>
/// [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm (gửi từ UC-92).
/// Luồng trạng thái: pending → approved (khóa đăng nhập, ẩn trung tâm, đóng lead mở, hẹn ngày xóa)
/// → completed (job nền xóa dữ liệu sau thời hạn chờ <see cref="AccountDeletionOptions.RetentionPeriod"/>, cùng thời hạn UC-80);
/// hoặc pending → rejected. Mỗi bước ghi nhật ký và báo kết quả qua email.
/// </summary>
public class CenterDeletionService(
    ICenterDeletionRequestRepository requests,
    ICenterDeletionWorkRepository work,
    IStudyCenterRepository centers,
    IUserRepository users,
    IAccountPurgeRepository accountPurge,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    AccountDeletionOptions? options = null) : ICenterDeletionService
{
    public const int MaxNoteLength = 1000;
    public const string NotificationType = "lead_closed_center_deleted";

    private readonly AccountDeletionOptions _options = options ?? new AccountDeletionOptions();

    private static readonly HashSet<string> Filters = new(StringComparer.OrdinalIgnoreCase)
    {
        CenterDeletionRequestStatuses.Pending,
        CenterDeletionRequestStatuses.Approved,
        CenterDeletionRequestStatuses.Completed,
        CenterDeletionRequestStatuses.Rejected,
    };

    public Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(status) || status.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : status.Trim().ToLowerInvariant();
        if (normalized is not null && !Filters.Contains(normalized))
            throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận pending, approved, completed, rejected hoặc all.");
        return work.SearchAsync(normalized, page, pageSize, ct);
    }

    public async Task<CenterDeletionActionResult> ApproveAsync(Guid adminUserId, Guid requestId, string? identityCheckNote, CancellationToken ct = default)
    {
        var note = identityCheckNote?.Trim() ?? string.Empty;
        if (note.Length == 0)
            return CenterDeletionActionResult.Fail("Vui lòng ghi lại kết quả kiểm tra danh tính người gửi yêu cầu.");
        if (note.Length > MaxNoteLength)
            return CenterDeletionActionResult.Fail($"Ghi chú tối đa {MaxNoteLength} ký tự.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return CenterDeletionActionResult.Missing();
        if (request.Status != CenterDeletionRequestStatuses.Pending)
            return CenterDeletionActionResult.Fail("Yêu cầu này đã được xử lý.");

        var center = await centers.GetByIdForUpdateAsync(request.StudyCenterId, ct);
        if (center is null)
            return CenterDeletionActionResult.Fail("Không tìm thấy trung tâm của yêu cầu này.");

        var now = DateTime.UtcNow;
        var purgeAt = now + _options.RetentionPeriod;
        IReadOnlyList<ClosedLeadRecipients> closed = [];

        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            // 1. Khóa đăng nhập tài khoản chủ trung tâm và thu hồi mọi phiên
            if (center.OwnerUserId is { } ownerId && await users.GetUserById(ownerId, txCt) is { } owner)
            {
                owner.Status = UserStatuses.Locked;
                owner.LockoutEnd = null;
                owner.FailedLoginAttempts = 0;
                await users.UpdateUserAsync(owner, txCt);
                await users.RevokeAllSessionsAsync(owner.Id, txCt);
            }

            // 2. Ẩn trung tâm khỏi danh bạ công khai
            center.IsActive = false;
            await centers.UpdateAsync(center, txCt);

            // 3. Đóng các yêu cầu liên hệ đang mở và báo người dùng liên quan
            closed = await work.CloseOpenLeadsAsync(center.Id, now, txCt);
            var notifications = BuildLeadClosedNotifications(closed, center.Name);
            if (notifications.Count > 0)
                await work.AddNotificationsAsync(notifications, txCt);

            // 4. Hẹn ngày xóa hẳn theo cùng thời hạn chờ của UC-80
            request.Status = CenterDeletionRequestStatuses.Approved;
            request.HandledByUserId = adminUserId;
            request.HandledAt = now;
            request.DecisionNote = note;
            request.ScheduledPurgeAt = purgeAt;
            await requests.UpdateAsync(request, txCt);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "center.deletion_approve",
                EntityType = "center_deletion_request",
                EntityId = request.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    studyCenterId = center.Id,
                    ownerUserId = center.OwnerUserId,
                    identityCheckNote = note,
                    closedLeads = closed.Count,
                    scheduledPurgeAt = purgeAt,
                }),
            }, txCt);
        }, ct);

        await SendOutcomeEmailsAsync(request, center.Name, center.Email, "approved", note, purgeAt, ct);
        return await ResultAsync(request.Id, ct);
    }

    public async Task<CenterDeletionActionResult> RejectAsync(Guid adminUserId, Guid requestId, string? reason, CancellationToken ct = default)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return CenterDeletionActionResult.Fail("Vui lòng nhập lý do từ chối.");
        if (trimmed.Length > MaxNoteLength)
            return CenterDeletionActionResult.Fail($"Lý do tối đa {MaxNoteLength} ký tự.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return CenterDeletionActionResult.Missing();
        if (request.Status != CenterDeletionRequestStatuses.Pending)
            return CenterDeletionActionResult.Fail("Yêu cầu này đã được xử lý.");

        var center = await centers.GetByIdAsync(request.StudyCenterId, ct);
        var now = DateTime.UtcNow;

        request.Status = CenterDeletionRequestStatuses.Rejected;
        request.HandledByUserId = adminUserId;
        request.HandledAt = now;
        request.DecisionNote = trimmed;
        await requests.UpdateAsync(request, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = "center.deletion_reject",
            EntityType = "center_deletion_request",
            EntityId = request.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = request.StudyCenterId, reason = trimmed }),
        }, ct);

        await SendOutcomeEmailsAsync(request, center?.Name ?? "Trung tâm", center?.Email, "rejected", trimmed, null, ct);
        return await ResultAsync(request.Id, ct);
    }

    public async Task<CenterPurgeSummary> PurgeDueAsync(DateTime now, CancellationToken ct = default)
    {
        var dueIds = await work.GetDuePurgeRequestIdsAsync(now, ct);
        var purged = 0;
        List<CenterPurgeFailure> failures = [];

        foreach (var requestId in dueIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var request = await requests.GetByIdAsync(requestId, ct);
                if (request is null || request.Status != CenterDeletionRequestStatuses.Approved)
                    continue;

                var center = await centers.GetByIdAsync(request.StudyCenterId, ct);
                var centerName = center?.Name ?? "Trung tâm";
                var ownerId = center?.OwnerUserId;

                // Lấy email trước khi xóa tài khoản để còn báo kết quả
                var recipients = await CollectRecipientsAsync(request, center?.Email, ct);

                await unitOfWork.ExecuteInTransactionAsync(async txCt =>
                {
                    await work.PurgeCenterDataAsync(request.StudyCenterId, now, txCt);
                    if (ownerId is { } id)
                        await accountPurge.DeleteUserAsync(id, txCt);

                    request.Status = CenterDeletionRequestStatuses.Completed;
                    request.CompletedAt = now;
                    await requests.UpdateAsync(request, txCt);

                    // Nhật ký chỉ giữ id, không giữ tên hay email đã xóa
                    await auditLogs.AddAsync(new AuditLog
                    {
                        ActorUserId = null,
                        Action = "center.purge",
                        EntityType = "study_center",
                        EntityId = request.StudyCenterId.ToString(),
                        DetailJson = JsonSerializer.Serialize(new { requestId = request.Id, ownerUserId = ownerId }),
                    }, txCt);
                }, ct);

                purged++;

                try
                {
                    await work.DeleteCenterFilesAsync(request.StudyCenterId, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add(new CenterPurgeFailure(request.Id, ex));
                }

                foreach (var email in recipients)
                    await emailSender.SendCenterDeletionOutcomeEmailAsync(email, centerName, "completed", null, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new CenterPurgeFailure(requestId, ex));
            }
        }

        return new CenterPurgeSummary(purged, failures);
    }

    private static List<Notification> BuildLeadClosedNotifications(IReadOnlyList<ClosedLeadRecipients> closed, string centerName)
    {
        var userIds = closed
            .SelectMany(c => c.SubmittedByUserId is { } submitter ? new[] { c.StudentUserId, submitter } : new[] { c.StudentUserId })
            .Distinct()
            .ToList();

        return userIds.Select(id => new Notification
        {
            UserId = id,
            Type = NotificationType,
            Title = "Yêu cầu liên hệ đã đóng",
            Body = $"{centerName} đã ngừng hoạt động trên USAS nên yêu cầu liên hệ của bạn với trung tâm này đã được đóng.",
            LinkUrl = "/account",
        }).ToList();
    }

    private async Task<List<string>> CollectRecipientsAsync(CenterDeletionRequest request, string? centerEmail, CancellationToken ct)
    {
        var emails = new List<string>();
        if (request.RequestedByUserId is { } requesterId && await users.GetUserById(requesterId, ct) is { } requester
            && !string.IsNullOrWhiteSpace(requester.Email))
            emails.Add(requester.Email.Trim());
        if (!string.IsNullOrWhiteSpace(centerEmail)
            && !emails.Contains(centerEmail.Trim(), StringComparer.OrdinalIgnoreCase))
            emails.Add(centerEmail.Trim());
        return emails;
    }

    private async Task SendOutcomeEmailsAsync(
        CenterDeletionRequest request, string centerName, string? centerEmail, string outcome, string? note, DateTime? purgeAt, CancellationToken ct)
    {
        foreach (var email in await CollectRecipientsAsync(request, centerEmail, ct))
            await emailSender.SendCenterDeletionOutcomeEmailAsync(email, centerName, outcome, note, purgeAt, ct);
    }

    private async Task<CenterDeletionActionResult> ResultAsync(Guid requestId, CancellationToken ct)
    {
        var view = await work.GetViewAsync(requestId, ct);
        return view is null ? CenterDeletionActionResult.Missing() : CenterDeletionActionResult.Ok(view);
    }
}
