namespace StudyAbroad.Application.Billing;

/// <summary>Chủ nhóm và tên của thành viên vừa bị kết thúc tư cách (để báo chủ nhóm).</summary>
public sealed record EndedMembership(Guid OwnerUserId, string MemberName);

/// <summary>Tên chủ nhóm và các thành viên active của những nhóm vừa kết thúc (để báo từng người).</summary>
public sealed record EndedOwnedGroups(string OwnerName, IReadOnlyList<Guid> MemberUserIds);

/// <summary>
/// [UC-80] Các bước gói dịch vụ khi xóa tài khoản. Chạy trong transaction của AuthService (xóa mềm) hoặc AccountPurgeService (xóa hẳn),
/// mỗi bước là một câu lệnh ghi thẳng, không theo dõi thực thể.
/// </summary>
public interface IBillingAccountRepository
{
    /// <summary>
    /// Kết thúc tư cách thành viên active của người này (ended, member_deleted, xóa hash). Null = không phải thành viên active
    /// hoặc dòng vừa bị thao tác khác kết thúc trước.
    /// </summary>
    Task<EndedMembership?> EndActiveMembershipAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Khóa rồi hủy mọi đơn pending của người này (cancelled). Trả mã các đơn đã hủy; khóa dòng để webhook thanh toán chạy cùng lúc
    /// phải chờ, nên đơn vừa được trả tiền không bị hủy nhầm và không bị gọi hủy ở cổng.
    /// </summary>
    Task<IReadOnlyList<long>> CancelPendingOrdersAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Khóa rồi kết thúc mọi nhóm active mà người này làm chủ (ended, owner_purged) cùng các lời mời và thành viên còn mở
    /// (ended, owner_purged, xóa hash). Null = không làm chủ nhóm nào.
    /// </summary>
    Task<EndedOwnedGroups?> EndOwnedGroupsAsync(Guid ownerUserId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Kết thúc mọi kỳ queued và active của người này (ended, owner_purged). Trả số kỳ.</summary>
    Task<int> EndOpenCyclesAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);
}
