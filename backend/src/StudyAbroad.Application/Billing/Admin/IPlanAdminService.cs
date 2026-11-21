namespace StudyAbroad.Application.Billing.Admin;

/// <summary>[UC-79] Admin tạo, sửa, ngừng bán và xóa gói. Lỗi quy tắc ném <see cref="BillingRuleException"/>.</summary>
public interface IPlanAdminService
{
    Task<IReadOnlyList<AdminPlanDto>> ListAsync(CancellationToken ct = default);
    Task<AdminPlanDto> CreateAsync(Guid adminUserId, SavePlanRequest request, CancellationToken ct = default);
    Task<AdminPlanDto> UpdateAsync(Guid adminUserId, Guid planId, SavePlanRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid adminUserId, Guid planId, CancellationToken ct = default);
}
