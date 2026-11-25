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
    /// [UC-75, UC-124] Nếu tài khoản có đơn hàng, chụp họ tên + email vào bảng lưu trữ kế toán 10 năm kèm mã người mua ngẫu nhiên
    /// và gắn vào các đơn hàng trước khi dòng users bị xóa. Trả về mã người mua đã tạo (null nếu không có đơn).
    /// </summary>
    Task<string?> SnapshotBuyerForOrdersAsync(Guid userId, DateTime now, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// [UC-75] Xóa hẳn các bản ghi lưu thông tin người mua đã quá hạn 10 năm sau năm tài chính. Trả số bản ghi đã xóa.
    /// </summary>
    Task<int> PurgeExpiredBuyerRetentionsAsync(DateTime now, CancellationToken ct = default) =>
        Task.FromResult(0);

    /// <summary>
    /// [UC-75 lớp 1b] Yêu cầu liên hệ của người dùng còn giữ tin tới mốc 90 ngày: xóa trắng số điện thoại, email và dấu vết đồng ý
    /// của phụ huynh. Tin nhắn và yêu cầu còn lại với người gửi null; <see cref="PurgeExpiredRetainedDataAsync"/> xóa hẳn khi tới hạn.
    /// </summary>
    Task ScrubRetainedLeadsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>[UC-75 lớp 3] Mức dùng AI chỉ giữ số tổng hợp: bỏ liên kết tới người dùng, tin chat và kỳ gói của họ.</summary>
    Task<int> AnonymizeAiUsageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Xóa dòng users. Dữ liệu cá nhân xóa theo (cascade); giao dịch, báo cáo diễn đàn, yêu cầu liên hệ, tin nhắn với trung tâm
    /// và đánh giá trung tâm còn lại với id người dùng null (set null, UC-75).
    /// </summary>
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);

    // ---- [UC-75] Dọn dữ liệu giữ lại của tài khoản đã xóa hẳn khi tới hạn: gọi mỗi lượt job, ngoài vòng từng tài khoản ----

    /// <summary>
    /// [UC-75 lớp 1b] Tin của người đã xóa hẳn (người gửi null) và mọi tin trong yêu cầu của người đã xóa hẳn (chủ null):
    /// xóa khi yêu cầu đã quá 90 ngày kể từ ngày gửi và tin không có báo cáo đang mở (UC-54). Yêu cầu không còn tin thì xóa luôn.
    /// </summary>
    Task<RetainedLeadPurgeResult> PurgeExpiredLeadMessagesAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// [UC-75 lớp 2] Đơn của tài khoản đã xóa hẳn quá 10 năm kể từ khi kết thúc năm tài chính có giao dịch thì xóa hẳn.
    /// Họ tên/email người mua hết hạn do <see cref="PurgeExpiredBuyerRetentionsAsync"/> xóa. Trả về số đơn đã xóa.
    /// </summary>
    Task<int> PurgeExpiredOrdersAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>Số tin và số yêu cầu liên hệ đã xóa hẳn.</summary>
public record RetainedLeadPurgeResult(int Messages, int Leads);

/// <summary>Số bài xóa hẳn và số bài giữ khung (có người khác trả lời).</summary>
public record ForumPurgeResult(int Deleted, int Tombstoned);
