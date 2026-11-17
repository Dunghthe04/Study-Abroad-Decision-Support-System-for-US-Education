using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Auth;

public interface IAccountPurgeService
{
    /// <summary>[UC-80] Xóa hẳn mọi tài khoản chờ xóa đã quá thời hạn. Một tài khoản lỗi không chặn các tài khoản khác.</summary>
    Task<AccountPurgeSummary> PurgeDueAccountsAsync(DateTime now, CancellationToken ct = default);
}

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
/// kiểm tra lại điều kiện → xóa payload thanh toán → xử lý bài diễn đàn → xóa user → ghi nhật ký chỉ có id.
/// Tệp học bạ, tệp nháp xóa SAU khi DB đã lưu: DB hoàn tác được còn tệp đã xóa thì không.
/// Chỉ xử lý vai trò được tự xóa (học sinh, phụ huynh).
/// </summary>
public class AccountPurgeService(
    IAccountPurgeRepository purge,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IProfileFileStorageService fileStorage,
    AccountDeletionOptions? options = null) : IAccountPurgeService
{
    public const string AuditAction = "user.purge_account";

    private readonly AccountDeletionOptions _options = options ?? new AccountDeletionOptions();

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
                await unitOfWork.ExecuteInTransactionAsync(async txCt =>
                {
                    // Người dùng có thể vừa khôi phục sau lúc lấy danh sách: kiểm tra lại và khóa dòng
                    if (!await purge.LockIfStillDueAsync(userId, deletedBefore, roles, txCt))
                        return;

                    await purge.ClearPaymentPayloadsAsync(userId, txCt);
                    await purge.PurgeForumPostsAsync(userId, now, txCt);
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

        return new AccountPurgeSummary(purged, skipped, failures, fileFailures);
    }
}
