using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>[UC-58] Lưu đề nghị tạm ngừng trung tâm (bảng app.center_suspension_proposals).</summary>
public interface ICenterSuspensionProposalRepository
{
    Task<bool> HasPendingAsync(Guid centerId, CancellationToken ct = default);

    Task AddAsync(CenterSuspensionProposal proposal, CancellationToken ct = default);

    Task<CenterSuspensionProposal?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    Task SaveAsync(CenterSuspensionProposal proposal, CancellationToken ct = default);

    /// <summary>Mới nhất trước; <paramref name="status"/> null hoặc "all" là mọi trạng thái.</summary>
    Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
}
