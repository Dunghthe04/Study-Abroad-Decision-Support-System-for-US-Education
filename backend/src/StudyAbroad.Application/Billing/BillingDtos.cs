using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

// Phản hồi cho khách hàng chỉ có phần trăm, không bao giờ có số token (D4).

/// <summary>Một gói đang bán trên trang gói (GET /api/v1/plans).</summary>
public sealed record PlanDto(
    string Code, string Name, string? Description, string Kind, long PriceVnd, int? DurationDays, int? QuotaPeriodDays, decimal? QuotaMultiplier, PlanSeatsDto? Seats)
{
    public static PlanDto From(SubscriptionPlan plan, SubscriptionPlan? freePlan = null) => new(
        plan.Code, plan.Name, plan.Description, plan.Kind, (long)plan.PriceVnd, plan.DurationDays,
        plan.QuotaPeriodDays, CycleRules.QuotaMultiplier(plan, freePlan),
        plan.Kind == PlanKinds.Family ? new PlanSeatsDto(FamilySeats.MaxStudents, FamilySeats.MaxParents) : null);
}

/// <summary>Chỗ ngồi thêm của gói Gia đình (chủ gói không tính).</summary>
public sealed record PlanSeatsDto(int Students, int Parents);

/// <summary>Kỳ đã mua đang xếp hàng: bắt đầu khi kỳ hiện tại hết hạn hoặc dùng hết, tùy cái nào tới trước.</summary>
public sealed record QueuedCycleDto(string PlanName, DateTime? ExpectedStartsAt);

/// <param name="PlanCode">Luôn là chuỗi (frontend không nhận null); "" khi dòng gói của kỳ không còn.</param>
public sealed record PoolDto(
    string PlanCode, string PlanName, DateTime? StartsAt, DateTime? EndsAt,
    int UsedPercent, bool Exhausted, bool LowQuota, bool ExpiringSoon, QueuedCycleDto? Queued,
    int? QuotaPeriodDays = null, DateTimeOffset? QuotaResetsAt = null);

/// <summary>PoolDto kèm vai trò trong gói Gia đình (owner | member) và tên chủ gói.</summary>
public sealed record FamilyPoolDto(
    string PlanCode, string PlanName, DateTime? StartsAt, DateTime? EndsAt,
    int UsedPercent, bool Exhausted, bool LowQuota, bool ExpiringSoon, QueuedCycleDto? Queued,
    string Role, string OwnerName,
    int? QuotaPeriodDays = null, DateTimeOffset? QuotaResetsAt = null);

/// <summary>Hạn mức Free; làm mới 00:00 ngày 1 giờ Việt Nam hoặc mỗi QuotaPeriodDays ngày.</summary>
public sealed record FreePoolDto(int UsedPercent, DateTimeOffset ResetsAt, int? QuotaPeriodDays = null);

/// <summary>GET /api/v1/subscription. LowQuota / ExpiringSoon là của pool đang bị trừ.</summary>
public sealed record SubscriptionSummaryDto(
    string Tier, string ChargingPool, PoolDto? Personal, FamilyPoolDto? Family, FreePoolDto Free,
    long? PendingOrderCode, bool LowQuota, bool ExpiringSoon);

public static class AccountTiers
{
    public const string Free = "free";
    public const string Premium = "premium";
}

/// <summary>Pool trả cho lượt chat kế tiếp, quyết định trước khi gọi advisor.</summary>
/// <param name="Ungated">Nhân viên (admin, moderator, trung tâm): không chặn, không tính.</param>
/// <param name="CycleId">Kỳ đóng dấu lên ai_calls; null = hạn mức Free (hoặc nhân viên).</param>
/// <param name="PlanCode">Đóng dấu lên ai_calls.plan_code ("free" khi dùng hạn mức Free, null với nhân viên).</param>
/// <param name="PoolKind">personal | family | free</param>
/// <param name="AllowedIntents">Ý định được phép; null = tất cả.</param>
/// <param name="FreeExhausted">PoolKind free và hạn mức tháng đã dùng hết.</param>
public sealed record ChatCharge(
    bool Ungated,
    bool IsPremium,
    Guid? CycleId,
    string? PlanCode,
    string PoolKind,
    IReadOnlyList<string>? AllowedIntents,
    bool FreeExhausted,
    DateTime FreeResetsAtUtc);
