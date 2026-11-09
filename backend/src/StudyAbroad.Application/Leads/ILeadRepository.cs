using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default);
    // [UC-44] Lấy danh sách yêu cầu tư vấn đã gửi của học sinh
    Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default);

    // [UC-73] Quản lý và xem yêu cầu liên hệ nhận được của trung tâm
    Task<CenterLeadsSummaryDto> GetReceivedLeadsAsync(
        Guid? centerId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<CenterReceivedLeadDto?> GetReceivedLeadDetailAsync(
        Guid leadId,
        Guid? restrictToCenterId,
        CancellationToken ct = default);

    Task<Lead?> GetLeadForUpdateAsync(Guid leadId, CancellationToken ct = default);
    Task UpdateAsync(Lead lead, CancellationToken ct = default);
}

