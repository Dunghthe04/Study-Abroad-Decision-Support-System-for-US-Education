using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>Truy cập gói cho màn quản trị (UC-79). Chỉ đụng bảng subscription_plans khi ghi; kỳ và đơn chỉ được đếm.</summary>
public interface IPlanAdminRepository
{
    /// <summary>Mọi gói kèm số lần dùng, theo DisplayOrder rồi Name, không theo dõi.</summary>
    Task<IReadOnlyList<PlanWithUsage>> ListAsync(CancellationToken ct = default);

    /// <summary>Gói theo id (được theo dõi để sửa rồi SaveAsync) kèm số lần dùng; null nếu không có.</summary>
    Task<PlanWithUsage?> FindAsync(Guid id, CancellationToken ct = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<bool> KindExistsAsync(string kind, CancellationToken ct = default);

    /// <summary>Thêm và lưu.</summary>
    Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default);

    /// <summary>Lưu các sửa đổi trên gói đã lấy bằng FindAsync.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Xóa và lưu.</summary>
    Task RemoveAsync(SubscriptionPlan plan, CancellationToken ct = default);
}
