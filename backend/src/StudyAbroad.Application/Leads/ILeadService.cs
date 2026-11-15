using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Leads;

public interface ILeadService
{
    Task<LeadDto> CreateLeadAsync(Guid userId, CreateLeadRequest request, string? clientIp = null, CancellationToken ct = default);
    Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default);

    // [UC-44] Xem yêu cầu liên hệ đã gửi (học sinh từ 16 tuổi, phụ huynh)
    Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

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

    // [Nhắn tin phía người gửi] Học sinh (từ 16 tuổi) / phụ huynh đọc và gửi tin trong yêu cầu liên hệ của mình
    Task<LeadMessagesThreadDto> GetSentLeadMessagesAsync(Guid userId, Guid leadId, CancellationToken ct = default);

    Task<SendLeadMessageResultDto> SendSentLeadMessageAsync(Guid userId, Guid leadId, string content, CancellationToken ct = default);
}

