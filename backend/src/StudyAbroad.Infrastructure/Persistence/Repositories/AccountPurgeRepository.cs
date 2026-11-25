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

    // Tin nhắn và yêu cầu còn giữ để trung tâm đọc tới mốc 90 ngày; số điện thoại và dấu vết phụ huynh không cần cho việc đó
    public Task ScrubRetainedLeadsAsync(Guid userId, CancellationToken ct = default) =>
        db.Leads
            .Where(l => l.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ContactPhone, (string?)null)
                .SetProperty(l => l.ParentEmail, (string?)null)
                .SetProperty(l => l.ParentDecisionIp, (string?)null)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.ParentTokenExpiresAt, (DateTime?)null), ct);

    public Task<int> AnonymizeAiUsageAsync(Guid userId, CancellationToken ct = default) =>
        db.AiCalls
            .Where(a => a.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.UserId, (Guid?)null)
                .SetProperty(a => a.ChatMessageId, (Guid?)null)
                .SetProperty(a => a.UserSubscriptionId, (Guid?)null), ct);

    public Task DeleteUserAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);

    public async Task<RetainedLeadPurgeResult> PurgeExpiredLeadMessagesAsync(DateTime now, CancellationToken ct = default)
    {
        var cutoff = now - RetentionPeriods.LeadMessages;

        // Tin của người đã xóa hẳn, hoặc mọi tin trong yêu cầu của người đã xóa hẳn, ở yêu cầu đã quá 90 ngày.
        // Báo cáo đang mở (chưa gỡ, chưa xử lý) giữ tin tới khi Admin xử lý xong; bác bỏ báo cáo thì xóa thông tin báo cáo nên tin hết bị giữ
        var messages = await db.LeadMessages
            .Where(m => (m.SenderUserId == null || db.Leads.Any(l => l.Id == m.LeadId && l.UserId == null))
                && db.Leads.Any(l => l.Id == m.LeadId && l.CreatedAt <= cutoff)
                && !(m.ReportedAt != null && m.ReportResolution == null && m.Status != "removed"))
            .ExecuteDeleteAsync(ct);

        var leads = await db.Leads
            .Where(l => l.UserId == null && l.CreatedAt <= cutoff && !db.LeadMessages.Any(m => m.LeadId == l.Id))
            .ExecuteDeleteAsync(ct);

        return new RetainedLeadPurgeResult(messages, leads);
    }

    public Task<int> PurgeExpiredOrdersAsync(DateTime now, CancellationToken ct = default)
    {
        var cutoff = RetentionPeriods.OrderPurgeCutoff(now);

        // Chỉ đơn của tài khoản đã xóa hẳn (user_id null); đơn của tài khoản còn sống không thuộc quy tắc này
        return db.PaymentTransactions
            .Where(p => p.UserId == null && p.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
