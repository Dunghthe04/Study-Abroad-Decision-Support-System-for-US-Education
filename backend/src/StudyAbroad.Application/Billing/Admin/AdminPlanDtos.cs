using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Gói trên màn quản trị (UC-79): các trường của PlanDto cộng id, hạn mức token thô, trạng thái bán và số lần được dùng.</summary>
/// <param name="ActiveCycles">Số kỳ của gói đang active hoặc queued.</param>
/// <param name="OrderCount">Số đơn hàng của gói, mọi trạng thái.</param>
public sealed record AdminPlanDto(
    Guid Id, string Code, string Name, string? Description, string Kind, long PriceVnd, int? DurationDays,
    int? QuotaPeriodDays, decimal? QuotaMultiplier, PlanSeatsDto? Seats, long TokenQuota, bool IsActive, int DisplayOrder, int ActiveCycles, int OrderCount)
{
    public static AdminPlanDto From(SubscriptionPlan plan, PlanUsage usage, SubscriptionPlan? freePlan = null) => new(
        plan.Id, plan.Code, plan.Name, plan.Description, plan.Kind, (long)plan.PriceVnd, plan.DurationDays,
        plan.QuotaPeriodDays, CycleRules.QuotaMultiplier(plan, freePlan),
        plan.Kind == PlanKinds.Family ? new PlanSeatsDto(FamilySeats.MaxStudents, FamilySeats.MaxParents) : null,
        plan.TokenQuota, plan.IsActive, plan.DisplayOrder, usage.ActiveCycles, usage.Orders);
}

/// <summary>
/// Thân POST / PUT admin/plans. POST gửi code và kind; PUT bỏ qua code, kind nếu gửi phải trùng kind hiện tại.
/// Mọi trường chữ để nullable để thiếu trường trả lỗi { code, message } thay vì lỗi model binding.
/// </summary>
public sealed record SavePlanRequest(
    string? Code, string? Kind, string? Name, string? Description, long PriceVnd, int? DurationDays,
    int? QuotaPeriodDays, long TokenQuota, bool IsActive, int DisplayOrder);

/// <summary>Số kỳ và đơn tham chiếu một gói. <paramref name="Cycles"/> tính mọi trạng thái (chặn xóa), <paramref name="ActiveCycles"/> chỉ active + queued.</summary>
public sealed record PlanUsage(int ActiveCycles, int Cycles, int Orders);

public sealed record PlanWithUsage(SubscriptionPlan Plan, PlanUsage Usage);
