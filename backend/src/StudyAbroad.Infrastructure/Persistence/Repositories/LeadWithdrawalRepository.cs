using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-80] Thu hồi lead còn mở bằng một câu UPDATE, chạy chung transaction của bước xóa tài khoản.</summary>
public class LeadWithdrawalRepository(AppDbContext db) : ILeadWithdrawalRepository
{
    public async Task<int> WithdrawOpenLeadsAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var withdrawn = await db.Leads
            .Where(l => l.UserId == userId
                && (l.Status == LeadStatuses.New || l.Status == LeadStatuses.Contacted))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.UpdatedAt, now), ct);

        // [UC-116] Lead còn chờ phụ huynh: trung tâm chưa nhận nên chỉ hủy; xóa hash để link trong mail hết dùng được
        await db.Leads
            .Where(l => l.UserId == userId && l.Status == LeadStatuses.PendingParent)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Cancelled)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.UpdatedAt, now), ct);

        return withdrawn;
    }
}
