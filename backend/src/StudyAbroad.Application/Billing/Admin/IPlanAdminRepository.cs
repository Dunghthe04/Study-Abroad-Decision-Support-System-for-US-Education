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

    /// <summary>Thêm và lưu. Ném <see cref="PlanConflictException"/> (CodeTaken, FreePlanExists) khi hai admin tạo cùng lúc vấp index duy nhất.</summary>
    Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default);

    /// <summary>Lưu các sửa đổi trên gói đã lấy bằng FindAsync.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Xóa và lưu. Ném <see cref="PlanConflictException"/> (InUse) khi một đơn hoặc kỳ vừa tham chiếu gói (khóa ngoại Restrict).</summary>
    Task RemoveAsync(SubscriptionPlan plan, CancellationToken ct = default);
}

public enum PlanConflict
{
    /// <summary>Trùng mã gói (index duy nhất trên code).</summary>
    CodeTaken,
    /// <summary>Đã có gói Free (index duy nhất một gói free).</summary>
    FreePlanExists,
    /// <summary>Gói đang được đơn hoặc kỳ tham chiếu.</summary>
    InUse,
}

/// <summary>Repository đổi vi phạm ràng buộc của Postgres (23505, 23503) sang ngoại lệ này để tầng Application không phụ thuộc Npgsql.</summary>
public sealed class PlanConflictException(PlanConflict conflict, Exception? inner = null)
    : Exception($"Gói vướng ràng buộc dữ liệu: {conflict}", inner)
{
    public PlanConflict Conflict { get; } = conflict;
}
