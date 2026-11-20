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
    DateTime? CompletedAt,
    /// <summary>Trung tâm (hoặc tài khoản chủ) đang bị giữ pháp lý: không được duyệt xóa, job không xóa hẳn.</summary>
    bool IsLegalHold = false,
    string? LegalHoldReason = null,
    DateTime? LegalHoldAt = null,
    /// <summary>Số tin nhắn của trung tâm bị người dùng báo cáo mà Admin chưa xử lý (UC-58): dấu hiệu cần xem trước khi duyệt xóa.</summary>
    int OpenReportCount = 0,
    /// <summary>Hạn xử lý (20 ngày kể từ khi nhận, cộng thêm nếu đã gia hạn).</summary>
    DateTime? DueAt = null,
    DateTime? ExtendedAt = null,
    string? ExtensionNote = null);

/// <summary>
/// [UC-93] Thời hạn xử lý yêu cầu xóa dữ liệu theo Nghị định 356/2025/NĐ-CP Điều 5 khoản 4: thực hiện trong 20 ngày;
/// được gia hạn tối đa 1 lần, không quá 20 ngày, kèm thông báo lý do cho người yêu cầu.
/// </summary>
public static class CenterDeletionDeadlines
{
    public const int ProcessingDays = 20;
    public const int MaxExtensionDays = 20;

    /// <summary>Tối thiểu từ lúc duyệt tới lúc xóa hẳn, để yêu cầu duyệt muộn không bị xóa ngay trong cùng giờ.</summary>
    public static readonly TimeSpan MinGraceAfterApproval = TimeSpan.FromDays(1);

    /// <summary>Hạn xử lý của một yêu cầu; yêu cầu cũ chưa có hạn thì tính từ ngày gửi.</summary>
    public static DateTime DueAt(CenterDeletionRequest request) =>
        request.DueAt ?? request.CreatedAt.AddDays(ProcessingDays);
}

/// <summary>[UC-93] Gia hạn một lần thời gian xử lý yêu cầu xóa. Lý do bắt buộc và được gửi cho người yêu cầu.</summary>
public record ExtendCenterDeletionRequest(string? Reason);

/// <summary>[UC-93] Tình trạng giữ pháp lý của một trung tâm.</summary>
public record CenterLegalHoldDto(
    Guid StudyCenterId,
    string CenterName,
    bool IsLegalHold,
    string? Reason,
    DateTime? Since,
    Guid? ByUserId,
    string? ByName,
    /// <summary>Yêu cầu xóa đang chờ hoặc đã duyệt của trung tâm, nếu có.</summary>
    Guid? OpenDeletionRequestId,
    string? OpenDeletionRequestStatus,
    DateTime? ScheduledPurgeAt);

/// <summary>[UC-93] Bật hoặc gỡ giữ pháp lý: lý do bắt buộc, ghi vào audit_logs.</summary>
public record SetCenterLegalHoldRequest(string? Reason);

public record CenterLegalHoldResult(CenterLegalHoldDto? Hold, string? Error = null, bool NotFound = false)
{
    public bool Success => Error is null && Hold is not null;
    public static CenterLegalHoldResult Ok(CenterLegalHoldDto hold) => new(hold);
    public static CenterLegalHoldResult Fail(string error) => new(null, error);
    public static CenterLegalHoldResult Missing() => new(null, "Không tìm thấy trung tâm.", true);
}

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

    /// <summary>Id các yêu cầu đã duyệt, đã tới hạn xóa hẳn, trung tâm và tài khoản chủ đều không bị giữ pháp lý.</summary>
    Task<List<Guid>> GetDuePurgeRequestIdsAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Số tin nhắn của trung tâm bị báo cáo mà Admin chưa xử lý (UC-58).</summary>
    Task<int> CountOpenReportsAsync(Guid studyCenterId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra lại trong transaction rằng trung tâm và tài khoản chủ chưa bị giữ pháp lý, rồi khóa dòng trung tâm
    /// (SELECT ... FOR UPDATE) tới hết transaction: Admin bật giữ pháp lý cùng lúc phải chờ, không lọt qua bước xóa.
    /// </summary>
    Task<bool> LockCenterIfPurgeableAsync(Guid studyCenterId, CancellationToken ct = default);

    /// <summary>
    /// Ẩn danh hóa tài khoản chủ trung tâm thay vì xóa dòng: tin nhắn trung tâm đã gửi trong các yêu cầu liên hệ
    /// (khóa ngoại cascade theo người gửi) và nhật ký vẫn còn để truy vết, nhưng không còn thông tin nhận diện.
    /// Xóa phiên đăng nhập, mã OTP và thông báo của tài khoản.
    /// </summary>
    Task AnonymizeOwnerAccountAsync(Guid userId, DateTime now, CancellationToken ct = default);

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
    /// <summary>Gia hạn một lần (tối đa 20 ngày) thời gian xử lý yêu cầu đang chờ hoặc đã duyệt, gửi lý do cho người yêu cầu.</summary>
    Task<CenterDeletionActionResult> ExtendAsync(Guid adminUserId, Guid requestId, string? reason, CancellationToken ct = default);
    /// <summary>Xóa hẳn các trung tâm đã duyệt xóa và quá thời hạn chờ (cùng thời hạn UC-80). Gọi từ job nền.</summary>
    Task<CenterPurgeSummary> PurgeDueAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Tình trạng giữ pháp lý của trung tâm.</summary>
    Task<CenterLegalHoldResult> GetLegalHoldAsync(Guid studyCenterId, CancellationToken ct = default);

    /// <summary>
    /// Bật giữ pháp lý khi trung tâm bị nghi lừa đảo, có tranh chấp hoặc cơ quan có thẩm quyền yêu cầu giữ dữ liệu:
    /// áp cho cả trung tâm và tài khoản chủ, chặn duyệt xóa và chặn job xóa hẳn tới khi gỡ.
    /// </summary>
    Task<CenterLegalHoldResult> SetLegalHoldAsync(Guid adminUserId, Guid studyCenterId, string? reason, CancellationToken ct = default);

    /// <summary>Gỡ giữ pháp lý khi việc điều tra kết thúc. Lý do bắt buộc.</summary>
    Task<CenterLegalHoldResult> ReleaseLegalHoldAsync(Guid adminUserId, Guid studyCenterId, string? reason, CancellationToken ct = default);
}

/// <summary>
/// [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm (gửi từ UC-92).
/// Luồng trạng thái: pending → approved (khóa đăng nhập, ẩn trung tâm, đóng lead mở, hẹn ngày xóa)
/// → completed (job nền xóa dữ liệu khi tới hạn xử lý của yêu cầu, <see cref="CenterDeletionDeadlines"/>: 20 ngày kể từ khi nhận,
/// gia hạn một lần tối đa 20 ngày theo Nghị định 356/2025/NĐ-CP Điều 5 khoản 4); hoặc pending → rejected.
/// Nhận yêu cầu, gia hạn, duyệt, từ chối, không xóa được vì giữ pháp lý và xóa xong đều ghi nhật ký và báo qua email.
/// Giữ pháp lý (Legal Hold) là ngoại lệ của quyền xóa dữ liệu: khi trung tâm bị nghi lừa đảo, có tranh chấp
/// hoặc cơ quan có thẩm quyền yêu cầu giữ dữ liệu, yêu cầu xóa không được duyệt và dữ liệu không bị xóa hẳn tới khi gỡ.
/// Xóa hẳn = xóa hồ sơ, tài liệu và ẩn danh hóa trung tâm cùng tài khoản chủ; nhật ký, tin nhắn và bản ghi yêu cầu
/// được giữ (không còn thông tin nhận diện) để truy vết về sau.
/// </summary>
public class CenterDeletionService(
    ICenterDeletionRequestRepository requests,
    ICenterDeletionWorkRepository work,
    IStudyCenterRepository centers,
    IUserRepository users,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender) : ICenterDeletionService
{
    public const int MaxNoteLength = 1000;
    public const string NotificationType = "lead_closed_center_deleted";

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

        // Dữ liệu đang được giữ cho điều tra hoặc tranh chấp thì không chấp nhận xóa: chỉ được từ chối hoặc chờ gỡ giữ
        var owner = center.OwnerUserId is { } ownerUserId ? await users.GetUserById(ownerUserId, ct) : null;
        if (center.IsLegalHold || owner?.IsLegalHold == true)
            return CenterDeletionActionResult.Fail(
                "Trung tâm đang bị giữ pháp lý nên chưa thể duyệt xóa. Hãy từ chối yêu cầu hoặc gỡ giữ pháp lý khi việc điều tra kết thúc.");

        var openReports = await work.CountOpenReportsAsync(center.Id, ct);
        var now = DateTime.UtcNow;
        // Xóa hẳn vào hạn xử lý của yêu cầu (20 ngày kể từ khi nhận, cộng gia hạn nếu có), nhưng sau duyệt ít nhất 1 ngày
        var purgeAt = Max(CenterDeletionDeadlines.DueAt(request), now + CenterDeletionDeadlines.MinGraceAfterApproval);
        IReadOnlyList<ClosedLeadRecipients> closed = [];

        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            // 1. Khóa đăng nhập tài khoản chủ trung tâm và thu hồi mọi phiên
            if (owner is not null)
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
                    openReports,
                    scheduledPurgeAt = purgeAt,
                }),
            }, txCt);
        }, ct);

        await SendOutcomeEmailsAsync(request, center.Name, center.Email, "approved", note, purgeAt, ct);
        return await ResultAsync(request.Id, ct);
    }

    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;

    public async Task<CenterDeletionActionResult> ExtendAsync(Guid adminUserId, Guid requestId, string? reason, CancellationToken ct = default)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return CenterDeletionActionResult.Fail("Vui lòng nhập lý do gia hạn, lý do được gửi cho người yêu cầu.");
        if (trimmed.Length > MaxNoteLength)
            return CenterDeletionActionResult.Fail($"Lý do tối đa {MaxNoteLength} ký tự.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return CenterDeletionActionResult.Missing();
        if (request.Status is not (CenterDeletionRequestStatuses.Pending or CenterDeletionRequestStatuses.Approved))
            return CenterDeletionActionResult.Fail("Chỉ gia hạn được yêu cầu đang chờ xử lý hoặc đã duyệt, chưa xóa hẳn.");
        if (request.ExtendedAt is not null)
            return CenterDeletionActionResult.Fail("Yêu cầu này đã được gia hạn một lần, theo quy định không gia hạn thêm.");

        var now = DateTime.UtcNow;
        var previousDue = CenterDeletionDeadlines.DueAt(request);
        var newDue = previousDue.AddDays(CenterDeletionDeadlines.MaxExtensionDays);
        var center = await centers.GetByIdAsync(request.StudyCenterId, ct);

        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            request.DueAt = newDue;
            request.ExtendedAt = now;
            request.ExtensionNote = trimmed;
            // Đã duyệt: ngày xóa hẳn dời theo hạn mới
            if (request.Status == CenterDeletionRequestStatuses.Approved && request.ScheduledPurgeAt < newDue)
                request.ScheduledPurgeAt = newDue;
            await requests.UpdateAsync(request, txCt);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "center.deletion_extend",
                EntityType = "center_deletion_request",
                EntityId = request.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { studyCenterId = request.StudyCenterId, reason = trimmed, previousDueAt = previousDue, newDueAt = newDue }),
            }, txCt);
        }, ct);

        foreach (var email in await CollectRecipientsAsync(request, center?.Email, ct))
            await emailSender.SendCenterDeletionOutcomeEmailAsync(email, center?.Name ?? "Trung tâm", "extended", trimmed, newDue, ct);
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
                if (center is null || center.IsLegalHold)
                    continue;

                var centerName = center.Name;
                var ownerId = center.OwnerUserId;
                if (ownerId is { } heldOwnerId && await users.GetUserById(heldOwnerId, ct) is { IsLegalHold: true })
                    continue;

                // Lấy email trước khi ẩn danh tài khoản để còn báo kết quả
                var recipients = await CollectRecipientsAsync(request, center.Email, ct);

                var held = false;
                await unitOfWork.ExecuteInTransactionAsync(async txCt =>
                {
                    // Admin có thể vừa bật giữ pháp lý sau lần đọc ở trên: kiểm tra lại và khóa dòng trong transaction
                    if (!await work.LockCenterIfPurgeableAsync(request.StudyCenterId, txCt))
                    {
                        held = true;
                        return;
                    }

                    await work.PurgeCenterDataAsync(request.StudyCenterId, now, txCt);
                    if (ownerId is { } id)
                        await work.AnonymizeOwnerAccountAsync(id, now, txCt);

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

                if (held)
                    continue;
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

    public const int MaxLegalHoldReasonLength = 500;

    public async Task<CenterLegalHoldResult> GetLegalHoldAsync(Guid studyCenterId, CancellationToken ct = default)
    {
        var center = await centers.GetByIdAsync(studyCenterId, ct);
        return center is null ? CenterLegalHoldResult.Missing() : CenterLegalHoldResult.Ok(await ToHoldDtoAsync(center, ct));
    }

    public async Task<CenterLegalHoldResult> SetLegalHoldAsync(Guid adminUserId, Guid studyCenterId, string? reason, CancellationToken ct = default)
    {
        var (trimmed, error) = ValidateHoldReason(reason, "Vui lòng nhập lý do giữ pháp lý (ví dụ: số công văn, khiếu nại liên quan).");
        if (error is not null)
            return CenterLegalHoldResult.Fail(error);

        var center = await centers.GetByIdForUpdateAsync(studyCenterId, ct);
        if (center is null)
            return CenterLegalHoldResult.Missing();
        if (center.IsLegalHold)
            return CenterLegalHoldResult.Fail("Trung tâm đang được giữ pháp lý.");

        var latest = await requests.GetLatestByCenterIdAsync(center.Id, ct);
        if (latest?.Status == CenterDeletionRequestStatuses.Completed)
            return CenterLegalHoldResult.Fail("Dữ liệu trung tâm này đã bị xóa hẳn, không còn gì để giữ.");

        var now = DateTime.UtcNow;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            center.IsLegalHold = true;
            center.LegalHoldReason = trimmed;
            center.LegalHoldAt = now;
            center.LegalHoldByUserId = adminUserId;
            await centers.UpdateAsync(center, txCt);

            // Giữ cả tài khoản chủ để job xóa tài khoản (UC-80) cũng không xóa
            if (center.OwnerUserId is { } ownerId && await users.GetUserById(ownerId, txCt) is { } owner)
            {
                owner.IsLegalHold = true;
                owner.LegalHoldReason = trimmed;
                await users.UpdateUserAsync(owner, txCt);
            }

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "center.legal_hold_set",
                EntityType = "study_center",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    reason = trimmed,
                    ownerUserId = center.OwnerUserId,
                    deletionRequestId = latest?.Id,
                    deletionRequestStatus = latest?.Status,
                }),
            }, txCt);
        }, ct);

        // Luật 91/2025 Điều 14 khoản 5: không xóa được vì lý do chính đáng thì phải thông báo cho người yêu cầu.
        // Nội dung chung, không nêu việc điều tra hay lý do giữ.
        if (latest?.Status is CenterDeletionRequestStatuses.Pending or CenterDeletionRequestStatuses.Approved)
        {
            foreach (var email in await CollectRecipientsAsync(latest, center.Email, ct))
                await emailSender.SendCenterDeletionOutcomeEmailAsync(email, center.Name, "held", null, null, ct);
        }

        return CenterLegalHoldResult.Ok(await ToHoldDtoAsync(center, ct));
    }

    public async Task<CenterLegalHoldResult> ReleaseLegalHoldAsync(Guid adminUserId, Guid studyCenterId, string? reason, CancellationToken ct = default)
    {
        var (trimmed, error) = ValidateHoldReason(reason, "Vui lòng nhập lý do gỡ giữ pháp lý (ví dụ: kết luận điều tra).");
        if (error is not null)
            return CenterLegalHoldResult.Fail(error);

        var center = await centers.GetByIdForUpdateAsync(studyCenterId, ct);
        if (center is null)
            return CenterLegalHoldResult.Missing();
        var owner = center.OwnerUserId is { } ownerId ? await users.GetUserById(ownerId, ct) : null;
        if (!center.IsLegalHold && owner?.IsLegalHold != true)
            return CenterLegalHoldResult.Fail("Trung tâm không bị giữ pháp lý.");

        var previousReason = center.LegalHoldReason ?? owner?.LegalHoldReason;
        var previousSince = center.LegalHoldAt;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            center.IsLegalHold = false;
            center.LegalHoldReason = null;
            center.LegalHoldAt = null;
            center.LegalHoldByUserId = null;
            await centers.UpdateAsync(center, txCt);

            if (owner is not null && owner.IsLegalHold)
            {
                owner.IsLegalHold = false;
                owner.LegalHoldReason = null;
                await users.UpdateUserAsync(owner, txCt);
            }

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "center.legal_hold_release",
                EntityType = "study_center",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { reason = trimmed, previousReason, heldSince = previousSince }),
            }, txCt);
        }, ct);

        // Khép vòng thông báo: yêu cầu xóa đang mở được xử lý tiếp. Nội dung chung, không nhắc việc điều tra.
        var latest = await requests.GetLatestByCenterIdAsync(center.Id, ct);
        if (latest?.Status is CenterDeletionRequestStatuses.Pending or CenterDeletionRequestStatuses.Approved)
        {
            foreach (var email in await CollectRecipientsAsync(latest, center.Email, ct))
                await emailSender.SendCenterDeletionOutcomeEmailAsync(email, center.Name, "resumed", null, null, ct);
        }

        return CenterLegalHoldResult.Ok(await ToHoldDtoAsync(center, ct));
    }

    private static (string Value, string? Error) ValidateHoldReason(string? reason, string emptyMessage)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return (trimmed, emptyMessage);
        if (trimmed.Length > MaxLegalHoldReasonLength)
            return (trimmed, $"Lý do tối đa {MaxLegalHoldReasonLength} ký tự.");
        return (trimmed, null);
    }

    private async Task<CenterLegalHoldDto> ToHoldDtoAsync(StudyCenter center, CancellationToken ct)
    {
        var owner = center.OwnerUserId is { } ownerId ? await users.GetUserById(ownerId, ct) : null;
        var isHeld = center.IsLegalHold || owner?.IsLegalHold == true;
        var by = center.LegalHoldByUserId is { } byId ? await users.GetUserById(byId, ct) : null;
        var latest = await requests.GetLatestByCenterIdAsync(center.Id, ct);
        var open = latest?.Status is CenterDeletionRequestStatuses.Pending or CenterDeletionRequestStatuses.Approved ? latest : null;
        return new CenterLegalHoldDto(
            center.Id,
            center.Name,
            isHeld,
            isHeld ? center.LegalHoldReason ?? owner?.LegalHoldReason : null,
            isHeld ? center.LegalHoldAt : null,
            isHeld ? center.LegalHoldByUserId : null,
            isHeld ? by?.FullName : null,
            open?.Id,
            open?.Status,
            open?.ScheduledPurgeAt);
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
