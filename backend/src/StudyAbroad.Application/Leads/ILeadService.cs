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

    // [USAS-2080 / UC-58] Moderator gỡ tin nhắn vi phạm, tin hiển thị "Tin nhắn đã bị gỡ" và có nhật ký
    // [USAS-2080 / UC-58] Hàng đợi tin bị gắn cờ / đã gỡ và thao tác Giữ (bỏ cờ) của Moderator
    Task<PagedResult<ModerationLeadMessageDto>> GetModerationMessagesAsync(string? filter, int page, int pageSize, CancellationToken ct = default);

    Task<LeadMessageDto> KeepLeadMessageAsync(Guid moderatorUserId, Guid leadId, Guid messageId, CancellationToken ct = default);

    Task<LeadMessageDto> RemoveLeadMessageAsync(Guid moderatorUserId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default);
}

