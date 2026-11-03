using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default);
}

