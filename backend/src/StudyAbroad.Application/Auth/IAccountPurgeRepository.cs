namespace StudyAbroad.Application.Auth;

/// <summary>
/// [UC-80] Các bước xóa hẳn tài khoản đã quá hạn chờ xóa. Mỗi bước là một câu lệnh,
/// gọi bên trong transaction của <see cref="AccountPurgeService"/>.
/// </summary>
public interface IAccountPurgeRepository
{
    /// <summary>Id các tài khoản đang chờ xóa (status deleted) thuộc các vai trò cho phép, xóa mềm từ trước mốc deletedBefore.</summary>
    Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra lại điều kiện và khóa dòng user (SELECT ... FOR UPDATE) tới hết transaction,
    /// để khôi phục tài khoản chạy cùng lúc phải chờ, không bị xóa nhầm.
    /// </summary>
    Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default);

    /// <summary>Xóa dữ liệu thô cổng thanh toán (payload_json) của mọi giao dịch của người dùng. Trả về số giao dịch.</summary>
    Task<int> ClearPaymentPayloadsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Bài diễn đàn của người dùng: bài có người khác trả lời thì thay nội dung bằng dòng "đã bị xóa" (giữ trả lời của người khác),
    /// bài còn lại xóa hẳn.
    /// </summary>
    Task<ForumPurgeResult> PurgeForumPostsAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Xóa dòng users. Dữ liệu cá nhân xóa theo (cascade); giao dịch và báo cáo diễn đàn
    /// còn lại với user_id null (set null).
    /// </summary>
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Số bài xóa hẳn và số bài giữ khung (có người khác trả lời).</summary>
public record ForumPurgeResult(int Deleted, int Tombstoned);
