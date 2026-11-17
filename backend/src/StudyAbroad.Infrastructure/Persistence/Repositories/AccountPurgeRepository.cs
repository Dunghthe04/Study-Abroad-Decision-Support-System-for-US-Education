using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-80] Các câu lệnh xóa hẳn tài khoản, chạy chung transaction do AccountPurgeService mở.</summary>
public class AccountPurgeRepository(AppDbContext db) : IAccountPurgeRepository
{
    public const string RemovedPostContent = "[Nội dung đã bị xóa]";

    public Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default) =>
        db.Users.AsNoTracking()
            .Where(u => u.Status == UserStatuses.Deleted
                && u.DeletedAt != null && u.DeletedAt <= deletedBefore
                && roles.Contains(u.Role))
            .OrderBy(u => u.DeletedAt)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var roleArray = roles.ToArray();
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM app.users
            WHERE id = {userId}
              AND status = {UserStatuses.Deleted}
              AND deleted_at IS NOT NULL AND deleted_at <= {deletedBefore}
              AND role = ANY({roleArray})
            FOR UPDATE
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

    public Task DeleteUserAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
}
