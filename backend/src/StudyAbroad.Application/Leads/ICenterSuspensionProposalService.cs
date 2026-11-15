using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Leads;

public interface ICenterSuspensionProposalService
{
    Task<IReadOnlyList<RepeatRemovalCenterDto>> GetRepeatRemovalCentersAsync(CancellationToken ct = default);

    Task<CenterSuspensionProposalDto> ProposeAsync(Guid moderatorUserId, Guid centerId, string? reason, CancellationToken ct = default);

    Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    Task<CenterSuspensionProposalDto> DecideAsync(Guid adminUserId, Guid proposalId, bool approve, string? note, CancellationToken ct = default);
}
