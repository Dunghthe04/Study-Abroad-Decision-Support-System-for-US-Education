using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>Truy vấn gói, kỳ, nhóm gia đình và token đã dùng. Kỳ, nhóm, thành viên trả về được theo dõi để sửa rồi lưu bằng SaveStepsAsync.</summary>
public interface IBillingRepository
{
    /// <summary>Mọi gói (cả gói ngừng bán), không theo dõi.</summary>
    Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default);

    /// <summary>Các kỳ active (chưa kết thúc, kể cả quá hạn mà chưa ai kết thúc) của một chủ ở một loại gói. Đang theo dõi.</summary>
    Task<List<UserSubscription>> GetActiveCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default);

    /// <summary>Nhóm active mà người này làm chủ.</summary>
    Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default);

    /// <summary>Nhóm active mà người này là thành viên active.</summary>
    Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Lời mời đang chờ và thành viên active của nhóm.</summary>
    Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default);

    /// <summary>Token tính phí (Claude) của mọi lượt chat đóng dấu kỳ này, mọi người dùng cộng lại (tùy chọn lọc theo cửa sổ [fromUtc, toUtc)).</summary>
    Task<long> SumCycleTokensAsync(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);

    /// <summary>Token tính phí của các lượt chat dùng hạn mức Free (không kỳ, có plan_code) từ fromUtc.</summary>
    Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default);

    Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Mã đơn đang chờ thanh toán của người dùng (tối đa 1).</summary>
    Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chạy từng bước sửa thực thể đang theo dõi, lưu sau mỗi bước, trong một transaction (dùng transaction đang có nếu có).</summary>
    Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default);

    /// <summary>
    /// Kết thúc kỳ hết hạn / kết thúc nhóm của một chủ ở một loại gói, an toàn khi nhiều request chạm cùng lúc. Trong một transaction (dùng transaction đang có nếu có):
    /// bỏ theo dõi bản cũ của các kỳ đó, <c>SELECT ... FOR UPDATE</c> các dòng kỳ active (lockGroup thì thêm dòng nhóm active của chủ),
    /// rồi gọi <paramref name="plan"/> để đọc lại dữ liệu mới trên dòng đã khóa và trả các bước cần làm (rỗng = bên kia đã làm xong), lưu như <see cref="SaveStepsAsync"/>.
    /// Trả true khi đã lưu ít nhất một bước, tức chính lần gọi này đổi dữ liệu. Vướng xung đột đồng thời (xmin) khi tự mở transaction thì quay lui, bỏ theo dõi và trả false
    /// ("đã có người làm"); trong transaction của người gọi thì ném lên để người gọi thử lại.
    /// </summary>
    Task<bool> SaveLockedStepsAsync(Guid ownerUserId, string kind, bool lockGroup, Func<Task<IReadOnlyList<Action>>> plan, CancellationToken ct = default);
}

public static class BillingRepositoryExtensions
{
    /// <summary>Token tính phí đã dùng của kỳ trong cửa sổ hiện tại: cả kỳ nếu không cấp lại token giữa kỳ, ngược lại chỉ cửa sổ chứa nowUtc.</summary>
    public static Task<long> SumCurrentWindowTokensAsync(this IBillingRepository repository, UserSubscription cycle, DateTime nowUtc, CancellationToken ct = default)
    {
        if (!CycleRules.IsRecurring(cycle))
            return repository.SumCycleTokensAsync(cycle.Id, null, null, ct);
        var (windowStart, windowEnd, _) = CycleRules.CycleWindowUtc(cycle, nowUtc);
        return repository.SumCycleTokensAsync(cycle.Id, windowStart, windowEnd, ct);
    }
}
