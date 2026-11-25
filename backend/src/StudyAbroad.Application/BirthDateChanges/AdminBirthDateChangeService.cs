using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>[UC-150] Yêu cầu sửa ngày sinh Administrator xem trong danh sách và màn chi tiết.</summary>
public record AdminBirthDateRequestDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateOnly? CurrentBirthDate,
    DateOnly RequestedBirthDate,
    int? CurrentAge,
    int RequestedAge,
    /// <summary>Mốc tuổi (16, 18) mà ngày sinh mới làm tuổi đi qua, theo chiều lên hoặc xuống.</summary>
    IReadOnlyList<int> CrossedThresholds,
    string Reason,
    string Status,
    string? EvidenceType,
    bool HasEvidence,
    string? EvidenceFileName,
    string? RepresentativeEmail,
    string? RejectReason,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

/// <summary>Tệp minh chứng Admin mở để xem (đã ghi nhật ký truy cập).</summary>
public sealed record BirthDateEvidenceFile(Stream Content, string ContentType, string FileName);

/// <summary>Thông tin yêu cầu HTTP để ghi nhật ký truy cập.</summary>
public sealed record AuditRequestInfo(string? IpAddress, string? UserAgent);

public interface IAdminBirthDateChangeService
{
    Task<PagedResult<AdminBirthDateRequestDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    Task<BirthDateChangeResult<AdminBirthDateRequestDto>> GetAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>[UC-150 4] Ghi nhật ký truy cập trước, ghi không được thì không trả tệp (E3).</summary>
    Task<BirthDateChangeResult<BirthDateEvidenceFile>> OpenEvidenceAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default);

    Task<BirthDateChangeResult<AdminBirthDateRequestDto>> ApproveAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default);

    /// <summary>[UC-150 5b] Từ chối bắt buộc có lý do (E2).</summary>
    Task<BirthDateChangeResult<AdminBirthDateRequestDto>> RejectAsync(Guid requestId, Guid adminId, string? rejectReason, AuditRequestInfo info, CancellationToken ct = default);
}

public class AdminBirthDateChangeService(
    IBirthDateChangeRepository requests,
    IUserRepository users,
    IBirthDateEvidenceStorage evidenceStorage,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    INotificationService? notifications = null,
    TimeProvider? clock = null,
    // [UC-110 / UC-109] Học sinh về dưới 16: giữ yêu cầu liên hệ, mời người đại diện đồng ý; null trong test cũ
    StudyAbroad.Application.Leads.ILeadParentHoldService? leadHolds = null,
    StudyAbroad.Application.ParentLinks.IParentLinkService? parentLinkService = null,
    StudyAbroad.Application.ParentLinks.IParentLinkRepository? parentLinks = null) : IAdminBirthDateChangeService
{
    public const int MaxRejectReasonLength = 500;
    public const int MaxPageSize = 100;

    /// <summary>Loại thông báo trong hệ thống khi Admin duyệt / từ chối (NotificationDtos.cs không thuộc phần này nên đặt ở đây).</summary>
    public const string NotificationApproved = "birth_date_request_approved";
    public const string NotificationRejected = "birth_date_request_rejected";

    public const string ActionApproved = "user.birth_date_approved";
    public const string ActionRejected = "user.birth_date_rejected";
    private const string EntityType = "birth_date_change_request";

    private static readonly int[] Thresholds = [AgeGroups.ChildMaxAgeExclusive, AgeGroups.AdultAge];

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<PagedResult<AdminBirthDateRequestDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? BirthDateChangeStatuses.Pending : status.Trim().ToLowerInvariant();
        // "all" = mọi trạng thái
        var result = await requests.SearchAsync(statusFilter == "all" ? null : statusFilter, page, pageSize, ct);
        var today = Today();
        var items = result.Items
            .Select(r => ToDto(r.Request, r.FullName, r.Email, r.Role, r.Request.CurrentBirthDate, today))
            .ToList();
        return new PagedResult<AdminBirthDateRequestDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<BirthDateChangeResult<AdminBirthDateRequestDto>> GetAsync(Guid requestId, CancellationToken ct = default)
    {
        var entity = await requests.GetByIdAsync(requestId, ct);
        if (entity is null)
            return NotFound();
        var user = await users.GetUserById(entity.UserId, ct);
        return Ok(ToDto(entity, user, Today()));
    }

    public async Task<BirthDateChangeResult<BirthDateEvidenceFile>> OpenEvidenceAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default)
    {
        var entity = await requests.GetByIdAsync(requestId, ct);
        if (entity is null)
            return BirthDateChangeResult<BirthDateEvidenceFile>.Fail(BirthDateChangeError.NotFound, "Không tìm thấy yêu cầu sửa ngày sinh.");
        if (entity.EvidencePath is not { } path)
            return BirthDateChangeResult<BirthDateEvidenceFile>.Fail(BirthDateChangeError.NotFound, "Yêu cầu không có tệp minh chứng hoặc tệp đã bị xóa.");

        // E3: nhật ký phải ghi được trước khi mở tệp
        try
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminId,
                Action = AuditLogConstants.BirthDateEvidenceViewed,
                EntityType = EntityType,
                EntityId = entity.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    targetUserId = entity.UserId,
                    evidenceType = entity.EvidenceType,
                }),
                IpAddress = info.IpAddress,
                UserAgent = info.UserAgent,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BirthDateChangeResult<BirthDateEvidenceFile>.Fail(BirthDateChangeError.Validation,
                "Không ghi được nhật ký truy cập nên chưa mở được minh chứng. Vui lòng thử lại.");
        }

        var stream = evidenceStorage.Open(path);
        if (stream is null)
            return BirthDateChangeResult<BirthDateEvidenceFile>.Fail(BirthDateChangeError.NotFound, "Tệp minh chứng không còn trên máy chủ.");
        return BirthDateChangeResult<BirthDateEvidenceFile>.Ok(new BirthDateEvidenceFile(
            stream,
            entity.EvidenceContentType ?? "application/octet-stream",
            entity.EvidenceFileName ?? "minh-chung"));
    }

    public async Task<BirthDateChangeResult<AdminBirthDateRequestDto>> ApproveAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default)
    {
        var entity = await requests.GetByIdAsync(requestId, ct);
        if (entity is null)
            return NotFound();
        if (entity.Status != BirthDateChangeStatuses.Pending)
            return NotPending(entity);

        var user = await users.GetUserById(entity.UserId, ct);
        if (user is null || user.Status == UserStatuses.Managed || (user.Role != UserRoles.Student && user.Role != UserRoles.Parent))
            return Fail(BirthDateChangeError.Validation, "Tài khoản gửi yêu cầu không còn sửa được ngày sinh. Vui lòng từ chối yêu cầu này.");

        var today = Today();
        var newAge = AgePolicy.GetAge(entity.RequestedBirthDate, today);
        var oldDob = user.DateOfBirth;
        int? oldAge = oldDob is { } o ? AgePolicy.GetAge(o, today) : null;
        var now = _clock.GetUtcNow().UtcDateTime;
        var closed = false;
        // [UC-150 bước 4] Học sinh từ 16 trở lên (hoặc chưa có ngày sinh) về dưới 16
        var droppedUnder16 = user.Role == UserRoles.Student && newAge < AgeGroups.ChildMaxAgeExclusive
            && (oldAge is null || oldAge >= AgeGroups.ChildMaxAgeExclusive);
        // [UC-150 bước 5] Học sinh dưới 16 lên từ 16: yêu cầu liên hệ còn chờ phụ huynh không cần phụ huynh nữa
        var roseTo16 = user.Role == UserRoles.Student && newAge >= AgeGroups.ChildMaxAgeExclusive
            && oldAge is { } before && before < AgeGroups.ChildMaxAgeExclusive;
        var hasConsent = false;
        Func<CancellationToken, Task>? sendInvite = null;

        await unitOfWork.ExecuteInTransactionAsync(async tct =>
        {
            // E1: người khác vừa xử lý hoặc người dùng vừa hủy
            closed = await requests.TryCloseAsync(entity.Id, BirthDateChangeStatuses.Approved, adminId, null, now, tct);
            if (!closed)
                return;

            user.DateOfBirth = entity.RequestedBirthDate;
            await users.UpdateUserAsync(user, tct);

            if (droppedUnder16)
            {
                // [UC-110] Yêu cầu liên hệ chưa kết thúc chờ phụ huynh xác nhận, hạn 7 ngày tính từ lúc giữ
                if (leadHolds is not null)
                    await leadHolds.HoldOpenLeadsAsync(user.Id, now, tct);
                // [UC-109] Chưa có đồng ý thì mời người đại diện; mail UC-110 chỉ gửi sau khi phụ huynh đồng ý tài khoản
                hasConsent = parentLinks is not null && await parentLinks.HasGrantedConsentAsync(user.Id, tct);
                if (!hasConsent && parentLinkService is not null && !string.IsNullOrWhiteSpace(entity.RepresentativeEmail))
                    sendInvite = await parentLinkService.InviteRepresentativeAfterBirthDateChangeAsync(user, entity.RepresentativeEmail, tct);
            }

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminId,
                Action = ActionApproved,
                EntityType = EntityType,
                EntityId = entity.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    targetUserId = user.Id,
                    reason = entity.Reason,
                    evidenceType = entity.EvidenceType,
                    oldBirthDate = oldDob?.ToString("yyyy-MM-dd"),
                    newBirthDate = entity.RequestedBirthDate.ToString("yyyy-MM-dd"),
                    oldAge,
                    newAge,
                    result = BirthDateChangeStatuses.Approved,
                }),
                IpAddress = info.IpAddress,
                UserAgent = info.UserAgent,
            }, tct);
        }, ct);

        if (!closed)
            return await ReloadNotPendingAsync(entity.Id, ct);

        // Gửi mail sau commit: lời mời đồng ý (UC-109), hoặc đã có đồng ý thì mail xác nhận yêu cầu liên hệ đang giữ (UC-110)
        if (sendInvite is not null)
            await sendInvite(ct);
        else if (droppedUnder16 && hasConsent && leadHolds is not null)
            await leadHolds.SendHeldLeadConfirmationsAsync(user, ct);
        // Sau commit; lỗi ở đây thì job hằng ngày trả lại
        if (roseTo16 && leadHolds is not null)
            await leadHolds.ReleaseParentHoldAsync(user, now, ct);

        evidenceStorage.Delete(entity.EvidencePath);
        entity.Status = BirthDateChangeStatuses.Approved;
        entity.EvidencePath = null;
        entity.ReviewedByUserId = adminId;
        entity.ReviewedAt = now;

        await NotifyAsync(user.Id, NotificationApproved,
            "Yêu cầu sửa ngày sinh đã được duyệt",
            $"Ngày sinh của bạn đã được cập nhật thành {entity.RequestedBirthDate:dd/MM/yyyy}.", ct);

        return Ok(ToDto(entity, user, today), $"Đã duyệt. Ngày sinh mới {entity.RequestedBirthDate:dd/MM/yyyy}, tuổi hiện tại {newAge}.");
    }

    public async Task<BirthDateChangeResult<AdminBirthDateRequestDto>> RejectAsync(Guid requestId, Guid adminId, string? rejectReason, AuditRequestInfo info, CancellationToken ct = default)
    {
        // E2
        var reason = rejectReason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
            return Fail(BirthDateChangeError.Validation, "Vui lòng nhập lý do từ chối.");
        if (reason.Length > MaxRejectReasonLength)
            return Fail(BirthDateChangeError.Validation, $"Lý do từ chối tối đa {MaxRejectReasonLength} ký tự.");

        var entity = await requests.GetByIdAsync(requestId, ct);
        if (entity is null)
            return NotFound();
        if (entity.Status != BirthDateChangeStatuses.Pending)
            return NotPending(entity);

        var now = _clock.GetUtcNow().UtcDateTime;
        var closed = false;
        await unitOfWork.ExecuteInTransactionAsync(async tct =>
        {
            closed = await requests.TryCloseAsync(entity.Id, BirthDateChangeStatuses.Rejected, adminId, reason, now, tct);
            if (!closed)
                return;
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminId,
                Action = ActionRejected,
                EntityType = EntityType,
                EntityId = entity.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    targetUserId = entity.UserId,
                    reason = entity.Reason,
                    rejectReason = reason,
                    evidenceType = entity.EvidenceType,
                    oldBirthDate = entity.CurrentBirthDate?.ToString("yyyy-MM-dd"),
                    newBirthDate = entity.RequestedBirthDate.ToString("yyyy-MM-dd"),
                    result = BirthDateChangeStatuses.Rejected,
                }),
                IpAddress = info.IpAddress,
                UserAgent = info.UserAgent,
            }, tct);
        }, ct);

        if (!closed)
            return await ReloadNotPendingAsync(entity.Id, ct);

        evidenceStorage.Delete(entity.EvidencePath);
        entity.Status = BirthDateChangeStatuses.Rejected;
        entity.EvidencePath = null;
        entity.ReviewedByUserId = adminId;
        entity.ReviewedAt = now;
        entity.RejectReason = reason;

        await NotifyAsync(entity.UserId, NotificationRejected,
            "Yêu cầu sửa ngày sinh bị từ chối",
            $"Administrator đã từ chối yêu cầu sửa ngày sinh của bạn. Lý do: {reason}", ct);

        var user = await users.GetUserById(entity.UserId, ct);
        return Ok(ToDto(entity, user, Today()), "Đã từ chối yêu cầu sửa ngày sinh.");
    }

    /// <summary>Mốc 16 / 18 mà tuổi đi qua giữa tuổi cũ và tuổi mới (cả hai chiều). Tuổi cũ không rõ thì xét tuổi mới đạt mốc.</summary>
    public static IReadOnlyList<int> CrossedThresholds(int? oldAge, int newAge) =>
        Thresholds.Where(t => oldAge is { } old
                ? (old < t) != (newAge < t)
                : newAge >= t)
            .ToList();

    private async Task NotifyAsync(Guid userId, string type, string title, string body, CancellationToken ct)
    {
        if (notifications is null)
            return;
        try
        {
            await notifications.NotifyAsync(userId, type, title, body, "/account", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Thông báo lỗi không làm hỏng kết quả duyệt đã lưu
        }
    }

    private async Task<BirthDateChangeResult<AdminBirthDateRequestDto>> ReloadNotPendingAsync(Guid id, CancellationToken ct)
    {
        var latest = await requests.GetByIdAsync(id, ct);
        return latest is null ? NotFound() : NotPending(latest);
    }

    private DateOnly Today() => AgePolicy.TodayInVietnam(_clock.GetUtcNow().UtcDateTime);

    private static AdminBirthDateRequestDto ToDto(BirthDateChangeRequest r, User? user, DateOnly today) =>
        ToDto(r, user?.FullName ?? string.Empty, user?.Email ?? string.Empty, user?.Role ?? string.Empty,
            // Còn chờ duyệt: so với ngày sinh đang lưu; đã đóng: so với ngày sinh lúc gửi
            r.Status == BirthDateChangeStatuses.Pending && user is not null ? user.DateOfBirth : r.CurrentBirthDate,
            today);

    private static AdminBirthDateRequestDto ToDto(BirthDateChangeRequest r, string fullName, string email, string role, DateOnly? currentDob, DateOnly today)
    {
        int? currentAge = currentDob is { } d ? AgePolicy.GetAge(d, today) : null;
        var requestedAge = AgePolicy.GetAge(r.RequestedBirthDate, today);
        return new AdminBirthDateRequestDto(
            r.Id, r.UserId, fullName, email, role,
            currentDob, r.RequestedBirthDate, currentAge, requestedAge,
            CrossedThresholds(currentAge, requestedAge),
            r.Reason, r.Status, r.EvidenceType, r.EvidencePath is not null, r.EvidenceFileName,
            r.RepresentativeEmail, r.RejectReason, r.CreatedAt, r.ReviewedAt);
    }

    private static BirthDateChangeResult<AdminBirthDateRequestDto> NotPending(BirthDateChangeRequest entity) =>
        Fail(BirthDateChangeError.Conflict, entity.Status switch
        {
            BirthDateChangeStatuses.Cancelled => "Người dùng đã hủy yêu cầu này.",
            _ => "Yêu cầu đã được xử lý trước đó.",
        });

    private static BirthDateChangeResult<AdminBirthDateRequestDto> NotFound() =>
        Fail(BirthDateChangeError.NotFound, "Không tìm thấy yêu cầu sửa ngày sinh.");

    private static BirthDateChangeResult<AdminBirthDateRequestDto> Ok(AdminBirthDateRequestDto dto, string message = "") =>
        BirthDateChangeResult<AdminBirthDateRequestDto>.Ok(dto, message);

    private static BirthDateChangeResult<AdminBirthDateRequestDto> Fail(BirthDateChangeError error, string message) =>
        BirthDateChangeResult<AdminBirthDateRequestDto>.Fail(error, message);
}
