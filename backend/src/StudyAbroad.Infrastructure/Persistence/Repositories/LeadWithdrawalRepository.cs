using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-80] Thu hồi lead còn mở bằng một câu UPDATE, chạy chung transaction của bước xóa tài khoản.</summary>
public class LeadWithdrawalRepository(AppDbContext db) : ILeadWithdrawalRepository
{
    public Task<int> WithdrawOpenLeadsAsync(Guid userId, DateTime now, CancellationToken ct = default) =>
        db.Leads
            .Where(l => l.UserId == userId
                && (l.Status == LeadStatuses.New || l.Status == LeadStatuses.Contacted))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LeadStatuses.Withdrawn)
                .SetProperty(l => l.UpdatedAt, now), ct);
}
