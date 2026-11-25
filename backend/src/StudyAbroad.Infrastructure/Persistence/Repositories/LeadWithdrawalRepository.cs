using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-80] Thu hồi lead còn mở bằng một câu UPDATE, chạy chung transaction của bước xóa tài khoản. [UC-111] Dùng lại khi phụ huynh rút đồng ý.</summary>
public class LeadWithdrawalRepository(AppDbContext db) : ILeadWithdrawalRepository
{
    public async Task<int> WithdrawOpenLeadsAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var withdrawn = await db.Leads
            .Where(l => l.UserId == userId
                && (l.Status == LeadStatuses.New || l.Status == LeadStatuses.Accepted || l.Status == LeadStatuses.Contacted))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.UpdatedAt, now), ct);

        // [UC-75 bước 4, UC-111 bước 5] Lead còn chờ phụ huynh cũng là yêu cầu chưa kết thúc: Đã thu hồi (không phải "Phụ huynh từ chối").
        // Trung tâm chưa nhận nên không cần báo; xóa hash để link trong mail hết dùng được
        await db.Leads
            .Where(l => l.UserId == userId && l.Status == LeadStatuses.PendingParent)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.ParentTokenHash, (string?)null)
                .SetProperty(l => l.UpdatedAt, now), ct);

        return withdrawn;
    }

    public async Task<IReadOnlyList<Guid>> ListCenterOwnersOfOpenLeadsAsync(Guid userId, CancellationToken ct = default) =>
        await db.Leads
            .Where(l => l.UserId == userId
                && (l.Status == LeadStatuses.New || l.Status == LeadStatuses.Accepted || l.Status == LeadStatuses.Contacted))
            .Join(db.StudyCenters, l => l.StudyCenterId, c => c.Id, (l, c) => c.OwnerUserId)
            .Where(owner => owner != null)
            .Select(owner => owner!.Value)
            .Distinct()
            .ToListAsync(ct);
}
