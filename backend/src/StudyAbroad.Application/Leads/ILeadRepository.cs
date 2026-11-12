using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default);

    // [UC-42] Giới hạn gửi, tính theo người bấm gửi (SubmittedByUserId)
    /// <summary>Số lead người này đã gửi từ <paramref name="sinceUtc"/> (giới hạn 5 lần/ngày).</summary>
    Task<int> CountBySubmitterSinceAsync(Guid submitterId, DateTime sinceUtc, CancellationToken ct = default);
    /// <summary>Thời điểm gửi gần nhất của người này tới trung tâm, chỉ xét từ <paramref name="sinceUtc"/>; null nếu chưa gửi (giới hạn 7 ngày).</summary>
    Task<DateTime?> GetLastToCenterAsync(Guid submitterId, Guid centerId, DateTime sinceUtc, CancellationToken ct = default);

    // [UC-44] Xem yêu cầu liên hệ đã gửi
    /// <summary>
    /// Lead có học sinh thuộc <paramref name="studentIds"/> hoặc do <paramref name="submitterId"/> bấm gửi,
    /// mới nhất trước. Học sinh: studentIds = [chính mình], submitterId = null.
    /// Phụ huynh: studentIds = các con đang liên kết, submitterId = phụ huynh.
    /// </summary>
    Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(
        IReadOnlyCollection<Guid> studentIds,
        Guid? submitterId,
        int page,
        int pageSize,
        CancellationToken ct = default);

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

