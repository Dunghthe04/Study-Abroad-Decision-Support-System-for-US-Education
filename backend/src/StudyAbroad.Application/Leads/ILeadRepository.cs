using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default);

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

    // [USAS-2053 / UC-74] Đọc và gửi tin nhắn trao đổi trong yêu cầu liên hệ (bảng app.lead_messages)
    Task<IReadOnlyList<LeadMessageDto>> GetLeadMessagesAsync(
        Guid leadId,
        Guid leadOwnerUserId,
        bool sharedProfile,
        string centerName,
        CancellationToken ct = default);

    Task<LeadMessage> AddLeadMessageAsync(
        LeadMessage message,
        Lead? leadToUpdate = null,
        CancellationToken ct = default);
}

