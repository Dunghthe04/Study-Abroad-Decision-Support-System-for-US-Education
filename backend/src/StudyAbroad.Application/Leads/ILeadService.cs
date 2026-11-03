using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Leads;

public interface ILeadService
{
    Task<LeadDto> CreateLeadAsync(Guid userId, CreateLeadRequest request, string? clientIp = null, CancellationToken ct = default);
    Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default);
    Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default);
}

