using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-114] Thông tin rút gọn của trung tâm để Moderator đối chiếu thay đổi.</summary>
public record CenterChangeCenterInfo(
    Guid Id,
    string Name,
    Guid? OwnerUserId,
    string VerificationStatus,
    bool IsActive);

/// <summary>
/// [UC-114] Một thay đổi website / fanpage do trung tâm tự sửa (UC-92) chờ Moderator kiểm tra.
/// Kèm trạng thái xác minh và ẩn/hiện của trung tâm để Moderator chọn thu hồi dấu xác minh (UC-59) hoặc ẩn trung tâm (UC-72).
/// </summary>
public record CenterProfileChangeDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    string CenterVerificationStatus,
    bool CenterIsActive,
    string Field,
    string? OldValue,
    string? NewValue,
    string Status,
    DateTime CreatedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? ReviewReason);

/// <summary>
/// [UC-114] Moderator chọn Đạt: gửi kèm giá trị mới mà họ đang xem. Nếu trung tâm đã sửa tiếp sau đó thì từ chối,
/// để không ghi nhận Đạt cho giá trị chưa ai kiểm tra.
/// </summary>
public record PassCenterProfileChangeRequest(string? NewValue);

/// <summary>[UC-114] Moderator chọn Không đạt: lý do bắt buộc (tối đa 500 ký tự) và giá trị mới đang xem (như Đạt).</summary>
public record FailCenterProfileChangeRequest(string? Reason, string? NewValue = null);

/// <summary>[UC-114] Kết quả thao tác duyệt: NotFound khi không có thay đổi, Error khi vi phạm quy tắc.</summary>
public record CenterProfileChangeReviewResult(CenterProfileChangeDto? Change, string? Error = null, bool NotFound = false)
{
    public bool Success => Change is not null && Error is null && !NotFound;
    public static CenterProfileChangeReviewResult Ok(CenterProfileChangeDto change) => new(change);
    public static CenterProfileChangeReviewResult Fail(string error) => new(null, error);
    public static CenterProfileChangeReviewResult Missing() => new(null, "Không tìm thấy thay đổi cần kiểm tra.", true);
}

/// <summary>[UC-114] Moderator kiểm tra thay đổi thông tin trung tâm (website, fanpage) do trung tâm tự sửa.</summary>
public interface ICenterProfileChangeReviewService
{
    Task<PagedResult<CenterProfileChangeDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    /// <param name="reviewedNewValue">Giá trị mới Moderator đã xem; khác giá trị hiện tại thì từ chối.</param>
    Task<CenterProfileChangeReviewResult> PassAsync(Guid changeId, string? reviewedNewValue, Guid moderatorUserId, CancellationToken ct = default);
    Task<CenterProfileChangeReviewResult> FailAsync(Guid changeId, string? reason, string? reviewedNewValue, Guid moderatorUserId, CancellationToken ct = default);
}

public class CenterProfileChangeReviewService(
    ICenterProfileChangeRepository changes,
    IAuditLogRepository auditLogs) : ICenterProfileChangeReviewService
{
    public const int MaxReasonLength = 500;

    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        CenterProfileChangeStatuses.Pending, CenterProfileChangeStatuses.Passed, CenterProfileChangeStatuses.Failed,
        CenterProfileChangeStatuses.Withdrawn,
    };

    private const string StaleMessage =
        "Trung tâm vừa cập nhật lại giá trị này sau khi bạn mở danh sách. Vui lòng tải lại và kiểm tra giá trị mới.";

    // Trống và null coi như nhau (trung tâm xóa website thì NewValue là null, giao diện gửi chuỗi rỗng)
    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [CenterProfileChangeFields.Website] = "Website",
        [CenterProfileChangeFields.Fanpage] = "Fanpage",
    };

    /// <summary>Danh sách theo trạng thái: mặc định pending (chờ kiểm tra), "all" là tất cả.</summary>
    public async Task<PagedResult<CenterProfileChangeDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        string? filter = normalized switch
        {
            null or "" => CenterProfileChangeStatuses.Pending,
            "all" => null,
            _ when Statuses.Contains(normalized) => normalized,
            _ => throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận 'pending', 'passed', 'failed', 'withdrawn' hoặc 'all'."),
        };

        var result = await changes.SearchAsync(filter, page, pageSize, ct);
        var centers = await LoadCentersAsync(result.Items.Select(x => x.StudyCenterId), ct);
        var items = result.Items.Select(x => ToDto(x, centers.GetValueOrDefault(x.StudyCenterId))).ToList();
        return new PagedResult<CenterProfileChangeDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>Đạt: thay đổi giữ nguyên, đóng mục kiểm tra và ghi nhật ký.</summary>
    public async Task<CenterProfileChangeReviewResult> PassAsync(Guid changeId, string? reviewedNewValue, Guid moderatorUserId, CancellationToken ct = default)
    {
        var change = await changes.GetByIdAsync(changeId, ct);
        if (change is null) return CenterProfileChangeReviewResult.Missing();
        if (change.Status != CenterProfileChangeStatuses.Pending)
            return CenterProfileChangeReviewResult.Fail("Thay đổi này đã được kiểm tra.");
        if (Normalize(change.NewValue) != Normalize(reviewedNewValue))
            return CenterProfileChangeReviewResult.Fail(StaleMessage);

        Review(change, CenterProfileChangeStatuses.Passed, moderatorUserId, null);
        await changes.UpdateAsync(change, ct);
        await AuditAsync(change, "center_change.pass", moderatorUserId, null, ct);

        var center = (await LoadCentersAsync([change.StudyCenterId], ct)).GetValueOrDefault(change.StudyCenterId);
        return CenterProfileChangeReviewResult.Ok(ToDto(change, center));
    }

    /// <summary>
    /// Không đạt: lý do bắt buộc, trung tâm nhận thông báo để sửa lại, ghi nhật ký.
    /// Nếu vi phạm, Moderator dùng tiếp thao tác thu hồi dấu xác minh (UC-59) hoặc ẩn trung tâm (UC-72) có sẵn.
    /// </summary>
    public async Task<CenterProfileChangeReviewResult> FailAsync(Guid changeId, string? reason, string? reviewedNewValue, Guid moderatorUserId, CancellationToken ct = default)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return CenterProfileChangeReviewResult.Fail("Vui lòng nhập lý do Không đạt.");
        if (trimmed.Length > MaxReasonLength)
            return CenterProfileChangeReviewResult.Fail($"Lý do tối đa {MaxReasonLength} ký tự.");

        var change = await changes.GetByIdAsync(changeId, ct);
        if (change is null) return CenterProfileChangeReviewResult.Missing();
        if (change.Status != CenterProfileChangeStatuses.Pending)
            return CenterProfileChangeReviewResult.Fail("Thay đổi này đã được kiểm tra.");
        if (Normalize(change.NewValue) != Normalize(reviewedNewValue))
            return CenterProfileChangeReviewResult.Fail(StaleMessage);

        Review(change, CenterProfileChangeStatuses.Failed, moderatorUserId, trimmed);
        await changes.UpdateAsync(change, ct);
        await AuditAsync(change, "center_change.fail", moderatorUserId, trimmed, ct);

        var center = (await LoadCentersAsync([change.StudyCenterId], ct)).GetValueOrDefault(change.StudyCenterId);
        await NotifyCenterAsync(change, center, trimmed, ct);
        return CenterProfileChangeReviewResult.Ok(ToDto(change, center));
    }

    private static void Review(CenterProfileChange change, string status, Guid moderatorUserId, string? reason)
    {
        change.Status = status;
        change.ReviewedByUserId = moderatorUserId;
        change.ReviewedAt = DateTime.UtcNow;
        change.ReviewReason = reason;
    }

    private Task AuditAsync(CenterProfileChange change, string action, Guid moderatorUserId, string? reason, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = moderatorUserId,
            Action = action,
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

    // Thông báo là phụ: lỗi ghi thông báo không được làm hỏng kết quả kiểm tra đã lưu
    private async Task NotifyCenterAsync(CenterProfileChange change, CenterChangeCenterInfo? center, string reason, CancellationToken ct)
    {
        if (center?.OwnerUserId is not { } ownerId) return;
        var label = FieldLabels.GetValueOrDefault(change.Field, change.Field);
        try
        {
            await changes.AddNotificationAsync(new Notification
            {
                UserId = ownerId,
                Type = "center_change_failed",
                Title = $"Thay đổi {label} chưa đạt kiểm tra",
                Body = $"{label} mới \"{change.NewValue}\" chưa đạt kiểm tra. Lý do: {reason}. Vui lòng cập nhật lại trong hồ sơ trung tâm.",
                LinkUrl = "/center/profile",
            }, ct);
        }
        catch (Exception)
        {
            // đã lưu kết quả kiểm tra; thông báo là phụ
        }
    }

    private async Task<Dictionary<Guid, CenterChangeCenterInfo>> LoadCentersAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];
        var infos = await changes.GetCenterInfosAsync(distinct, ct);
        return infos.ToDictionary(x => x.Id);
    }

    private static CenterProfileChangeDto ToDto(CenterProfileChange x, CenterChangeCenterInfo? center) =>
        new(
            x.Id,
            x.StudyCenterId,
            center?.Name ?? "Trung tâm không còn tồn tại",
            center?.VerificationStatus ?? string.Empty,
            center?.IsActive ?? false,
            x.Field,
            x.OldValue,
            x.NewValue,
            x.Status,
            x.CreatedAt,
            x.ReviewedByUserId,
            x.ReviewedAt,
            x.ReviewReason);
}
