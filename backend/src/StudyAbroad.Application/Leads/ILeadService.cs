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

    // [UC-73] Trung tâm chấp nhận / từ chối yêu cầu liên hệ Mới
    Task<CenterReceivedLeadDto> AcceptReceivedLeadAsync(Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default);

    Task<CenterReceivedLeadDto> RejectReceivedLeadAsync(Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default);

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
        Func<CancellationToken, Task<LeadMessageAttachment>>? attachmentFactory = null,
        CancellationToken ct = default);

    // [Nhắn tin phía người gửi] Học sinh (từ 16 tuổi) / phụ huynh đọc và gửi tin trong yêu cầu liên hệ của mình
    Task<LeadMessagesThreadDto> GetSentLeadMessagesAsync(Guid userId, Guid leadId, CancellationToken ct = default);

    Task<SendLeadMessageResultDto> SendSentLeadMessageAsync(
        Guid userId,
        Guid leadId,
        string content,
        Func<CancellationToken, Task<LeadMessageAttachment>>? attachmentFactory = null,
        CancellationToken ct = default);

    // [UC-143 / UC-54] Báo cáo tin nhắn vi phạm (học sinh / phụ huynh hoặc trung tâm) -> Admin nhận thông báo, xem xét và xử lý
    Task<LeadMessageDto> ReportSentLeadMessageAsync(Guid userId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default, string? detail = null);

    Task<LeadMessageDto> ReportReceivedLeadMessageAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        Guid messageId,
        string? reason,
        CancellationToken ct = default,
        string? detail = null);

    /// <summary><paramref name="viewerAdminId"/> có giá trị thì lần đọc hàng đợi được ghi nhật ký truy cập.</summary>
    Task<PagedResult<AdminLeadMessageReportDto>> GetReportedMessagesAsync(string? filter, int page, int pageSize, CancellationToken ct = default, Guid? viewerAdminId = null);

    /// <summary>Tin bị báo cáo kèm một số tin ngữ cảnh liền kề (số lượng là tham số hệ thống). Mỗi lần đọc ghi nhật ký truy cập.</summary>
    Task<AdminLeadMessageReportContextDto> GetReportContextAsync(Guid adminUserId, Guid leadId, Guid messageId, CancellationToken ct = default);

    /// <summary>Gỡ riêng tin nhắn bị báo cáo (không khóa tài khoản). Thông báo cho người gửi và người báo cáo.</summary>
    Task RemoveReportedMessageAsync(Guid adminUserId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default);

    /// <summary>Cảnh cáo người gửi tin bị báo cáo, tin vẫn hiện. Thông báo cho người gửi và người báo cáo.</summary>
    Task WarnReportedSenderAsync(Guid adminUserId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default);

    /// <summary>Sau khi khóa tài khoản người gửi: đánh dấu báo cáo đã xử lý và thông báo cho người báo cáo.</summary>
    Task MarkReportResolvedByLockAsync(Guid adminUserId, Guid leadId, Guid messageId, CancellationToken ct = default);

    Task DismissReportAsync(Guid adminUserId, Guid leadId, Guid messageId, CancellationToken ct = default);

    Task<Guid> GetMessageSenderIdAsync(Guid leadId, Guid messageId, CancellationToken ct = default);

    Task<int> RemoveAllMessagesOfLockedSenderAsync(Guid adminUserId, Guid senderUserId, string? reason, CancellationToken ct = default);

    // ===== [Chat realtime] =====

    /// <summary>Người dùng được vào phòng chat của yêu cầu liên hệ ở phía nào (chặn IDOR giống đọc tin nhắn).</summary>
    Task<LeadChatAccessDto> GetChatAccessAsync(Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default);

    /// <summary>Đánh dấu đã xem các tin của phía đối diện và báo cho họ biết.</summary>
    Task MarkThreadReadAsync(Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default);
}
