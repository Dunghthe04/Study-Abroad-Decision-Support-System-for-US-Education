using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>Truy vấn gói, kỳ, nhóm gia đình và token đã dùng. Kỳ, nhóm, thành viên trả về được theo dõi để sửa rồi lưu bằng SaveStepsAsync.</summary>
public interface IBillingRepository
{
    /// <summary>Mọi gói (cả gói ngừng bán), không theo dõi.</summary>
    Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default);

    /// <summary>Các kỳ queued và active của một làn (chủ, loại).</summary>
    Task<List<UserSubscription>> GetOpenCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default);

    /// <summary>Nhóm active mà người này làm chủ.</summary>
    Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default);

    /// <summary>Nhóm active mà người này là thành viên active.</summary>
    Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Lời mời đang chờ và thành viên active của nhóm.</summary>
    Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default);

    /// <summary>Token tính phí (Claude) của mọi lượt chat đóng dấu kỳ này, mọi người dùng cộng lại.</summary>
    Task<long> SumCycleTokensAsync(Guid cycleId, CancellationToken ct = default);

    /// <summary>Token tính phí của các lượt chat dùng hạn mức Free (không kỳ, có plan_code) từ fromUtc.</summary>
    Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default);

    Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Mã đơn đang chờ thanh toán của người dùng (tối đa 1).</summary>
    Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Chạy từng bước sửa thực thể đang theo dõi, lưu sau mỗi bước, trong một transaction (dùng transaction đang có nếu có).
    /// Lưu theo thứ tự để index duy nhất một-active-mỗi-làn không vấp: kết thúc kỳ cũ trước, kích hoạt kỳ mới sau.
    /// </summary>
    Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default);
}
