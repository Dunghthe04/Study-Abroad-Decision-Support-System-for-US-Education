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

    // [UC-116] Học sinh dưới 16 gửi: chờ phụ huynh xác nhận qua link mail
    /// <summary>Lead theo hash bí mật trong link mail phụ huynh. Không theo dõi thay đổi.</summary>
    Task<Lead?> GetByParentTokenHashAsync(string tokenHash, CancellationToken ct = default);
    /// <summary>
    /// Ghi kết quả phụ huynh trả lời / hết hạn (kèm bản ghi đồng ý nếu có) chỉ khi lead còn "pending_parent".
    /// False nếu lead đã có kết quả: ai bấm trước quyết định.
    /// </summary>
    Task<bool> TryCompleteParentConfirmAsync(Guid leadId, LeadParentDecision decision, Consent? consent, CancellationToken ct = default);

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

    /// <summary>
    /// [UC-110 bước 1] Dữ liệu trung tâm sẽ thấy khi nhận lead, dựng như <see cref="GetReceivedLeadDetailAsync"/> nhưng không lọc
    /// lead chờ phụ huynh. Chỉ dùng để làm bản xem trước trong mail phụ huynh, không trả cho trung tâm.
    /// </summary>
    Task<CenterReceivedLeadDto?> GetSharedPreviewAsync(Guid leadId, CancellationToken ct = default);

    Task<Lead?> GetLeadForUpdateAsync(Guid leadId, CancellationToken ct = default);
    Task UpdateAsync(Lead lead, CancellationToken ct = default);

    // [USAS-2053 / UC-74] Đọc và gửi tin nhắn trao đổi trong yêu cầu liên hệ (bảng app.lead_messages)
    Task<IReadOnlyList<LeadMessageDto>> GetLeadMessagesAsync(
        Guid leadId,
        Guid? leadOwnerUserId,
        bool sharedProfile,
        string centerName,
        CancellationToken ct = default);

    Task<LeadMessage> AddLeadMessageAsync(
        LeadMessage message,
        Lead? leadToUpdate = null,
        CancellationToken ct = default);

    // [USAS-2080 / UC-58] Moderator gỡ tin nhắn vi phạm: lấy tin (đúng lead) để sửa rồi lưu
    Task<LeadMessage?> GetLeadMessageForUpdateAsync(Guid leadId, Guid messageId, CancellationToken ct = default);

    Task SaveLeadMessageAsync(LeadMessage message, CancellationToken ct = default);

    /// <summary>
    /// [Chat realtime] Đánh dấu đã xem các tin còn hiện của phía đối diện trong lead. readerIsCenterSide = true thì người đọc là
    /// trung tâm (đánh dấu tin của học sinh / phụ huynh), ngược lại là tin của trung tâm. Trả về mã các tin vừa đánh dấu.
    /// </summary>
    Task<IReadOnlyList<Guid>> MarkMessagesReadAsync(Guid leadId, bool readerIsCenterSide, DateTime readAt, CancellationToken ct = default);

    /// <summary>[UC-58] Hàng đợi báo cáo cho Admin, mới báo trước. filter: "pending" (chưa xử lý), "resolved" (đã gỡ, cảnh cáo hoặc khóa) hoặc "all".</summary>
    Task<PagedResult<AdminLeadMessageReportDto>> GetReportedMessagesAsync(
        string filter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>[UC-58] Gỡ toàn bộ tin nhắn đang hiện của một người dùng (khi tài khoản bị khóa). Trả về số tin đã gỡ.</summary>
    Task<int> RemoveAllMessagesOfUserAsync(Guid senderUserId, Guid removedByUserId, CancellationToken ct = default);

    /// <summary>[UC-58] Id các tài khoản Admin đang hoạt động, để gửi thông báo báo cáo.</summary>
    Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default);

    Task AddNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);

    /// <summary>
    /// [UC-58] Tin bị báo cáo cùng tối đa <paramref name="contextSize"/> tin liền trước và liền sau trong cùng yêu cầu liên hệ,
    /// xếp theo thời gian. Trả danh sách rỗng nếu tin không thuộc yêu cầu này.
    /// </summary>
    Task<IReadOnlyList<AdminReportContextMessageDto>> GetReportContextMessagesAsync(
        Guid leadId,
        Guid messageId,
        int contextSize,
        CancellationToken ct = default);

    /// <summary>[UC-58] Số tin ngữ cảnh mỗi phía do Admin cấu hình trong app_settings, null nếu chưa cấu hình hoặc giá trị không đọc được.</summary>
    Task<int?> GetReportContextSizeSettingAsync(CancellationToken ct = default);

    /// <summary>[UC-67 / UC-55 4c] Từ chối tự động các lead ở trạng thái Mới khi trung tâm bị ẩn hoặc thu hồi dấu xác minh, đồng thời gửi thông báo cho người gửi.</summary>
    Task<int> RejectNewLeadsByCenterAsync(Guid studyCenterId, string centerName, string reason, DateTime now, CancellationToken ct = default) =>
        Task.FromResult(0);

    /// <summary>[UC-57] Chuyển các lead đang mở của trung tâm sang center_inactive khi tài khoản trung tâm bị khóa.</summary>
    Task<int> MarkOpenLeadsCenterInactiveAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default) =>
        Task.FromResult(0);
}

