using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Orders;

public enum OrderInsertResult
{
    Added,
    /// <summary>Mã đơn đã có (trùng Unix ms): thử mã kế tiếp.</summary>
    CodeTaken,
    /// <summary>Người dùng đã có một đơn đang chờ (index một đơn chờ mỗi người).</summary>
    PendingExists,
}

/// <summary>
/// Đơn hàng và các thao tác ghi riêng của thanh toán. Kỳ, nhóm, thành viên đọc qua IBillingRepository (cùng DbContext, cùng transaction).
/// Thực thể trả từ Lock / Add* được theo dõi.
/// </summary>
public interface IOrderRepository
{
    /// <summary>Đơn theo mã, không theo dõi.</summary>
    Task<PaymentTransaction?> FindByCodeAsync(long orderCode, CancellationToken ct = default);

    /// <summary>Đơn của người dùng, mới nhất trước, không theo dõi.</summary>
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> ListByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Các kỳ theo id (không theo dõi) để hiện ngày bắt đầu / kết thúc trên đơn.</summary>
    Task<IReadOnlyDictionary<Guid, UserSubscription>> GetCyclesAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default);

    /// <summary>Thêm đơn mới và lưu ngay (không trong transaction ngoài: vi phạm index làm hỏng transaction).</summary>
    Task<OrderInsertResult> TryAddAsync(PaymentTransaction order, CancellationToken ct = default);

    /// <summary>
    /// SELECT ... FOR UPDATE theo mã đơn rồi nạp bản mới nhất, đang theo dõi (bỏ bản cũ trong bộ nhớ nếu có). Gọi trong transaction.
    /// </summary>
    Task<PaymentTransaction?> LockByCodeAsync(long orderCode, CancellationToken ct = default);

    /// <summary>Đánh dấu thêm kỳ mới; lưu ở bước kế của SaveStepsAsync.</summary>
    void AddCycle(UserSubscription cycle);

    void AddGroup(FamilyGroup group);

    /// <summary>Lưu mọi thay đổi đang theo dõi.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Bỏ mọi thực thể đang theo dõi để lần thử lại đọc dữ liệu mới và không ghi lại phần dang dở.</summary>
    void ClearTracking();

    /// <summary>Lỗi do vướng index duy nhất hoặc xung đột đồng thời: đáng thử lại một lần.</summary>
    bool IsConflict(Exception ex);
}
