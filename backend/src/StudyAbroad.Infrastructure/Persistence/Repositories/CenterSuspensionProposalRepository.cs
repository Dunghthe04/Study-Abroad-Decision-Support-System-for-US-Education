using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-58] Đề nghị tạm ngừng trung tâm do Moderator gửi lên Admin.</summary>
public class CenterSuspensionProposalRepository(AppDbContext db) : ICenterSuspensionProposalRepository
{
    public Task<bool> HasPendingAsync(Guid centerId, CancellationToken ct = default) =>
        db.CenterSuspensionProposals.AsNoTracking()
            .AnyAsync(p => p.StudyCenterId == centerId && p.Status == CenterSuspensionProposalStatuses.Pending, ct);

    public async Task AddAsync(CenterSuspensionProposal proposal, CancellationToken ct = default)
    {
        db.CenterSuspensionProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
    }

    public Task<CenterSuspensionProposal?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.CenterSuspensionProposals.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task SaveAsync(CenterSuspensionProposal proposal, CancellationToken ct = default)
    {
        proposal.UpdatedAt = DateTime.UtcNow;
        db.CenterSuspensionProposals.Update(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : (pageSize > 50 ? 50 : pageSize);

        var query = from p in db.CenterSuspensionProposals.AsNoTracking()
                    join c in db.StudyCenters.AsNoTracking() on p.StudyCenterId equals c.Id
                    select new { Proposal = p, CenterName = c.Name };
        if (!string.IsNullOrEmpty(status) && status != "all")
            query = query.Where(x => x.Proposal.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Proposal.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new CenterSuspensionProposalDto(
            x.Proposal.Id, x.Proposal.StudyCenterId, x.CenterName, x.Proposal.ProposedByUserId, x.Proposal.Reason,
            x.Proposal.Status, x.Proposal.RemovedMessageCount, x.Proposal.CreatedAt,
            x.Proposal.DecidedByUserId, x.Proposal.DecidedAt, x.Proposal.DecisionNote)).ToList();

        return new PagedResult<CenterSuspensionProposalDto>(items, safePage, safePageSize, total);
    }
}
