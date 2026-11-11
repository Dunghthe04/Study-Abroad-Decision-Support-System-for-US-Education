namespace StudyAbroad.Application.Leads;

public interface ILeadService
{
    Task<LeadDto> CreateLeadAsync(Guid userId, CreateLeadRequest request, string? clientIp = null, CancellationToken ct = default);
    Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default);

    // [UC-73] Quản lý và xem yêu cầu liên hệ nhận được của trung tâm
    Task<CenterLeadsSummaryDto> GetReceivedLeadsForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid? targetCenterId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<CenterReceivedLeadDto?> GetReceivedLeadDetailForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default);

    Task<CenterReceivedLeadDto> UpdateReceivedLeadStatusAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        string newStatus,
        CancellationToken ct = default);

    // [USAS-2053 / UC-74] Đọc và gửi tin nhắn trả lời trong yêu cầu liên hệ phía Trung tâm
    Task<LeadMessagesThreadDto> GetReceivedLeadMessagesForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default);

    Task<SendLeadMessageResultDto> SendLeadMessageForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        string content,
        CancellationToken ct = default);
}

