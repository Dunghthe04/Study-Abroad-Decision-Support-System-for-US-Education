using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-80] Các câu lệnh xóa hẳn tài khoản, chạy chung transaction do AccountPurgeService mở.</summary>
public class AccountPurgeRepository(AppDbContext db) : IAccountPurgeRepository
{
    public const string RemovedPostContent = "[Nội dung đã bị xóa]";

    public Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default) =>
        db.Users.AsNoTracking()
            .Where(u => u.Status == UserStatuses.Deleted
                && u.DeletedAt != null && u.DeletedAt <= deletedBefore
                && !u.IsLegalHold
                && roles.Contains(u.Role))
            .OrderBy(u => u.DeletedAt)
            .Select(u => u.Id)
            .ToListAsync(ct);

    // NO KEY UPDATE, không phải UPDATE: vẫn loại trừ purge khác và mọi lần sửa dòng user, nhưng tương thích với KEY SHARE mà khóa ngoại tới users
    // lấy khi thanh toán thêm kỳ (đơn → kỳ). FOR UPDATE ở đây chờ vòng với thanh toán: purge giữ dòng user rồi ghi đơn, thanh toán giữ dòng đơn rồi chèn kỳ cần KEY SHARE dòng user
    public async Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var roleArray = roles.ToArray();
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM app.users
            WHERE id = {userId}
              AND status = {UserStatuses.Deleted}
              AND deleted_at IS NOT NULL AND deleted_at <= {deletedBefore}
              AND is_legal_hold = FALSE
              AND role = ANY({roleArray})
            FOR NO KEY UPDATE
            """).ToListAsync(ct);
        return ids.Count > 0;
    }

    public Task<int> ClearPaymentPayloadsAsync(Guid userId, CancellationToken ct = default) =>
        db.PaymentTransactions
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PayloadJson, (string?)null), ct);

    public async Task<ForumPurgeResult> PurgeForumPostsAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        // Diễn đàn hai tầng: bài gốc và trả lời trỏ thẳng vào bài gốc.
        // Bài có trả lời của người khác (kể cả AI) → giữ khung để trả lời đó không mất theo cascade
        var hasOthersReply = db.ForumPosts.Where(p => p.UserId == userId
            && db.ForumPosts.Any(r => r.ParentPostId == p.Id && (r.UserId == null || r.UserId != userId)));

        var tombstoned = await hasOthersReply.ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Title, (string?)null)
            .SetProperty(p => p.Content, RemovedPostContent)
            .SetProperty(p => p.Status, "removed")
            .SetProperty(p => p.UpdatedAt, now), ct);

        // Còn lại (không ai khác trả lời) xóa hẳn; trả lời của chính người này dưới bài đó cũng là bài của họ
        var deleted = await db.ForumPosts
            .Where(p => p.UserId == userId
                && !db.ForumPosts.Any(r => r.ParentPostId == p.Id && (r.UserId == null || r.UserId != userId)))
            .ExecuteDeleteAsync(ct);

        return new ForumPurgeResult(deleted, tombstoned);
    }

    /// <summary>Đơn đã có tiền đi qua (paid, mismatch): chỉ những đơn này buộc phải lưu thông tin người mua 10 năm.</summary>
    public static readonly Expression<Func<PaymentTransaction, bool>> MoneyMoved =
        p => p.Status == OrderStatuses.Paid || p.Status == OrderStatuses.Mismatch;

    public async Task<string?> SnapshotBuyerForOrdersAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        // Chỉ đơn đã có tiền đi qua (paid, mismatch) mới phải lưu thông tin người mua; đơn chờ / hủy / hết hạn thì không
        var latestOrderCreatedAt = await db.PaymentTransactions
            .Where(p => p.UserId == userId).Where(MoneyMoved)
            .Select(p => (DateTime?)p.CreatedAt)
            .MaxAsync(ct);
        if (latestOrderCreatedAt is null)
            return null;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(ct);
        if (user is null)
            return null;

        var buyerCode = $"DEL-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
        var retention = new DeletedBuyerRetention
        {
            BuyerCode = buyerCode,
            FullName = user.FullName,
            Email = user.Email,
            RetainUntilUtc = AccountPurgeService.ComputeRetentionExpiryUtc(latestOrderCreatedAt.Value),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.DeletedBuyerRetentions.Add(retention);
        await db.SaveChangesAsync(ct);

        await db.PaymentTransactions
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedBuyerRetentionId, retention.Id), ct);
        return buyerCode;
    }

    public Task<int> PurgeExpiredBuyerRetentionsAsync(DateTime now, CancellationToken ct = default) =>
        db.DeletedBuyerRetentions
            .Where(r => r.RetainUntilUtc <= now)
            .ExecuteDeleteAsync(ct);

    public Task DeleteUserAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
}
