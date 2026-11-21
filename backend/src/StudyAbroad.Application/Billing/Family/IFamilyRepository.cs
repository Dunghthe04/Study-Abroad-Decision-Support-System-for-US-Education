using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Family;

/// <summary>
/// Truy vấn và ghi lời mời / thành viên gói Gia đình. Nhóm, kỳ và danh sách thành viên mở lấy từ <see cref="IBillingRepository"/>.
/// Dòng trả về "được theo dõi" sửa rồi lưu bằng <see cref="SaveAsync"/>; mọi thao tác đổi chỗ ngồi chạy trong transaction sau <see cref="LockActiveGroupAsync"/>.
/// </summary>
public interface IFamilyRepository
{
    /// <summary>
    /// Khóa dòng nhóm (<c>SELECT ... FOR UPDATE</c>) để các thao tác đổi chỗ ngồi của cùng một nhóm chạy lần lượt. Chỉ có tác dụng trong transaction.
    /// False = nhóm không còn tồn tại hoặc không còn active.
    /// </summary>
    Task<bool> LockActiveGroupAsync(Guid groupId, CancellationToken ct = default);

    Task<FamilyMember?> FindMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Theo hash token (hex, đã băm bằng <see cref="FamilyInviteToken.Hash"/>).</summary>
    Task<FamilyMember?> FindMemberByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Dòng thành viên active của người này trong nhóm.</summary>
    Task<FamilyMember?> FindActiveMembershipAsync(Guid groupId, Guid userId, CancellationToken ct = default);

    /// <summary>Không theo dõi (chỉ để đọc chủ nhóm).</summary>
    Task<FamilyGroup?> FindGroupAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Số chỗ đang giữ của một loại, đếm mới từ DB (sau khi khóa nhóm): thành viên active và lời mời còn hạn (pending, hết hạn sau nowUtc).
    /// excludeMemberId: bỏ một dòng ra khỏi phép đếm (lời mời đang chấp nhận).
    /// </summary>
    Task<int> CountSeatsAsync(Guid groupId, string seatRole, Guid? excludeMemberId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Email đã có lời mời pending hoặc đã là thành viên active trong nhóm.</summary>
    Task<bool> HasOpenInviteForEmailAsync(Guid groupId, string email, CancellationToken ct = default);

    /// <summary>Người này có đơn pending cho gói loại family.</summary>
    Task<bool> HasPendingFamilyOrderAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Token tính phí mỗi người đã dùng trong một kỳ (để chia phần trăm, tùy chọn lọc theo cửa sổ [fromUtc, toUtc)). Chỉ dùng nội bộ, không bao giờ ra khỏi server.</summary>
    Task<Dictionary<Guid, long>> SumTokensByUserAsync(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);

    Task AddMemberAsync(FamilyMember member, CancellationToken ct = default);

    /// <summary>
    /// Lưu thay đổi. Vi phạm index duy nhất đổi thành <see cref="BillingRuleException"/> (already_in_family, invite_exists);
    /// dòng bị request khác sửa trước thì ném <see cref="Common.ConcurrencyConflictException"/>.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Lời mời pending đã quá hạn → expired, xóa hash. groupId null = mọi nhóm. Trả số dòng.</summary>
    Task<int> ExpireDueInvitesAsync(DateTime nowUtc, Guid? groupId = null, CancellationToken ct = default);
}
