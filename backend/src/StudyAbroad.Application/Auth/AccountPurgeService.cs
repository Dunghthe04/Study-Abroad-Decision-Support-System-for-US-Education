using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Auth;

public interface IAccountPurgeService
{
    /// <summary>[UC-80] Xóa hẳn mọi tài khoản chờ xóa đã quá thời hạn. Một tài khoản lỗi không chặn các tài khoản khác.</summary>
    Task<AccountPurgeSummary> PurgeDueAccountsAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// [UC-75] Xóa hẳn dữ liệu giữ lại của tài khoản đã xóa khi tới hạn: tin nhắn với trung tâm sau 90 ngày (hoặc khi báo cáo
    /// đang mở được xử lý xong), đơn hàng sau 10 năm (họ tên/email người mua dọn ở <see cref="PurgeDueAccountsAsync"/>).
    /// </summary>
    Task<RetainedDataPurgeSummary> PurgeExpiredRetainedDataAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>Số tin nhắn, yêu cầu liên hệ và đơn hàng đã xóa hẳn trong một lượt dọn.</summary>
public record RetainedDataPurgeSummary(int Messages, int Leads, int Orders);

/// <summary>
/// Kết quả một lượt chạy: số tài khoản đã xóa, số bỏ qua (vừa khôi phục), các tài khoản lỗi,
/// và các tài khoản đã xóa trong DB nhưng xóa tệp trên ổ đĩa lỗi (cần dọn tay).
/// </summary>
public record AccountPurgeSummary(
    int Purged,
    int Skipped,
    IReadOnlyList<AccountPurgeFailure> Failures,
    IReadOnlyList<AccountPurgeFailure> FileCleanupFailures);

public record AccountPurgeFailure(Guid UserId, Exception Error);

/// <summary>
/// [UC-80] Xóa hẳn tài khoản sau thời hạn chờ (mặc định 30 ngày). Mỗi tài khoản một transaction:
/// kiểm tra lại điều kiện → xóa payload thanh toán → giữ họ tên/email người mua (UC-75 lớp 2) → xóa trắng liên hệ trên yêu cầu
/// còn giữ (lớp 1b) → ẩn danh mức dùng AI (lớp 3) → xử lý bài diễn đàn → xóa user → ghi nhật ký chỉ có id.
/// Tệp học bạ, tệp nháp xóa SAU khi DB đã lưu: DB hoàn tác được còn tệp đã xóa thì không.
/// Chỉ xử lý vai trò được tự xóa (học sinh, phụ huynh).
/// </summary>
public class AccountPurgeService(
    IAccountPurgeRepository purge,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IProfileFileStorageService fileStorage,
    AccountDeletionOptions? options = null,
    IBillingAccountHooks? billingHooks = null) : IAccountPurgeService
{
    public const string AuditAction = "user.purge_account";
    public const int AccountingRetentionYears = 10;

    private readonly AccountDeletionOptions _options = options ?? new AccountDeletionOptions();

    /// <summary>
    /// [UC-75] Mốc hết hạn lưu thông tin người mua: 10 năm kể từ khi kết thúc năm tài chính (theo giờ Việt Nam, 31/12) của giao dịch.
    /// Ví dụ giao dịch trong năm tài chính 2026 (VN) → kết thúc năm tài chính là 01/01/2027 00:00 +07:00 → giữ tới 01/01/2037 00:00 +07:00 (31/12/2036 17:00 UTC).
    /// </summary>
    public static DateTime ComputeRetentionExpiryUtc(DateTime orderCreatedAtUtc)
    {
        var vnOffset = TimeSpan.FromHours(7);
        var vnTime = DateTime.SpecifyKind(orderCreatedAtUtc, DateTimeKind.Utc) + vnOffset;
        var endOfFiscalYearVn = new DateTime(vnTime.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return endOfFiscalYearVn.AddYears(AccountingRetentionYears) - vnOffset;
    }

    public async Task<AccountPurgeSummary> PurgeDueAccountsAsync(DateTime now, CancellationToken ct = default)
    {
        var deletedBefore = now - _options.RetentionPeriod;
        var roles = UserRoles.SelfDelete.ToList();
        var dueIds = await purge.GetDueUserIdsAsync(deletedBefore, roles, ct);

        var purged = 0;
        var skipped = 0;
        List<AccountPurgeFailure> failures = [];
        List<AccountPurgeFailure> fileFailures = [];

        foreach (var userId in dueIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var done = false;
                Func<CancellationToken, Task>? billingAfterCommit = null;
                await unitOfWork.ExecuteInTransactionAsync(async txCt =>
                {
                    // Người dùng có thể vừa khôi phục sau lúc lấy danh sách: kiểm tra lại và khóa dòng
                    if (!await purge.LockIfStillDueAsync(userId, deletedBefore, roles, txCt))
                        return;

                    // [UC-75, UC-124] Chụp họ tên + email vào bảng lưu kế toán 10 năm trước khi xóa dòng user (UserId sẽ bị SetNull)
                    await purge.SnapshotBuyerForOrdersAsync(userId, now, txCt);
                    await purge.ClearPaymentPayloadsAsync(userId, txCt);
                    // [UC-75] Lớp 1b và 3: tin nhắn, đánh giá còn lại qua set null; liên hệ trên yêu cầu và mức dùng AI bỏ dấu người dùng
                    await purge.ScrubRetainedLeadsAsync(userId, txCt);
                    await purge.AnonymizeAiUsageAsync(userId, txCt);
                    await purge.PurgeForumPostsAsync(userId, now, txCt);
                    // Nhóm Gia đình và kỳ của chủ gói kết thúc trước khi dòng user mất (cascade xóa nốt)
                    if (billingHooks is not null)
                        billingAfterCommit = await billingHooks.OnBeforePurgeAsync(userId, now, txCt);
                    await purge.DeleteUserAsync(userId, txCt);

                    // Nhật ký chỉ giữ id, không giữ email hay tên đã xóa
                    await auditLogs.AddAsync(new AuditLog
                    {
                        ActorUserId = null,
                        Action = AuditAction,
                        EntityType = "user",
                        EntityId = userId.ToString(),
                    }, txCt);
                    done = true;
                }, ct);

                if (!done)
                {
                    skipped++;
                    continue;
                }

                purged++;
                if (billingAfterCommit is not null)
                    await billingAfterCommit(ct);   // báo thành viên: không ném lỗi, user đã mất khỏi DB
                try
                {
                    await fileStorage.DeleteAllUserFilesAsync(userId, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // User đã mất khỏi DB, không thử lại theo user được nữa: báo để dọn tay
                    fileFailures.Add(new AccountPurgeFailure(userId, ex));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new AccountPurgeFailure(userId, ex));
            }
        }

        // [UC-75] Dọn các bản ghi thông tin người mua đã quá hạn lưu 10 năm
        await purge.PurgeExpiredBuyerRetentionsAsync(now, ct);

        return new AccountPurgeSummary(purged, skipped, failures, fileFailures);
    }

    public async Task<RetainedDataPurgeSummary> PurgeExpiredRetainedDataAsync(DateTime now, CancellationToken ct = default)
    {
        RetainedLeadPurgeResult leads = new(0, 0);
        var orders = 0;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            leads = await purge.PurgeExpiredLeadMessagesAsync(now, txCt);
            orders = await purge.PurgeExpiredOrdersAsync(now, txCt);
        }, ct);
        return new RetainedDataPurgeSummary(leads.Messages, leads.Leads, orders);
    }
}
