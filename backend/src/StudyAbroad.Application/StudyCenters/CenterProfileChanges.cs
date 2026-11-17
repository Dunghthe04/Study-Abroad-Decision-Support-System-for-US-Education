using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-114] Một thay đổi website / fanpage của trung tâm chờ hoặc đã được Moderator kiểm tra, kèm giá trị cũ và mới.</summary>
public record CenterProfileChangeDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    string Field,
    string? OldValue,
    string? NewValue,
    string Status,
    DateTime CreatedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? ReviewReason);

/// <summary>[UC-114] Kết quả kiểm tra: Đạt hoặc Không đạt. Không đạt bắt buộc có lý do (tối đa 500 ký tự).</summary>
public record ReviewCenterProfileChangeRequest(bool Passed, string? Reason = null);

public interface ICenterProfileChangeRepository
{
    /// <summary>Thay đổi đang chờ kiểm tra của một trường trên một trung tâm (được theo dõi để sửa), null nếu không có.</summary>
    Task<CenterProfileChange?> GetPendingAsync(Guid centerId, string field, CancellationToken ct = default);

    Task<CenterProfileChange?> GetForUpdateAsync(Guid changeId, CancellationToken ct = default);

    Task AddAsync(CenterProfileChange change, CancellationToken ct = default);

    Task SaveAsync(CenterProfileChange change, CancellationToken ct = default);

    /// <summary>Danh sách thay đổi theo trạng thái ("all" là mọi trạng thái), chờ lâu nhất lên đầu với pending, mới nhất lên đầu với còn lại.</summary>
    Task<PagedResult<CenterProfileChangeDto>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Tên trung tâm và tài khoản chủ trung tâm, để gửi thông báo kết quả.</summary>
    Task<(string Name, Guid? OwnerUserId)?> GetCenterInfoAsync(Guid centerId, CancellationToken ct = default);
}

public interface ICenterProfileChangeService
{
    /// <summary>[UC-92] Ghi nhận trung tâm vừa đổi một trường cần kiểm tra. Giá trị không đổi thì không làm gì.</summary>
    Task RecordAsync(Guid centerId, Guid changedByUserId, string field, string? oldValue, string? newValue, CancellationToken ct = default);

    Task<PagedResult<CenterProfileChangeDto>> GetAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    Task<CenterProfileChangeDto> ReviewAsync(Guid moderatorUserId, Guid changeId, ReviewCenterProfileChangeRequest request, CancellationToken ct = default);
}

/// <summary>
/// [USAS-2138 / UC-114] Moderator xem các thay đổi website / fanpage mà trung tâm tự sửa ở UC-92, đối chiếu giá trị cũ và mới
/// rồi chọn Đạt hoặc Không đạt (kèm lý do bắt buộc). Không đạt thì trung tâm nhận thông báo để sửa lại. Mọi thao tác ghi nhật ký.
/// Thay đổi tên pháp lý, mã số doanh nghiệp và tài liệu không qua đây mà đi theo luồng duyệt hồ sơ xác minh (UC-59).
/// </summary>
public class CenterProfileChangeService(
    ICenterProfileChangeRepository changes,
    IAuditLogRepository auditLogs,
    INotificationService? notifications = null) : ICenterProfileChangeService
{
    public const string FieldWebsite = "website";
    public const string FieldFanpage = "fanpage";

    public const string StatusPending = "pending";
    public const string StatusPassed = "passed";
    public const string StatusFailed = "failed";
    public const string StatusWithdrawn = "withdrawn";

    public const int MaxReasonLength = 500;
    public const int MaxPageSize = 50;

    private static readonly HashSet<string> StatusFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusPending, StatusPassed, StatusFailed, StatusWithdrawn, "all",
    };

    public static string FieldLabel(string field) => field == FieldFanpage ? "fanpage" : "website";

    /// <summary>So sánh hai giá trị đường dẫn: bỏ khoảng trắng đầu cuối, coi rỗng và null là một, không phân biệt hoa thường.</summary>
    public static bool SameValue(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    public async Task RecordAsync(
        Guid centerId,
        Guid changedByUserId,
        string field,
        string? oldValue,
        string? newValue,
        CancellationToken ct = default)
    {
        if (SameValue(oldValue, newValue)) return;

        var pending = await changes.GetPendingAsync(centerId, field, ct);
        if (pending is not null)
        {
            // Đổi tiếp khi lần trước chưa được kiểm tra: giữ giá trị cũ ban đầu để Moderator đối chiếu đúng gốc
            if (SameValue(pending.OldValue, newValue))
            {
                // Trung tâm đổi lại đúng giá trị trước đó: không còn gì để kiểm tra
                pending.Status = StatusWithdrawn;
                pending.NewValue = NullIfEmpty(newValue);
            }
            else
            {
                pending.NewValue = NullIfEmpty(newValue);
                pending.ChangedByUserId = changedByUserId;
            }
            await changes.SaveAsync(pending, ct);
            return;
        }

        await changes.AddAsync(new CenterProfileChange
        {
            StudyCenterId = centerId,
            Field = field,
            OldValue = NullIfEmpty(oldValue),
            NewValue = NullIfEmpty(newValue),
            Status = StatusPending,
            ChangedByUserId = changedByUserId,
        }, ct);
    }

    public Task<PagedResult<CenterProfileChangeDto>> GetAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? StatusPending : status.Trim().ToLowerInvariant();
        if (!StatusFilters.Contains(normalized))
            throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận 'pending', 'passed', 'failed', 'withdrawn' hoặc 'all'.");

        return changes.SearchAsync(normalized, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), ct);
    }

    public async Task<CenterProfileChangeDto> ReviewAsync(
        Guid moderatorUserId,
        Guid changeId,
        ReviewCenterProfileChangeRequest request,
        CancellationToken ct = default)
    {
        if (changeId == Guid.Empty)
            throw new ArgumentException("Mã thay đổi không hợp lệ.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (!request.Passed && reason is null)
            throw new ArgumentException("Chọn Không đạt thì phải nhập lý do.");
        if (reason is { Length: > MaxReasonLength })
            throw new ArgumentException($"Lý do tối đa {MaxReasonLength} ký tự.");

        var change = await changes.GetForUpdateAsync(changeId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thay đổi cần kiểm tra.");
        if (change.Status != StatusPending)
            throw new ArgumentException("Thay đổi này đã được xử lý.");

        change.Status = request.Passed ? StatusPassed : StatusFailed;
        change.ReviewedByUserId = moderatorUserId;
        change.ReviewedAt = DateTime.UtcNow;
        change.ReviewReason = reason;
        await changes.SaveAsync(change, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = moderatorUserId,
            Action = request.Passed ? "center.profile_change_passed" : "center.profile_change_failed",
            EntityType = "center_profile_change",
            EntityId = change.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                studyCenterId = change.StudyCenterId,
                field = change.Field,
                oldValue = change.OldValue,
                newValue = change.NewValue,
                reason,
            }),
        }, ct);

        var center = await changes.GetCenterInfoAsync(change.StudyCenterId, ct);
        await NotifyCenterAsync(center?.OwnerUserId, change, request.Passed, reason, ct);

        return new CenterProfileChangeDto(
            change.Id, change.StudyCenterId, center?.Name ?? string.Empty, change.Field, change.OldValue, change.NewValue,
            change.Status, change.CreatedAt, change.ReviewedByUserId, change.ReviewedAt, change.ReviewReason);
    }

    /// <summary>Báo kết quả cho tài khoản trung tâm. Ghi thông báo lỗi thì bỏ qua: kết quả kiểm tra đã lưu.</summary>
    private async Task NotifyCenterAsync(Guid? ownerUserId, CenterProfileChange change, bool passed, string? reason, CancellationToken ct)
    {
        if (notifications is null || ownerUserId is not { } ownerId) return;
        try
        {
            var label = FieldLabel(change.Field);
            if (passed)
            {
                await notifications.NotifyAsync(ownerId, "center_profile_change",
                    $"Thay đổi {label} đã được kiểm tra: Đạt", null, "/center/profile", ct);
            }
            else
            {
                await notifications.NotifyAsync(ownerId, "center_profile_change",
                    $"Thay đổi {label} không đạt kiểm tra",
                    $"Lý do: {reason} Vui lòng sửa lại {label} trong hồ sơ trung tâm.", "/center/profile", ct);
            }
        }
        catch (Exception)
        {
            // thông báo là phụ
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
