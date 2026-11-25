using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [UC-42 / UC-73 / USAS-2053 UC-74] Dịch vụ gửi và xử lý yêu cầu tư vấn tới Trung tâm du học.
/// </summary>
public class LeadService(
    ILeadRepository leads,
    IStudyCenterRepository studyCenters,
    IUserRepository users,
    IParentLinkRepository parentLinks,
    IAuditLogRepository? auditLogs = null,
    IEmailSender? emailSender = null,
    INotificationService? notifications = null,
    ILeadChatNotifier? chat = null,
    ILeadChatPresence? presence = null,
    StudyAbroad.Application.Admin.Policies.IPolicyVersionLookup? policyVersions = null) : ILeadService
{
    public const int MaxMessageLength = 2000;
    public const int MaxReplyMessageLength = 4000;
    public const int MaxPhoneLength = 32;
    /// <summary>UC-42: mỗi người gửi tối đa 5 lead từ 0h hôm nay (giờ Việt Nam).</summary>
    public const int MaxLeadsPerDay = 5;
    /// <summary>UC-42: gửi lại cùng một trung tâm phải cách lần trước 7 ngày.</summary>
    public static readonly TimeSpan CenterCooldown = TimeSpan.FromDays(7);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "new", "accepted", "contacted", "closed"
    };

    /// <summary>[UC-80] Người gửi đã xóa tài khoản: lead bị thu hồi, không ai trao đổi hay đổi trạng thái được nữa.</summary>
    private static void EnsureNotWithdrawn(Lead lead)
    {
        if (string.Equals(lead.Status, LeadStatuses.Withdrawn, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_withdrawn", 400, "Yêu cầu đã được người gửi thu hồi, không thể tiếp tục trao đổi.");
    }

    /// <summary>
    /// [UC-74] Chỉ nhắn tin được khi yêu cầu Đã chấp nhận (gồm "contacted" cũ). Chặn khi yêu cầu còn Mới, Bị từ chối,
    /// Đã đóng, Đã thu hồi, Đã hủy, Hết hạn (7 ngày chờ phụ huynh) hoặc quá 90 ngày. Gọi trước khi tải ảnh lên.
    /// </summary>
    internal static void EnsureCanMessage(Lead lead)
    {
        var status = lead.Status;
        EnsureNotWithdrawn(lead);

        if (string.Equals(status, LeadStatuses.Closed, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_closed", 400, "Yêu cầu đã đóng, không gửi thêm tin nhắn được.");
        if (string.Equals(status, LeadStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_rejected", 400, "Yêu cầu đã bị trung tâm từ chối, không nhắn tin được.");
        if (string.Equals(status, LeadStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_cancelled", 400, "Yêu cầu đã bị hủy, không nhắn tin được.");
        if (string.Equals(status, LeadStatuses.CenterInactive, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("center_inactive", 400, "Trung tâm hiện tạm ngừng hoạt động, không thể tiếp tục nhắn tin.");
        if (string.Equals(status, LeadStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            || CenterLeadMapper.IsDataHidden(lead))
            throw new LeadRuleException("lead_expired", 400, "Yêu cầu đã hết hạn, không nhắn tin được.");
        // [UC-116] Còn chờ phụ huynh xác nhận: trung tâm chưa nhận yêu cầu
        if (string.Equals(status, LeadStatuses.PendingParent, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_not_open", 400, "Trung tâm chưa nhận yêu cầu này nên chưa nhắn tin được.");
        if (!LeadStatuses.IsAccepted(status))
            throw new LeadRuleException("lead_not_accepted", 400, "Trung tâm chưa chấp nhận yêu cầu này nên chưa nhắn tin được.");
    }

    private async Task<(Guid? CenterId, string? CenterName)> ResolveCenterScopeAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid? targetCenterId,
        CancellationToken ct)
    {
        if (userRole == UserRoles.Admin || userRole == UserRoles.Moderator)
        {
            return (targetCenterId, null);
        }

        if (userRole == UserRoles.Center)
        {
            var center = await studyCenters.GetByOwnerOrEmailAsync(userId, userEmail, ct);
            if (center is null)
                throw new KeyNotFoundException("Tài khoản chưa được liên kết với trung tâm tư vấn nào trong hệ thống.");
            return (center.Id, center.Name);
        }

        throw new UnauthorizedAccessException("Bạn không có quyền truy cập danh sách yêu cầu của trung tâm.");
    }

    private async Task<(Lead Lead, string CenterName)> GetAuthorizedLeadForCenterAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct)
    {
        if (leadId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ không hợp lệ.");

        var (centerId, centerName) = await ResolveCenterScopeAsync(userId, userEmail, userRole, null, ct);

        var lead = await leads.GetLeadForUpdateAsync(leadId, ct);
        // [UC-116] Lead chờ phụ huynh / bị từ chối / hết hạn: trung tâm chưa nhận, báo như không tồn tại
        if (lead is null || LeadParentConfirm.IsHiddenFromCenter(lead))
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        // [Chống IDOR] Trung tâm chỉ được đọc/trả lời các yêu cầu gửi tới đúng trung tâm mình
        // Lead của trung tâm khác báo "không tìm thấy" như lead không tồn tại, để không lộ lead có thật
        if (centerId.HasValue && lead.StudyCenterId != centerId.Value)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        if (string.IsNullOrWhiteSpace(centerName))
        {
            var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
            centerName = center?.Name ?? "Trung tâm tư vấn";
        }

        return (lead, centerName);
    }

    public async Task<LeadDto> CreateLeadAsync(
        Guid userId,
        CreateLeadRequest request,
        string? clientIp = null,
        CancellationToken ct = default)
    {
        if (request.StudyCenterId == Guid.Empty)
            throw new ArgumentException("Mã trung tâm tư vấn không được để trống.");

        // 1. Trung tâm tồn tại, đang hoạt động và đã được xác minh
        var center = await studyCenters.GetByIdAsync(request.StudyCenterId, ct);
        if (center is null || !center.IsActive)
            throw new KeyNotFoundException("Trung tâm tư vấn không tồn tại hoặc đã tạm dừng hoạt động.");
        if (center.VerificationStatus != CenterReferral.Verified)
            throw new LeadRuleException("center_not_verified", 400, "Trung tâm này chưa được xác minh nên chưa nhận yêu cầu liên hệ.");

        var message = request.Message?.Trim();
        if (!string.IsNullOrEmpty(message) && message.Length > MaxMessageLength)
            throw new ArgumentException($"Lời nhắn tối đa {MaxMessageLength} ký tự.");

        var phone = request.ContactPhone?.Trim();
        if (!string.IsNullOrEmpty(phone) && phone.Length > MaxPhoneLength)
            throw new ArgumentException($"Số điện thoại tối đa {MaxPhoneLength} ký tự.");

        var submitter = await users.GetUserById(userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản người gửi.");
        var now = DateTime.UtcNow;
        var today = AgePolicy.TodayInVietnam(now);

        // 2. Xác định học sinh có hồ sơ được chia sẻ
        Guid studentId;
        if (submitter.Role == UserRoles.Student)
        {
            studentId = submitter.Id; // học sinh tự gửi: bỏ qua StudentUserId nếu có
        }
        else if (submitter.Role == UserRoles.Parent)
        {
            if (request.StudentUserId is not { } childId || childId == Guid.Empty)
                throw new LeadRuleException("student_required", 400, "Vui lòng chọn con cần gửi yêu cầu liên hệ.");

            var linked = await parentLinks.GetActiveByParentAsync(submitter.Id, ct);
            if (!linked.Any(l => l.StudentUserId == childId))
                throw new LeadRuleException("not_linked", 403, "Bạn chưa liên kết với học sinh này.");
            studentId = childId;
        }
        else
        {
            throw new LeadRuleException("role_not_allowed", 403, "Chỉ học sinh hoặc phụ huynh được gửi yêu cầu liên hệ.");
        }

        // 3. Kiểm tra tuổi, chỉ khi học sinh tự gửi (phụ huynh gửi thay con dưới 16 vẫn được)
        var needsParentConfirm = false;
        if (submitter.Role == UserRoles.Student)
        {
            if (AgeFeatures.Check(AgeFeatures.ShareWithCenter, submitter.Role, submitter.DateOfBirth, today, hasParentConsent: false) is not null)
            {
                // [UC-116] Dưới 16: phải có phụ huynh đã đồng ý (UC-115); lead chờ phụ huynh xác nhận qua mail rồi trung tâm mới nhận
                if (!await parentLinks.HasGrantedConsentAsync(submitter.Id, ct))
                    throw new LeadRuleException("age_restricted", 403,
                        "Học sinh dưới 16 tuổi cần phụ huynh đồng ý trước khi gửi yêu cầu liên hệ. Hãy mời phụ huynh xác nhận ở trang Tài khoản.");
                needsParentConfirm = true;
            }

            var isMinor = submitter.DateOfBirth is { } dob && AgePolicy.GetAge(dob, today) < AgeGroups.AdultAge;
            if (isMinor && !request.AcknowledgedCenterContact)
                throw new LeadRuleException("acknowledgement_required", 400,
                    "Vui lòng xác nhận bạn hiểu trung tâm sẽ nhận thông tin này và liên hệ lại với bạn.");
        }

        // 4. Nhóm dữ liệu chia sẻ: chỉ 5 key hợp lệ, bỏ trùng, bắt buộc có contact
        var requested = (request.SharedFields ?? [])
            .Select(f => f?.Trim().ToLowerInvariant() ?? string.Empty)
            .ToHashSet();
        var unknown = requested.Where(f => !LeadSharedFields.All.Contains(f)).ToList();
        if (unknown.Count > 0)
            throw new LeadRuleException("invalid_shared_fields", 400, $"Nhóm dữ liệu không hợp lệ: {string.Join(", ", unknown)}.");
        if (!requested.Contains(LeadSharedFields.Contact))
            throw new LeadRuleException("contact_required", 400, "Bắt buộc chia sẻ thông tin liên hệ để trung tâm liên hệ lại.");
        var sharedFields = LeadSharedFields.All.Where(requested.Contains).ToList(); // giữ thứ tự chuẩn

        // 5. Đồng ý chia sẻ
        if (!request.ConsentAccepted)
            throw new LeadRuleException("consent_required", 400, "Vui lòng đồng ý chia sẻ các thông tin đã chọn cho trung tâm.");

        // 6. Giới hạn gửi, tính theo người bấm gửi
        var lastToCenter = await leads.GetLastToCenterAsync(submitter.Id, center.Id, now - CenterCooldown, ct);
        if (lastToCenter is { } last)
        {
            var retryAt = last + CenterCooldown;
            throw new LeadRuleException("center_cooldown", 429,
                $"Bạn đã gửi yêu cầu tới trung tâm này. Có thể gửi lại sau {(retryAt + VietnamOffset):HH:mm dd/MM/yyyy}.",
                retryAt);
        }

        var startOfTodayUtc = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);
        if (await leads.CountBySubmitterSinceAsync(submitter.Id, startOfTodayUtc, ct) >= MaxLeadsPerDay)
            throw new LeadRuleException("daily_limit", 429, $"Mỗi ngày chỉ gửi được tối đa {MaxLeadsPerDay} yêu cầu liên hệ. Vui lòng thử lại vào ngày mai.");

        // 7. Lưu consent và lead trong cùng một lần SaveChanges
        var isParent = submitter.Role == UserRoles.Parent;
        // [UC-116] Học sinh dưới 16: chưa có đồng ý chia sẻ; bản ghi đồng ý tạo khi phụ huynh bấm Đồng ý trong mail
        var parentSecret = needsParentConfirm ? ParentInvite.GenerateToken() : null;
        // [UC-42 4b / UC-110 bước 3] Ghi phiên bản văn bản đang hiệu lực: phụ huynh gửi thay con dưới 16 là đồng ý của người đại diện
        var child = isParent ? await users.GetUserById(studentId, ct) : null;
        var byGuardian = child?.DateOfBirth is { } childDob && AgePolicy.GetAge(childDob, today) < AgeGroups.ChildMaxAgeExclusive;
        var consent = needsParentConfirm ? null : new Consent
        {
            UserId = studentId,
            Purpose = AgeFeatures.ShareWithCenter,
            Granted = true,
            PolicyVersion = await LeadParentConfirm.ConsentPolicyVersionAsync(policyVersions, byGuardian, ct),
            GrantedByUserId = isParent ? submitter.Id : null,
            GrantedIp = clientIp,
        };

        var lead = new Lead
        {
            UserId = studentId,
            SubmittedByUserId = submitter.Id,
            StudyCenterId = center.Id,
            StudyLevel = request.StudyLevel?.Trim(),
            ContactPhone = phone,
            Message = message,
            SharedFields = sharedFields,
            SharedProfile = sharedFields.Any(f => f != LeadSharedFields.Contact),
            AcknowledgedCenterContact = !isParent && request.AcknowledgedCenterContact,
            Status = needsParentConfirm ? LeadStatuses.PendingParent : LeadStatuses.New,
            ParentTokenHash = parentSecret is null ? null : ParentInvite.HashToken(parentSecret),
            ParentTokenExpiresAt = parentSecret is null ? null : now.AddDays(LeadParentConfirm.ExpiryDays),
        };

        var created = await leads.CreateLeadAsync(lead, consent, ct);
        if (parentSecret != null)
            await SendParentConfirmEmailsAsync(submitter, center.Name, created, parentSecret, ct);
        // [UC-110 / UC-42] Báo trung tâm có yêu cầu mới (lead chờ phụ huynh xác nhận thì trung tâm chưa thấy, chưa báo)
        else if (notifications is not null && center.OwnerUserId is { } ownerId)
            await notifications.NotifyAsync(ownerId, NotificationTypes.LeadNew, "Có yêu cầu liên hệ mới",
                "Một học sinh / phụ huynh vừa gửi yêu cầu liên hệ tới trung tâm.", "/center/leads", ct);
        return (await leads.GetDtoByIdAsync(created.Id, ct))!;
    }

    /// <summary>[UC-116] Mail hỏi đồng ý tới mọi phụ huynh đang liên kết; mỗi người một link (bí mật chung + mã liên kết).</summary>
    private async Task SendParentConfirmEmailsAsync(User student, string centerName, Lead lead, string secret, CancellationToken ct)
    {
        if (emailSender is null)
            return;

        var activeLinks = (await parentLinks.GetByStudentAsync(student.Id, ct))
            .Where(l => l.Status == ParentLinkStatuses.Active);
        // [UC-110 bước 1] Bản xem trước dựng từ đúng dữ liệu trung tâm sẽ thấy khi phụ huynh đồng ý
        var view = await leads.GetSharedPreviewAsync(lead.Id, ct);
        var preview = view is null
            ? lead.SharedFields.Select(f => new LeadSharedPreviewGroup(LeadParentConfirm.FieldLabel(f), [])).ToList()
            : LeadParentConfirm.BuildPreview(view, lead.SharedFields);
        foreach (var link in activeLinks)
        {
            await emailSender.SendLeadParentConfirmEmailAsync(link.ParentEmail, student.FullName, centerName, preview,
                LeadParentConfirm.ComposeToken(secret, link.Id), LeadParentConfirm.ExpiryDays, ct);
        }
    }

    public async Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default)
    {
        var lead = await leads.GetByIdAsync(leadId, ct);
        // [UC-42] Phụ huynh gửi thay con vẫn xem được lead mình đã gửi
        if (lead is null || (lead.UserId != userId && lead.SubmittedByUserId != userId))
            return null;

        return await leads.GetDtoByIdAsync(leadId, ct);
    }

    /// <summary>
    /// [UC-44] Danh sách yêu cầu liên hệ đã gửi.
    /// Học sinh (từ 16 tuổi): lead có hồ sơ của mình, kể cả lead phụ huynh gửi thay.
    /// Phụ huynh: lead mình bấm gửi, cộng lead của các con đang liên kết.
    /// </summary>
    public async Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var user = await users.GetUserById(userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == UserRoles.Student)
        {
            await EnsureStudentCanViewSentLeadsAsync(user, ct);
            return await leads.GetSentLeadsAsync([user.Id], submitterId: null, page, pageSize, ct);
        }

        if (user.Role == UserRoles.Parent)
        {
            // [UC-44] Phụ huynh xem được lead mình trực tiếp gửi + lead của con dưới 18 tuổi đang liên kết.
            // Con từ đủ 18 tuổi trở lên có quyền riêng tư với các lead do chính con gửi.
            var minorChildIds = await GetLinkedMinorChildIdsAsync(user.Id, ct);
            return await leads.GetSentLeadsAsync(minorChildIds, submitterId: user.Id, page, pageSize, ct);
        }

        throw SentLeadsRoleNotAllowed();
    }

    private async Task<List<Guid>> GetLinkedMinorChildIdsAsync(Guid parentUserId, CancellationToken ct)
    {
        var linked = await parentLinks.GetActiveByParentAsync(parentUserId, ct);
        var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
        var minorIds = new List<Guid>();
        foreach (var childId in linked.Select(l => l.StudentUserId).Distinct())
        {
            var child = await users.GetUserById(childId, ct);
            if (child?.DateOfBirth is not { } dob || AgePolicy.GetAge(dob, today) < AgeGroups.AdultAge)
                minorIds.Add(childId);
        }
        return minorIds;
    }

    /// <summary>
    /// [UC-44] Theo tài liệu tuổi: học sinh dưới 16 không tự xem yêu cầu liên hệ, cùng mốc với tự gửi.
    /// [UC-116] Dưới 16 đã có phụ huynh đồng ý thì xem được (để theo dõi lead chờ phụ huynh xác nhận).
    /// </summary>
    private async Task EnsureStudentCanViewSentLeadsAsync(User student, CancellationToken ct)
    {
        if (await AgeAccess.CheckAsync(AgeFeatures.ShareWithCenter, student, parentLinks, DateTime.UtcNow, ct) is not null)
            throw new LeadRuleException("age_restricted", 403,
                "Học sinh dưới 16 tuổi cần phụ huynh đồng ý mới xem được yêu cầu liên hệ. Phụ huynh có thể xem từ tài khoản của mình.");
    }

    private static LeadRuleException SentLeadsRoleNotAllowed() =>
        new("role_not_allowed", 403, "Chỉ học sinh hoặc phụ huynh xem được yêu cầu liên hệ đã gửi.");

    /// <summary>
    /// [Nhắn tin phía người gửi] Lấy một lead mà học sinh / phụ huynh được xem, cùng luật với GetSentLeadsAsync:
    /// học sinh từ 16 tuổi là chủ lead; phụ huynh là người bấm gửi hoặc đang liên kết với học sinh của lead.
    /// Không có quyền thì báo "không tìm thấy" như lead không tồn tại, để không lộ lead của người khác.
    /// </summary>
    private async Task<(User User, Lead Lead, string CenterName)> GetAuthorizedSentLeadAsync(
        Guid userId,
        Guid leadId,
        CancellationToken ct)
    {
        if (leadId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ không hợp lệ.");

        var user = await users.GetUserById(userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == UserRoles.Student)
            await EnsureStudentCanViewSentLeadsAsync(user, ct);
        else if (user.Role != UserRoles.Parent)
            throw SentLeadsRoleNotAllowed();

        var lead = await leads.GetByIdAsync(leadId, ct);
        if (lead is null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        var canView = false;
        if (user.Role == UserRoles.Student)
        {
            canView = lead.UserId == user.Id;
        }
        else
        {
            // [UC-44] Phụ huynh: lead mình bấm gửi (kể cả khi liên kết đã hủy), hoặc lead của con dưới 18 tuổi đang liên kết
            canView = lead.SubmittedByUserId == user.Id
                      || (lead.UserId is { } studentId && (await GetLinkedMinorChildIdsAsync(user.Id, ct)).Contains(studentId));
        }
        if (!canView)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        var center = await studyCenters.GetByIdAsync(lead!.StudyCenterId, ct);
        return (user, lead, center?.Name ?? "Trung tâm tư vấn");
    }

    /// <summary>
    /// [Nhắn tin phía người gửi] Học sinh / phụ huynh đọc luồng tin nhắn với trung tâm trong yêu cầu của mình.
    /// Người xem là học sinh hoặc phụ huynh của chính lead nên luôn hiện tên thật (sharedProfile: true);
    /// luật ẩn danh chỉ áp dụng cho phía trung tâm.
    /// </summary>
    public async Task<LeadMessagesThreadDto> GetSentLeadMessagesAsync(
        Guid userId,
        Guid leadId,
        CancellationToken ct = default)
    {
        var (_, lead, centerName) = await GetAuthorizedSentLeadAsync(userId, leadId, ct);
        var messages = await leads.GetLeadMessagesAsync(lead.Id, lead.UserId, sharedProfile: true, centerName, ct);

        return new LeadMessagesThreadDto(lead.Id, lead.Status, messages);
    }

    /// <summary>
    /// [Nhắn tin phía người gửi] Học sinh / phụ huynh gửi tin cho trung tâm trong yêu cầu của mình.
    /// Không còn bộ lọc nội dung: nhắn tự do, vi phạm xử lý qua báo cáo (UC-58).
    /// Khác phía trung tâm: không đổi trạng thái lead, và lead đã hoàn tất (closed) thì không gửi được.
    /// Có thể kèm một ảnh: <paramref name="attachmentFactory"/> chỉ được gọi sau khi đã kiểm tra quyền.
    /// </summary>
    public async Task<SendLeadMessageResultDto> SendSentLeadMessageAsync(
        Guid userId,
        Guid leadId,
        string content,
        Func<CancellationToken, Task<LeadMessageAttachment>>? attachmentFactory = null,
        CancellationToken ct = default)
    {
        var trimmedContent = ValidateMessageContent(content, allowEmpty: attachmentFactory is not null);

        var (user, lead, centerName) = await GetAuthorizedSentLeadAsync(userId, leadId, ct);

        // [UC-74] Chặn theo trạng thái trước khi tải ảnh lên
        EnsureCanMessage(lead);

        var attachment = attachmentFactory is null ? null : await attachmentFactory(ct);

        var inspection = LeadMessageContentFilter.Inspect(trimmedContent);

        var saved = await leads.AddLeadMessageAsync(new LeadMessage
        {
            LeadId = lead.Id,
            SenderUserId = user.Id,
            Content = trimmedContent,
            AttachmentUrl = attachment?.Url,
            AttachmentPublicId = attachment?.PublicId,
            Status = "visible",
            IsFlagged = inspection.IsFlagged,
            FlagReason = inspection.FlagReason,
        }, leadToUpdate: null, ct);

        await AuditMessageSentAsync(user.Id, user.Role, lead, saved, ct);

        var dto = LeadMessageMapper.ToDto(saved, user.Role, user.FullName, sharedProfile: true, centerName);
        // [Chat realtime] Trung tâm thấy tên theo quy tắc chia sẻ hồ sơ (UC-42), khác bản học sinh tự thấy
        var centerView = LeadMessageMapper.ToDto(
            saved, user.Role, user.FullName, lead.SharedFields.Contains(LeadSharedFields.Contact), centerName);
        await PublishNewMessageAsync(lead, saved, LeadChatSides.Student, dto, centerView, centerName, ct);
        return new SendLeadMessageResultDto(dto, lead.Status);
    }

    // ===== [Chat realtime] =====

    private const int NotificationPreviewLength = 120;

    /// <summary>Người ở phía học sinh của lead: học sinh chủ lead và phụ huynh đã bấm gửi (nếu có).</summary>
    private static IReadOnlyList<Guid> StudentSideUserIds(Lead lead)
    {
        // [UC-75 lớp 1b] Học sinh đã bị xóa hẳn (UserId null) thì không còn ai để báo
        var ids = new List<Guid>();
        if (lead.UserId is { } owner) ids.Add(owner);
        if (lead.SubmittedByUserId is { } submitted && submitted != lead.UserId) ids.Add(submitted);
        return ids;
    }

    /// <summary>
    /// [Chat realtime] Người dùng được vào phòng chat của lead ở phía nào. Dùng đúng luật phân quyền của đọc tin nhắn
    /// (học sinh / phụ huynh của lead hoặc trung tâm sở hữu lead), lead không có quyền báo "không tìm thấy".
    /// </summary>
    public async Task<LeadChatAccessDto> GetChatAccessAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default)
    {
        if (userRole is UserRoles.Student or UserRoles.Parent)
        {
            var (_, lead, _) = await GetAuthorizedSentLeadAsync(userId, leadId, ct);
            var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
            return new LeadChatAccessDto(LeadChatSides.Student, center?.OwnerUserId is { } owner ? [owner] : []);
        }

        if (userRole != UserRoles.Center)
            throw new LeadRuleException("role_not_allowed", 403, "Chỉ học sinh, phụ huynh hoặc trung tâm tham gia được cuộc trò chuyện này.");

        var (centerLead, _) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);
        return new LeadChatAccessDto(LeadChatSides.Center, StudentSideUserIds(centerLead));
    }

    /// <summary>
    /// [Chat realtime] Người đọc đã mở xem cuộc trò chuyện: đánh dấu đã xem các tin của phía đối diện và báo cho họ.
    /// </summary>
    public async Task MarkThreadReadAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default)
    {
        var access = await GetChatAccessAsync(userId, userEmail, userRole, leadId, ct);
        var readAt = DateTime.UtcNow;
        var ids = await leads.MarkMessagesReadAsync(leadId, access.Side == LeadChatSides.Center, readAt, ct);
        if (ids.Count == 0 || chat is null) return;

        // Tin vừa được xem là tin của phía đối diện với người đọc, nên báo cho đúng phía đó
        await chat.MessagesReadAsync(leadId, LeadChatSides.Opposite(access.Side), ids, readAt, ct);
    }

    /// <summary>
    /// [Chat realtime] Đẩy tin mới tới hai phía, rồi báo người nhận (chuông thông báo + toast) trừ khi họ đang mở đúng
    /// cuộc trò chuyện này. Lỗi ở đây không được làm hỏng việc gửi tin: tin đã lưu, bên kia vẫn lấy được bằng REST.
    /// </summary>
    private async Task PublishNewMessageAsync(
        Lead lead,
        LeadMessage saved,
        string senderSide,
        LeadMessageDto studentView,
        LeadMessageDto centerView,
        string centerName,
        CancellationToken ct)
    {
        try
        {
            if (chat is not null)
                await chat.NewMessageAsync(lead.Id, studentView, centerView, ct);

            IReadOnlyList<Guid> recipients;
            string title;
            string link;
            if (senderSide == LeadChatSides.Student)
            {
                var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
                recipients = center?.OwnerUserId is { } owner ? [owner] : [];
                title = $"Tin nhắn mới từ {centerView.SenderName}";
                link = "/center/leads";
            }
            else
            {
                recipients = StudentSideUserIds(lead);
                title = $"Tin nhắn mới từ {centerName}";
                link = "/account#sent-leads";
            }

            var body = string.IsNullOrWhiteSpace(saved.Content)
                ? "Đã gửi một ảnh."
                : saved.Content.Length > NotificationPreviewLength
                    ? saved.Content[..NotificationPreviewLength] + "…"
                    : saved.Content;

            foreach (var recipient in recipients)
            {
                if (presence?.IsViewing(recipient, lead.Id) == true) continue;

                // NotifyAsync lưu thông báo rồi tự đẩy realtime tới trình duyệt người nhận
                if (notifications is not null)
                    await notifications.NotifyAsync(recipient, "lead_message_new", title, body, link, ct);
            }
        }
        catch (Exception)
        {
            // Cố ý nuốt lỗi: realtime và thông báo chỉ là phần thêm, không được làm thất bại việc gửi tin đã lưu
        }
    }

    private static string ValidateMessageContent(string? content, bool allowEmpty)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 && !allowEmpty)
            throw new ArgumentException("Nội dung tin nhắn không được để trống.");
        if (trimmed.Length > MaxReplyMessageLength)
            throw new ArgumentException($"Nội dung tin nhắn tối đa {MaxReplyMessageLength} ký tự.");
        return trimmed;
    }

    private async Task AuditMessageSentAsync(Guid actorId, string actorRole, Lead lead, LeadMessage saved, CancellationToken ct)
    {
        if (auditLogs is null) return;
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorId,
            Action = "lead_message.send",
            EntityType = "lead_message",
            EntityId = saved.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                leadId = lead.Id,
                studyCenterId = lead.StudyCenterId,
                senderRole = actorRole,
                contentLength = saved.Content.Length,
                hasAttachment = saved.AttachmentUrl is not null,
                leadStatus = lead.Status,
            }),
        }, ct);
    }

    public async Task<CenterLeadsSummaryDto> GetReceivedLeadsForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid? targetCenterId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (centerId, _) = await ResolveCenterScopeAsync(userId, userEmail, userRole, targetCenterId, ct);
        return await leads.GetReceivedLeadsAsync(centerId, status, search, page, pageSize, ct);
    }

    public async Task<CenterReceivedLeadDto?> GetReceivedLeadDetailForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default)
    {
        var (centerId, _) = await ResolveCenterScopeAsync(userId, userEmail, userRole, null, ct);
        return await leads.GetReceivedLeadDetailAsync(leadId, centerId, ct);
    }

    public async Task<CenterReceivedLeadDto> UpdateReceivedLeadStatusAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        string newStatus,
        CancellationToken ct = default)
    {
        var normalizedStatus = newStatus?.Trim().ToLower() ?? "";
        if (!ValidStatuses.Contains(normalizedStatus))
            throw new ArgumentException("Trạng thái không hợp lệ. Chỉ chấp nhận 'new', 'accepted', 'contacted' hoặc 'closed'. Từ chối yêu cầu dùng thao tác riêng.");

        var (centerId, _) = await ResolveCenterScopeAsync(userId, userEmail, userRole, null, ct);

        var lead = await leads.GetLeadForUpdateAsync(leadId, ct);
        // [UC-116] Lead trung tâm chưa nhận: không đổi trạng thái được
        if (lead is null || LeadParentConfirm.IsHiddenFromCenter(lead))
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        // [Chống IDOR] Lead của trung tâm khác báo "không tìm thấy" như lead không tồn tại, để không lộ lead có thật
        if (centerId.HasValue && lead.StudyCenterId != centerId.Value)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        EnsureNotWithdrawn(lead);

        // [UC-73] Yêu cầu đã từ chối thì không đổi trạng thái được nữa
        if (string.Equals(lead.Status, LeadStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_rejected", 400, "Yêu cầu đã bị từ chối, không đổi trạng thái được.");

        var previousStatus = lead.Status;
        lead.Status = normalizedStatus;
        await leads.UpdateAsync(lead, ct);

        // [UC-110 / UC-73] Báo người gửi (và học sinh nếu phụ huynh gửi thay) khi trạng thái thật sự thay đổi
        if (notifications is not null && !string.Equals(previousStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            // Cùng cách gọi với màn hình của người gửi (types/lead.ts); "Mới" đứng một mình khó hiểu nên nói rõ là đang chờ trung tâm
            // "accepted" cũng là trạng thái khi trung tâm bấm "Mở lại trao đổi" sau khi đã đóng: lúc đó không phải vừa được chấp nhận
            var reopened = string.Equals(previousStatus, LeadStatuses.Closed, StringComparison.OrdinalIgnoreCase);
            var label = normalizedStatus switch
            {
                "accepted" when reopened => "Trung tâm đã mở lại trao đổi",
                "accepted" => "Đã được trung tâm chấp nhận",
                "contacted" => "Đang trao đổi",
                "closed" => "Đã hoàn tất",
                _ => "Chờ trung tâm phản hồi",
            };
            foreach (var recipient in new[] { lead.SubmittedByUserId, (Guid?)lead.UserId }.OfType<Guid>().Distinct())
                await notifications.NotifyAsync(recipient, NotificationTypes.LeadStatusChanged, "Yêu cầu liên hệ đổi trạng thái",
                    $"Yêu cầu liên hệ của bạn chuyển sang: {label}.", "/account", ct);
        }

        var updated = await leads.GetReceivedLeadDetailAsync(leadId, centerId, ct);
        return updated ?? throw new KeyNotFoundException("Không thể tải thông tin sau khi cập nhật.");
    }

    /// <summary>[UC-73] Trung tâm chấp nhận yêu cầu "Mới": sang "Đã chấp nhận" và mở nhắn tin (UC-74).</summary>
    public Task<CenterReceivedLeadDto> AcceptReceivedLeadAsync(
        Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default) =>
        DecideReceivedLeadAsync(userId, userEmail, userRole, leadId, accept: true, ct);

    /// <summary>[UC-73] Trung tâm từ chối yêu cầu "Mới": sang "Bị từ chối", người gửi nhận thông báo.</summary>
    public Task<CenterReceivedLeadDto> RejectReceivedLeadAsync(
        Guid userId, string? userEmail, string userRole, Guid leadId, CancellationToken ct = default) =>
        DecideReceivedLeadAsync(userId, userEmail, userRole, leadId, accept: false, ct);

    private async Task<CenterReceivedLeadDto> DecideReceivedLeadAsync(
        Guid userId, string? userEmail, string userRole, Guid leadId, bool accept, CancellationToken ct)
    {
        if (leadId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ không hợp lệ.");

        var (centerId, centerName) = await ResolveCenterScopeAsync(userId, userEmail, userRole, null, ct);

        var lead = await leads.GetLeadForUpdateAsync(leadId, ct);
        // [UC-116] Lead trung tâm chưa nhận; [Chống IDOR] lead của trung tâm khác báo "không tìm thấy"
        if (lead is null || LeadParentConfirm.IsHiddenFromCenter(lead)
            || (centerId.HasValue && lead.StudyCenterId != centerId.Value))
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        EnsureNotWithdrawn(lead);

        if (CenterLeadMapper.IsDataHidden(lead))
            throw new LeadRuleException("lead_expired", 400, "Yêu cầu đã thu hồi hoặc quá 90 ngày, không xử lý được nữa.");
        if (!string.Equals(lead.Status, LeadStatuses.New, StringComparison.OrdinalIgnoreCase))
            throw new LeadRuleException("lead_not_new", 400, "Chỉ chấp nhận hoặc từ chối được yêu cầu ở trạng thái Mới.");

        lead.Status = accept ? LeadStatuses.Accepted : LeadStatuses.Rejected;
        await leads.UpdateAsync(lead, ct);

        if (auditLogs is not null)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = accept ? "lead.accept" : "lead.reject",
                EntityType = "lead",
                EntityId = lead.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { studyCenterId = lead.StudyCenterId }),
            }, ct);
        }

        if (!accept)
        {
            if (string.IsNullOrWhiteSpace(centerName))
                centerName = (await studyCenters.GetByIdAsync(lead.StudyCenterId, ct))?.Name ?? "Trung tâm tư vấn";

            // Thông báo cho người bấm gửi, và học sinh nếu phụ huynh gửi thay
            var recipients = new[] { lead.SubmittedByUserId ?? lead.UserId, lead.UserId }.OfType<Guid>().Distinct().ToList();
            await leads.AddNotificationsAsync(recipients.Select(id => new Notification
            {
                UserId = id,
                Type = "lead_rejected",
                Title = "Yêu cầu liên hệ bị từ chối",
                Body = $"{centerName} đã từ chối yêu cầu liên hệ của bạn.",
                LinkUrl = "/account",
            }).ToList(), ct);
        }

        var updated = await leads.GetReceivedLeadDetailAsync(leadId, centerId, ct);
        return updated ?? throw new KeyNotFoundException("Không thể tải thông tin sau khi cập nhật.");
    }

    /// <summary>
    /// [USAS-2053 / UC-74] Đọc luồng tin nhắn trao đổi trong một yêu cầu liên hệ của trung tâm.
    /// Chặn IDOR theo trung tâm sở hữu và ẩn danh tên người gửi nếu lead không chia sẻ nhóm contact (UC-42).
    /// </summary>
    public async Task<LeadMessagesThreadDto> GetReceivedLeadMessagesForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default)
    {
        var (lead, centerName) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);

        // [UC-44 E1] Lead đã thu hồi vẫn xem được lịch sử tin nhắn; [UC-73] nhưng không lộ tên người gửi (dữ liệu cá nhân).
        // [UC-75 lớp 1b] Người gửi đã bị xóa hẳn hiện "Người dùng đã xóa tài khoản" (LeadMessageMapper)
        // [UC-42] Hiện tên khi chia sẻ nhóm contact, cùng quy tắc với CenterLeadMapper
        var withdrawn = string.Equals(lead.Status, LeadStatuses.Withdrawn, StringComparison.OrdinalIgnoreCase);
        var showName = !withdrawn && lead.SharedFields.Contains(LeadSharedFields.Contact);
        var messages = await leads.GetLeadMessagesAsync(lead.Id, lead.UserId, showName, centerName, ct);

        return new LeadMessagesThreadDto(lead.Id, lead.Status, messages);
    }

    /// <summary>
    /// [USAS-2053 / UC-74] Trung tâm tư vấn gửi tin nhắn trả lời trong yêu cầu liên hệ (có thể kèm ảnh) và
    /// tự chuyển trạng thái lead "new" -> "contacted". Không còn bộ lọc nội dung.
    /// </summary>
    public async Task<SendLeadMessageResultDto> SendLeadMessageForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        string content,
        Func<CancellationToken, Task<LeadMessageAttachment>>? attachmentFactory = null,
        CancellationToken ct = default)
    {
        var trimmedContent = ValidateMessageContent(content, allowEmpty: attachmentFactory is not null);

        var (lead, centerName) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);
        // [UC-74] Chỉ nhắn khi đã chấp nhận (UC-73); chặn trước khi tải ảnh lên
        EnsureCanMessage(lead);

        var attachment = attachmentFactory is null ? null : await attachmentFactory(ct);

        // [UC-74 / Phòng chống lừa đảo] Kiểm tra và gắn cờ nếu tin nhắn chứa STK ngân hàng, SĐT hoặc chào mời giao dịch ngoài
        var inspection = LeadMessageContentFilter.Inspect(trimmedContent);

        var entity = new LeadMessage
        {
            LeadId = lead.Id,
            SenderUserId = userId,
            Content = trimmedContent,
            AttachmentUrl = attachment?.Url,
            AttachmentPublicId = attachment?.PublicId,
            Status = "visible",
            RemovedByUserId = null,
            IsFlagged = inspection.IsFlagged,
            FlagReason = inspection.FlagReason,
        };

        // [UC-74] Không tự chuyển trạng thái nữa: chấp nhận yêu cầu là thao tác riêng ở UC-73
        var saved = await leads.AddLeadMessageAsync(entity, leadToUpdate: null, ct);

        await AuditMessageSentAsync(userId, userRole, lead, saved, ct);

        var dto = LeadMessageMapper.ToDto(
            saved,
            userRole,
            senderFullName: null,
            sharedProfile: lead.SharedFields.Contains(LeadSharedFields.Contact),
            centerName: centerName);

        // [Chat realtime] Tin của trung tâm hiện giống nhau ở hai phía (tên là tên trung tâm)
        await PublishNewMessageAsync(lead, saved, LeadChatSides.Center, dto, dto, centerName, ct);
        return new SendLeadMessageResultDto(dto, lead.Status);
    }

    public const int MaxRemoveReasonLength = 500;
    /// <summary>[UC-143] Độ dài lý do báo cáo tin nhắn (thống nhất với báo cáo tin diễn đàn UC-49): 3 đến 64 ký tự.</summary>
    public const int MinReportReasonLength = 3;
    public const int MaxReportReasonLength = 64;
    /// <summary>[UC-143] Độ dài chi tiết thêm (không bắt buộc): tối đa 1.000 ký tự.</summary>
    public const int MaxReportDetailLength = 1000;

    /// <summary>
    /// [UC-143] Học sinh / phụ huynh báo cáo một tin của trung tâm trong yêu cầu của mình (trong 90 ngày kể từ ngày gửi yêu cầu).
    /// Admin nhận thông báo. Báo lại tin đã báo không đổi gì; tin đã gỡ hoặc tin của chính mình thì không báo được.
    /// </summary>
    public async Task<LeadMessageDto> ReportSentLeadMessageAsync(
        Guid userId,
        Guid leadId,
        Guid messageId,
        string? reason,
        CancellationToken ct = default,
        string? detail = null)
    {
        var (trimmedReason, trimmedDetail) = ValidateReportInput(messageId, reason, detail);

        var (user, lead, centerName) = await GetAuthorizedSentLeadAsync(userId, leadId, ct);
        return await ReportMessageAsync(user.Id, user.Role, lead, centerName, messageId, trimmedReason, trimmedDetail,
            reporterIsCenterSide: false, sharedProfile: true, ct);
    }

    /// <summary>[UC-143] Trung tâm báo cáo một tin của học sinh / phụ huynh (trong 90 ngày kể từ ngày gửi yêu cầu). Admin nhận thông báo.</summary>
    public async Task<LeadMessageDto> ReportReceivedLeadMessageAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        Guid messageId,
        string? reason,
        CancellationToken ct = default,
        string? detail = null)
    {
        var (trimmedReason, trimmedDetail) = ValidateReportInput(messageId, reason, detail);

        var (lead, centerName) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);
        return await ReportMessageAsync(userId, userRole, lead, centerName, messageId, trimmedReason, trimmedDetail,
            reporterIsCenterSide: true, sharedProfile: lead.SharedFields.Contains(LeadSharedFields.Contact), ct);
    }

    private static (string Reason, string? Detail) ValidateReportInput(Guid messageId, string? reason, string? detail)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("Mã tin nhắn không hợp lệ.");

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length < MinReportReasonLength || trimmedReason.Length > MaxReportReasonLength)
            throw new ArgumentException($"Lý do báo cáo phải từ {MinReportReasonLength} đến {MaxReportReasonLength} ký tự.");

        var trimmedDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        if (trimmedDetail is { Length: > MaxReportDetailLength })
            throw new ArgumentException($"Chi tiết thêm tối đa {MaxReportDetailLength} ký tự.");

        return (trimmedReason, trimmedDetail);
    }

    private async Task<LeadMessageDto> ReportMessageAsync(
        Guid reporterId,
        string reporterRole,
        Lead lead,
        string centerName,
        Guid messageId,
        string reason,
        string? detail,
        bool reporterIsCenterSide,
        bool sharedProfile,
        CancellationToken ct)
    {
        // [UC-143 E3] Chỉ cho báo cáo trong 90 ngày kể từ ngày gửi yêu cầu (kể cả khi lead Đã hoàn tất, Hết hạn hoặc Đã thu hồi)
        if (lead.CreatedAt < DateTime.UtcNow.AddDays(-CenterLeadMapper.DataVisibleDays))
            throw new LeadRuleException("report_expired", 400, "Đã quá 90 ngày kể từ ngày gửi yêu cầu, không thể báo cáo tin nhắn này.");

        var message = await leads.GetLeadMessageForUpdateAsync(lead.Id, messageId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tin nhắn trong yêu cầu liên hệ này.");

        var sender = message.SenderUserId is { } senderId ? await users.GetUserById(senderId, ct) : null;
        var senderIsCenterSide = LeadMessageMapper.IsStaffOrCenterRole(sender?.Role);
        if (senderIsCenterSide == reporterIsCenterSide)
            throw new ArgumentException(reporterIsCenterSide
                ? "Chỉ báo cáo được tin nhắn của học sinh / phụ huynh."
                : "Chỉ báo cáo được tin nhắn của trung tâm.");
        if (string.Equals(message.Status, "removed", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Tin nhắn đã bị gỡ.");

        if (!message.ReportedAt.HasValue)
        {
            message.ReportedByUserId = reporterId;
            message.ReportedAt = DateTime.UtcNow;
            message.ReportReason = reason;
            message.ReportDetail = detail;
            await leads.SaveLeadMessageAsync(message, ct);

            if (auditLogs is not null)
            {
                await auditLogs.AddAsync(new AuditLog
                {
                    ActorUserId = reporterId,
                    Action = "lead_message.report",
                    EntityType = "lead_message",
                    EntityId = message.Id.ToString(),
                    DetailJson = JsonSerializer.Serialize(new
                    {
                        leadId = lead.Id,
                        studyCenterId = lead.StudyCenterId,
                        reporterRole,
                        senderUserId = message.SenderUserId,
                        reason,
                        detail,
                    }),
                }, ct);
            }

            await NotifyAdminsOfReportAsync(centerName, reason, ct);
        }

        return LeadMessageMapper.ToDto(message, sender?.Role ?? UserRoles.Student, sender?.FullName, sharedProfile, centerName);
    }

    private async Task NotifyAdminsOfReportAsync(string centerName, string? reason, CancellationToken ct)
    {
        var adminIds = await leads.GetActiveAdminUserIdsAsync(ct);
        if (adminIds.Count == 0) return;

        var body = $"Có tin nhắn bị báo cáo trong yêu cầu liên hệ với {centerName}." +
                   (reason is null ? string.Empty : $" Lý do: {reason}");
        await leads.AddNotificationsAsync(adminIds.Select(id => new Notification
        {
            UserId = id,
            Type = "lead_message_report",
            Title = "Tin nhắn bị báo cáo",
            Body = body,
            LinkUrl = "/admin/lead-message-reports",
        }).ToList(), ct);
    }

    private static readonly HashSet<string> ReportFilters = new(StringComparer.OrdinalIgnoreCase) { "pending", "resolved", "all" };

    /// <summary>Số tin ngữ cảnh mỗi phía khi Admin chưa cấu hình.</summary>
    public const int DefaultReportContextSize = 3;
    public const int MaxReportContextSize = 10;
    /// <summary>Khóa app_settings chứa số tin ngữ cảnh. Giá trị JSON là một số (3) hoặc đối tượng {"size": 3}.</summary>
    public const string ReportContextSizeSettingKey = "lead_message.report_context_size";

    public const string ReportResolutionRemoved = "removed";
    public const string ReportResolutionWarned = "warned";
    public const string ReportResolutionLocked = "locked";
    public const string ReportResolutionDismissed = "dismissed";

    /// <summary>Đọc số tin ngữ cảnh từ JSON cấu hình; trả null nếu trống hoặc sai định dạng (khi đó dùng mặc định).</summary>
    public static int? ParseReportContextSize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var value = doc.RootElement;
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("size", out var size))
                value = size;
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>[UC-58] Hàng đợi báo cáo cho Admin (quyền do controller kiểm tra). Có viewerAdminId thì ghi nhật ký truy cập.</summary>
    public async Task<PagedResult<AdminLeadMessageReportDto>> GetReportedMessagesAsync(
        string? filter,
        int page,
        int pageSize,
        CancellationToken ct = default,
        Guid? viewerAdminId = null)
    {
        var normalized = filter?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalized) && !ReportFilters.Contains(normalized))
            throw new ArgumentException("Bộ lọc không hợp lệ. Chỉ nhận 'pending', 'resolved' hoặc 'all'.");

        var effectiveFilter = string.IsNullOrEmpty(normalized) ? "pending" : normalized;
        var result = await leads.GetReportedMessagesAsync(effectiveFilter, page, pageSize, ct);

        // Hàng đợi trả nội dung tin bị báo cáo nên mỗi lần đọc đều vào nhật ký truy cập (UC-97)
        if (viewerAdminId is { } adminId && auditLogs is not null && result.Items.Count > 0)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminId,
                Action = "lead_message.report_queue_view",
                EntityType = "lead_message",
                DetailJson = JsonSerializer.Serialize(new { filter = effectiveFilter, page, messageIds = result.Items.Select(i => i.Id) }),
            }, ct);
        }

        return result;
    }

    /// <summary>
    /// [UC-58] Admin đọc tin bị báo cáo kèm một số tin ngữ cảnh liền kề (số lượng lấy từ cấu hình, mặc định
    /// <see cref="DefaultReportContextSize"/> mỗi phía). Admin không đọc toàn bộ cuộc trò chuyện. Mỗi lần đọc ghi nhật ký truy cập.
    /// </summary>
    public async Task<AdminLeadMessageReportContextDto> GetReportContextAsync(
        Guid adminUserId,
        Guid leadId,
        Guid messageId,
        CancellationToken ct = default)
    {
        var message = await GetReportedMessageAsync(leadId, messageId, ct);

        var size = Math.Clamp(await leads.GetReportContextSizeSettingAsync(ct) ?? DefaultReportContextSize, 0, MaxReportContextSize);
        var messages = await leads.GetReportContextMessagesAsync(leadId, messageId, size, ct);

        if (auditLogs is not null)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "lead_message.report_view",
                EntityType = "lead_message",
                EntityId = message.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { leadId, contextSize = size, messageIds = messages.Select(m => m.Id) }),
            }, ct);
        }

        return new AdminLeadMessageReportContextDto(leadId, messageId, size, messages);
    }

    /// <summary>
    /// [UC-58] Admin gỡ riêng tin nhắn vi phạm (không khóa tài khoản). Lý do bắt buộc. Tin chuyển sang "removed",
    /// báo cáo được đánh dấu đã xử lý, ghi nhật ký, thông báo cho người gửi và người báo cáo. Gỡ lại tin đã gỡ không đổi gì.
    /// </summary>
    public async Task RemoveReportedMessageAsync(Guid adminUserId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default)
    {
        var trimmedReason = RequireResolutionReason(reason);
        var message = await GetReportedMessageAsync(leadId, messageId, ct);
        if (string.Equals(message.Status, "removed", StringComparison.OrdinalIgnoreCase)) return;

        message.Status = "removed";
        message.RemovedByUserId = adminUserId;
        message.ReportResolution = ReportResolutionRemoved;
        message.ReportResolvedAt = DateTime.UtcNow;
        await leads.SaveLeadMessageAsync(message, ct);

        await AuditReportResolutionAsync(adminUserId, "lead_message.remove", leadId, message, trimmedReason, ct);
        await NotifyUserAsync(message.SenderUserId, "lead_message_removed", "Tin nhắn của bạn đã bị gỡ",
            $"Một tin nhắn của bạn trong yêu cầu liên hệ đã bị gỡ do vi phạm quy tắc. Lý do: {trimmedReason}", ct);
        await NotifyReporterAsync(message, "Tin nhắn bạn báo cáo đã bị gỡ do vi phạm quy tắc.", ct);
    }

    /// <summary>
    /// [UC-58] Admin cảnh cáo người gửi tin bị báo cáo. Lý do bắt buộc. Tin vẫn hiện, báo cáo được đánh dấu đã xử lý,
    /// ghi nhật ký, thông báo cho người gửi và người báo cáo. Báo cáo đã xử lý rồi thì không cảnh cáo lại.
    /// </summary>
    public async Task WarnReportedSenderAsync(Guid adminUserId, Guid leadId, Guid messageId, string? reason, CancellationToken ct = default)
    {
        var trimmedReason = RequireResolutionReason(reason);
        var message = await GetReportedMessageAsync(leadId, messageId, ct);
        if (message.ReportResolution is not null || string.Equals(message.Status, "removed", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Báo cáo này đã được xử lý.");

        message.ReportResolution = ReportResolutionWarned;
        message.ReportResolvedAt = DateTime.UtcNow;
        await leads.SaveLeadMessageAsync(message, ct);

        await AuditReportResolutionAsync(adminUserId, "lead_message.warn", leadId, message, trimmedReason, ct);
        await NotifyUserAsync(message.SenderUserId, "lead_message_warning", "Cảnh cáo về tin nhắn của bạn",
            $"Một tin nhắn của bạn trong yêu cầu liên hệ bị báo cáo và được xác định là không phù hợp. Lý do: {trimmedReason} Tiếp tục vi phạm có thể dẫn tới khóa tài khoản.", ct);
        await NotifyReporterAsync(message, "Người gửi tin nhắn bạn báo cáo đã bị cảnh cáo.", ct);
    }

    /// <summary>
    /// [UC-58] Gọi sau khi Admin khóa tài khoản người gửi: đánh dấu báo cáo đã xử lý bằng khóa tài khoản và thông báo cho người báo cáo.
    /// </summary>
    public async Task MarkReportResolvedByLockAsync(Guid adminUserId, Guid leadId, Guid messageId, CancellationToken ct = default)
    {
        var message = await leads.GetLeadMessageForUpdateAsync(leadId, messageId, ct);
        if (message is null || !message.ReportedAt.HasValue) return;

        message.ReportResolution = ReportResolutionLocked;
        message.ReportResolvedAt = DateTime.UtcNow;
        await leads.SaveLeadMessageAsync(message, ct);
        await NotifyReporterAsync(message, "Tài khoản gửi tin nhắn bạn báo cáo đã bị khóa và các tin nhắn của tài khoản đó đã bị gỡ.", ct);
    }

    private static string RequireResolutionReason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Vui lòng nhập lý do xử lý.");
        if (trimmed.Length > MaxRemoveReasonLength)
            throw new ArgumentException($"Lý do tối đa {MaxRemoveReasonLength} ký tự.");
        return trimmed;
    }

    /// <summary>Lấy tin (đúng yêu cầu liên hệ) và bắt buộc tin đó đang có báo cáo.</summary>
    private async Task<LeadMessage> GetReportedMessageAsync(Guid leadId, Guid messageId, CancellationToken ct)
    {
        if (leadId == Guid.Empty || messageId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ hoặc mã tin nhắn không hợp lệ.");

        var message = await leads.GetLeadMessageForUpdateAsync(leadId, messageId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tin nhắn trong yêu cầu liên hệ này.");
        if (!message.ReportedAt.HasValue)
            throw new KeyNotFoundException("Tin nhắn này không có báo cáo nào.");
        return message;
    }

    private async Task AuditReportResolutionAsync(Guid adminUserId, string action, Guid leadId, LeadMessage message, string reason, CancellationToken ct)
    {
        if (auditLogs is null) return;
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = action,
            EntityType = "lead_message",
            EntityId = message.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                leadId,
                senderUserId = message.SenderUserId,
                reportedBy = message.ReportedByUserId,
                reportReason = message.ReportReason,
                reason,
            }),
        }, ct);
    }

    // [UC-75 lớp 1b] userId null: người nhận đã bị xóa hẳn, không gửi
    private Task NotifyUserAsync(Guid? userId, string type, string title, string body, CancellationToken ct) =>
        userId is { } id
            ? leads.AddNotificationsAsync([new Notification { UserId = id, Type = type, Title = title, Body = body }], ct)
            : Task.CompletedTask;

    /// <summary>[UC-58] Bước 4 của luồng chính: báo kết quả xử lý cho người đã báo cáo.</summary>
    private Task NotifyReporterAsync(LeadMessage message, string outcome, CancellationToken ct) =>
        NotifyReporterAsync(message.ReportedByUserId, outcome, ct);

    private Task NotifyReporterAsync(Guid? reporterId, string outcome, CancellationToken ct) =>
        reporterId is { } id
            ? NotifyUserAsync(id, "lead_message_report_result", "Kết quả xử lý báo cáo tin nhắn", outcome, ct)
            : Task.CompletedTask;

    /// <summary>[UC-58] Admin xem xét thấy không vi phạm: bỏ báo cáo, tin vẫn hiện, ghi nhật ký. Tin chưa bị báo cáo thì không đổi gì.</summary>
    public async Task DismissReportAsync(Guid adminUserId, Guid leadId, Guid messageId, CancellationToken ct = default)
    {
        if (leadId == Guid.Empty || messageId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ hoặc mã tin nhắn không hợp lệ.");

        var message = await leads.GetLeadMessageForUpdateAsync(leadId, messageId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tin nhắn trong yêu cầu liên hệ này.");

        if (!message.ReportedAt.HasValue) return;
        if (message.ReportResolution is not null) return;

        var reportedBy = message.ReportedByUserId;
        var reportReason = message.ReportReason;
        // [UC-54] Giữ nguyên lịch sử báo cáo để đối chiếu audit, chỉ đánh dấu đã bác bỏ (dismissed)
        message.ReportResolution = ReportResolutionDismissed;
        message.ReportResolvedAt = DateTime.UtcNow;
        await leads.SaveLeadMessageAsync(message, ct);

        if (auditLogs is not null)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "lead_message.report_dismiss",
                EntityType = "lead_message",
                EntityId = message.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { leadId, senderUserId = message.SenderUserId, reportedBy, reportReason }),
            }, ct);
        }

        await NotifyReporterAsync(reportedBy, "Báo cáo tin nhắn của bạn đã được xem xét và không được xác định là vi phạm.", ct);
    }

    /// <summary>[UC-58] Id người gửi của tin bị báo cáo, để Admin khóa tài khoản đó.</summary>
    public async Task<Guid> GetMessageSenderIdAsync(Guid leadId, Guid messageId, CancellationToken ct = default)
    {
        if (leadId == Guid.Empty || messageId == Guid.Empty)
            throw new ArgumentException("Mã yêu cầu liên hệ hoặc mã tin nhắn không hợp lệ.");

        var message = await leads.GetLeadMessageForUpdateAsync(leadId, messageId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tin nhắn trong yêu cầu liên hệ này.");
        // [UC-75 lớp 1b] Người gửi đã bị xóa hẳn: không còn tài khoản để khóa
        return message.SenderUserId ?? throw new ArgumentException("Người gửi tin này đã xóa tài khoản.");
    }

    /// <summary>[UC-58] Sau khi Admin khóa tài khoản: gỡ toàn bộ tin nhắn của tài khoản đó và ghi nhật ký.</summary>
    public async Task<int> RemoveAllMessagesOfLockedSenderAsync(
        Guid adminUserId,
        Guid senderUserId,
        string? reason,
        CancellationToken ct = default)
    {
        var removed = await leads.RemoveAllMessagesOfUserAsync(senderUserId, adminUserId, ct);

        if (auditLogs is not null)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = "lead_message.remove_all_of_locked_user",
                EntityType = "user",
                EntityId = senderUserId.ToString(),
                DetailJson = JsonSerializer.Serialize(new { removedCount = removed, reason }),
            }, ct);
        }

        return removed;
    }
}
