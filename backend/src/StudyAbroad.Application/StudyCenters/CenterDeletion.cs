using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-92, UC-93] Một yêu cầu xóa tài khoản và dữ liệu trung tâm.</summary>
public record CenterDeletionRequestDto(
    Guid Id,
    Guid? StudyCenterId,
    string CenterName,
    string? ContactEmail,
    string? Reason,
    string Status,
    DateTime CreatedAt,
    DateTime DueAt,
    bool IdentityVerified,
    Guid? ProcessedByUserId,
    DateTime? ProcessedAt,
    string? AdminNote);

/// <summary>[UC-92] Trung tâm gửi yêu cầu xóa. Lý do không bắt buộc, tối đa 1000 ký tự.</summary>
public record CreateCenterDeletionRequest(string? Reason = null);

/// <summary>[UC-93] Administrator thực hiện xóa. Phải xác nhận đã kiểm tra danh tính người yêu cầu.</summary>
public record CompleteCenterDeletionRequest(bool IdentityVerified, string? Note = null);

/// <summary>[UC-93] Administrator từ chối yêu cầu xóa. Lý do bắt buộc.</summary>
public record RejectCenterDeletionRequest(string? Reason = null);

/// <summary>Trung tâm và tài khoản chủ trung tâm tại thời điểm gửi yêu cầu.</summary>
public record CenterDeletionTarget(Guid StudyCenterId, string CenterName, string? ContactEmail);

/// <summary>Kết quả sau khi xóa dữ liệu: những người có yêu cầu liên hệ đang mở vừa bị đóng, để gửi thông báo.</summary>
public record CenterDeletionOutcome(int ClosedLeadCount, IReadOnlyList<Guid> AffectedUserIds);

public interface ICenterDeletionRepository
{
    /// <summary>Trung tâm do tài khoản này làm chủ, null nếu không có.</summary>
    Task<CenterDeletionTarget?> GetTargetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);

    Task<CenterDeletionRequest?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default);

    Task<CenterDeletionRequest?> GetPendingForCenterAsync(Guid centerId, CancellationToken ct = default);

    Task<CenterDeletionRequest?> GetForUpdateAsync(Guid requestId, CancellationToken ct = default);

    Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default);

    Task SaveAsync(CenterDeletionRequest request, CancellationToken ct = default);

    Task<PagedResult<CenterDeletionRequest>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Xóa tài khoản và dữ liệu trung tâm trong một giao dịch: đóng các yêu cầu liên hệ đang mở, gỡ trung tâm khỏi danh bạ và xóa
    /// thông tin hiển thị, liên lạc, pháp lý; vô hiệu và ẩn danh tài khoản chủ trung tâm, thu hồi mọi phiên đăng nhập.
    /// Các dòng lead, đánh giá của học sinh / phụ huynh được giữ lại (không còn chứa dữ liệu của trung tâm).
    /// </summary>
    Task<CenterDeletionOutcome> ExecuteDeletionAsync(Guid? centerId, Guid? ownerUserId, CancellationToken ct = default);
}

public interface ICenterDeletionService
{
    Task<CenterDeletionRequestDto> RequestAsync(Guid centerUserId, CreateCenterDeletionRequest request, CancellationToken ct = default);

    Task<CenterDeletionRequestDto?> GetMineAsync(Guid centerUserId, CancellationToken ct = default);

    Task<CenterDeletionRequestDto> CancelAsync(Guid centerUserId, CancellationToken ct = default);

    Task<PagedResult<CenterDeletionRequestDto>> GetAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    Task<CenterDeletionRequestDto> CompleteAsync(Guid adminUserId, Guid requestId, CompleteCenterDeletionRequest request, CancellationToken ct = default);

    Task<CenterDeletionRequestDto> RejectAsync(Guid adminUserId, Guid requestId, RejectCenterDeletionRequest request, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2120 / UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu từ trang hồ sơ.
/// [USAS-2121 / UC-93] Administrator nhận yêu cầu, kiểm tra danh tính người yêu cầu, thực hiện xóa theo thời hạn đã công bố
/// và báo kết quả qua email. Các yêu cầu liên hệ đang mở của trung tâm được đóng và người dùng liên quan nhận thông báo.
/// </summary>
public class CenterDeletionService(
    ICenterDeletionRepository repository,
    IAuditLogRepository auditLogs,
    INotificationService? notifications = null,
    IEmailSender? emailSender = null) : ICenterDeletionService
{
    /// <summary>Thời hạn xử lý đã công bố, tính từ lúc trung tâm gửi yêu cầu.</summary>
    public static readonly TimeSpan ProcessingDeadline = TimeSpan.FromDays(30);

    public const string StatusPending = "pending";
    public const string StatusCompleted = "completed";
    public const string StatusRejected = "rejected";
    public const string StatusCancelled = "cancelled";

    public const int MaxReasonLength = 1000;
    public const int MaxPageSize = 50;

    private static readonly HashSet<string> StatusFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusPending, StatusCompleted, StatusRejected, StatusCancelled, "all",
    };

    public async Task<CenterDeletionRequestDto> RequestAsync(Guid centerUserId, CreateCenterDeletionRequest request, CancellationToken ct = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is { Length: > MaxReasonLength })
            throw new ArgumentException($"Lý do tối đa {MaxReasonLength} ký tự.");

        var target = await repository.GetTargetByOwnerAsync(centerUserId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm cho tài khoản này.");

        if (await repository.GetPendingForCenterAsync(target.StudyCenterId, ct) is not null)
            throw new ArgumentException("Trung tâm đã có một yêu cầu xóa đang chờ xử lý.");

        var now = DateTime.UtcNow;
        var entity = new CenterDeletionRequest
        {
            StudyCenterId = target.StudyCenterId,
            RequestedByUserId = centerUserId,
            CenterName = target.CenterName,
            ContactEmail = target.ContactEmail,
            Reason = reason,
            Status = StatusPending,
            CreatedAt = now,
            DueAt = now.Add(ProcessingDeadline),
        };
        await repository.AddAsync(entity, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = centerUserId,
            Action = "center.deletion_requested",
            EntityType = "center_deletion_request",
            EntityId = entity.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = target.StudyCenterId, dueAt = entity.DueAt, hasReason = reason is not null }),
        }, ct);

        if (notifications is not null)
        {
            foreach (var adminId in await repository.GetActiveAdminUserIdsAsync(ct))
            {
                await TryNotifyAsync(adminId, "center_deletion_request", "Có yêu cầu xóa tài khoản trung tâm",
                    $"{target.CenterName} yêu cầu xóa tài khoản và dữ liệu. Hạn xử lý: {entity.DueAt:dd/MM/yyyy}.",
                    "/admin/center-deletion-requests", ct);
            }
        }

        return ToDto(entity);
    }

    public async Task<CenterDeletionRequestDto?> GetMineAsync(Guid centerUserId, CancellationToken ct = default)
    {
        var latest = await repository.GetLatestForUserAsync(centerUserId, ct);
        return latest is null ? null : ToDto(latest);
    }

    /// <summary>Trung tâm rút lại yêu cầu khi Administrator chưa xử lý.</summary>
    public async Task<CenterDeletionRequestDto> CancelAsync(Guid centerUserId, CancellationToken ct = default)
    {
        var target = await repository.GetTargetByOwnerAsync(centerUserId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm cho tài khoản này.");
        var pending = await repository.GetPendingForCenterAsync(target.StudyCenterId, ct)
            ?? throw new KeyNotFoundException("Không có yêu cầu xóa nào đang chờ xử lý.");

        pending.Status = StatusCancelled;
        pending.ProcessedAt = DateTime.UtcNow;
        await repository.SaveAsync(pending, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = centerUserId,
            Action = "center.deletion_cancelled",
            EntityType = "center_deletion_request",
            EntityId = pending.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = pending.StudyCenterId }),
        }, ct);

        return ToDto(pending);
    }

    public async Task<PagedResult<CenterDeletionRequestDto>> GetAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? StatusPending : status.Trim().ToLowerInvariant();
        if (!StatusFilters.Contains(normalized))
            throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận 'pending', 'completed', 'rejected', 'cancelled' hoặc 'all'.");

        var paged = await repository.SearchAsync(normalized, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), ct);
        return new PagedResult<CenterDeletionRequestDto>(paged.Items.Select(ToDto).ToList(), paged.Page, paged.PageSize, paged.TotalCount);
    }

    public async Task<CenterDeletionRequestDto> CompleteAsync(
        Guid adminUserId,
        Guid requestId,
        CompleteCenterDeletionRequest request,
        CancellationToken ct = default)
    {
        if (!request.IdentityVerified)
            throw new ArgumentException("Phải xác nhận đã kiểm tra danh tính người yêu cầu trước khi xóa.");
        var note = TrimOrNull(request.Note);
        if (note is { Length: > MaxReasonLength })
            throw new ArgumentException($"Ghi chú tối đa {MaxReasonLength} ký tự.");

        var entity = await GetPendingAsync(requestId, ct);

        var outcome = await repository.ExecuteDeletionAsync(entity.StudyCenterId, entity.RequestedByUserId, ct);

        entity.Status = StatusCompleted;
        entity.IdentityVerified = true;
        entity.ProcessedByUserId = adminUserId;
        entity.ProcessedAt = DateTime.UtcNow;
        entity.AdminNote = note;
        await repository.SaveAsync(entity, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = "center.deleted",
            EntityType = "center_deletion_request",
            EntityId = entity.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                studyCenterId = entity.StudyCenterId,
                centerName = entity.CenterName,
                closedLeads = outcome.ClosedLeadCount,
                notifiedUsers = outcome.AffectedUserIds.Count,
                overdue = entity.ProcessedAt > entity.DueAt,
                note,
            }),
        }, ct);

        // Người dùng có yêu cầu liên hệ đang mở với trung tâm này nhận thông báo
        foreach (var userId in outcome.AffectedUserIds)
        {
            await TryNotifyAsync(userId, "lead_closed", "Yêu cầu liên hệ của bạn đã được đóng",
                $"{entity.CenterName} đã ngừng hoạt động trên hệ thống nên yêu cầu liên hệ của bạn với trung tâm này đã được đóng.",
                "/centers", ct);
        }

        await TrySendResultEmailAsync(entity, completed: true, note, ct);
        return ToDto(entity);
    }

    public async Task<CenterDeletionRequestDto> RejectAsync(
        Guid adminUserId,
        Guid requestId,
        RejectCenterDeletionRequest request,
        CancellationToken ct = default)
    {
        var reason = TrimOrNull(request.Reason)
            ?? throw new ArgumentException("Từ chối yêu cầu xóa thì phải nhập lý do.");
        if (reason.Length > MaxReasonLength)
            throw new ArgumentException($"Lý do tối đa {MaxReasonLength} ký tự.");

        var entity = await GetPendingAsync(requestId, ct);

        entity.Status = StatusRejected;
        entity.ProcessedByUserId = adminUserId;
        entity.ProcessedAt = DateTime.UtcNow;
        entity.AdminNote = reason;
        await repository.SaveAsync(entity, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = "center.deletion_rejected",
            EntityType = "center_deletion_request",
            EntityId = entity.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studyCenterId = entity.StudyCenterId, reason }),
        }, ct);

        if (entity.RequestedByUserId is { } ownerId)
        {
            await TryNotifyAsync(ownerId, "center_deletion_request", "Yêu cầu xóa tài khoản bị từ chối",
                $"Lý do: {reason}", "/center/profile", ct);
        }

        await TrySendResultEmailAsync(entity, completed: false, reason, ct);
        return ToDto(entity);
    }

    private async Task<CenterDeletionRequest> GetPendingAsync(Guid requestId, CancellationToken ct)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu không hợp lệ.");

        var entity = await repository.GetForUpdateAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu xóa.");
        if (entity.Status != StatusPending)
            throw new ArgumentException("Yêu cầu này đã được xử lý.");
        return entity;
    }

    /// <summary>Thông báo là phụ: lỗi khi ghi thông báo không làm hỏng thao tác chính.</summary>
    private async Task TryNotifyAsync(Guid userId, string type, string title, string body, string link, CancellationToken ct)
    {
        if (notifications is null) return;
        try
        {
            await notifications.NotifyAsync(userId, type, title, body, link, ct);
        }
        catch (Exception)
        {
            // bỏ qua
        }
    }

    private async Task TrySendResultEmailAsync(CenterDeletionRequest entity, bool completed, string? note, CancellationToken ct)
    {
        if (emailSender is null || string.IsNullOrWhiteSpace(entity.ContactEmail)) return;
        try
        {
            await emailSender.SendCenterDeletionResultEmailAsync(entity.ContactEmail, entity.CenterName, completed, note, ct);
        }
        catch (Exception)
        {
            // kết quả đã lưu; email là kênh báo phụ
        }
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CenterDeletionRequestDto ToDto(CenterDeletionRequest r) =>
        new(r.Id, r.StudyCenterId, r.CenterName, r.ContactEmail, r.Reason, r.Status, r.CreatedAt, r.DueAt,
            r.IdentityVerified, r.ProcessedByUserId, r.ProcessedAt, r.AdminNote);
}
