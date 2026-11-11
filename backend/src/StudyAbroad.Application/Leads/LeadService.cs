using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
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
    IAuditLogRepository? auditLogs = null) : ILeadService
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
        "new", "contacted", "closed"
    };

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
        if (lead is null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        // [Chống IDOR] Trung tâm chỉ được đọc/trả lời các yêu cầu gửi tới đúng trung tâm mình
        if (centerId.HasValue && lead.StudyCenterId != centerId.Value)
            throw new UnauthorizedAccessException("Bạn không có quyền thao tác với yêu cầu liên hệ của trung tâm khác.");

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
        if (submitter.Role == UserRoles.Student)
        {
            if (AgeFeatures.Check(AgeFeatures.ShareWithCenter, submitter.Role, submitter.DateOfBirth, today, hasParentConsent: false) is not null)
                throw new LeadRuleException("age_restricted", 403,
                    "Học sinh dưới 16 tuổi chưa được tự gửi yêu cầu liên hệ. Phụ huynh có thể gửi thay từ tài khoản của mình.");

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
        var consent = new Consent
        {
            UserId = studentId,
            Purpose = AgeFeatures.ShareWithCenter,
            Granted = true,
            PolicyVersion = "v1.0",
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
            Status = "new",
        };

        var created = await leads.CreateLeadAsync(lead, consent, ct);
        return (await leads.GetDtoByIdAsync(created.Id, ct))!;
    }

    public async Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default)
    {
        var lead = await leads.GetByIdAsync(leadId, ct);
        // [UC-42] Phụ huynh gửi thay con vẫn xem được lead mình đã gửi
        if (lead is null || (lead.UserId != userId && lead.SubmittedByUserId != userId))
            return null;

        return await leads.GetDtoByIdAsync(leadId, ct);
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
            throw new ArgumentException("Trạng thái không hợp lệ. Chỉ chấp nhận 'new', 'contacted', hoặc 'closed'.");

        var (centerId, _) = await ResolveCenterScopeAsync(userId, userEmail, userRole, null, ct);

        var lead = await leads.GetLeadForUpdateAsync(leadId, ct);
        if (lead is null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

        if (centerId.HasValue && lead.StudyCenterId != centerId.Value)
            throw new UnauthorizedAccessException("Bạn không có quyền cập nhật yêu cầu liên hệ của trung tâm khác.");

        lead.Status = normalizedStatus;
        await leads.UpdateAsync(lead, ct);

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
        // [UC-42] Hiện tên khi chia sẻ nhóm contact, cùng quy tắc với CenterLeadMapper
        var showName = lead.SharedFields.Contains(LeadSharedFields.Contact);
        var messages = await leads.GetLeadMessagesAsync(lead.Id, lead.UserId, showName, centerName, ct);
        var flaggedCount = messages.Count(m => m.IsFlagged && m.Status != "removed");

        return new LeadMessagesThreadDto(lead.Id, lead.Status, messages, flaggedCount);
    }

    /// <summary>
    /// [USAS-2053 / UC-74] Trung tâm tư vấn gửi tin nhắn trả lời trong yêu cầu liên hệ.
    /// Áp dụng cùng bộ lọc nội dung (gắn cờ số điện thoại/quảng cáo) và tự chuyển trạng thái lead "new" -> "contacted".
    /// </summary>
    public async Task<SendLeadMessageResultDto> SendLeadMessageForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        string content,
        CancellationToken ct = default)
    {
        var trimmedContent = content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedContent))
            throw new ArgumentException("Nội dung tin nhắn không được để trống.");

        if (trimmedContent.Length > MaxReplyMessageLength)
            throw new ArgumentException($"Nội dung tin nhắn tối đa {MaxReplyMessageLength} ký tự.");

        var (lead, centerName) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);

        // [UC-74 / UC-44] Áp dụng cùng bộ lọc: phát hiện số điện thoại và quảng cáo để gắn cờ kiểm duyệt
        var filterResult = LeadMessageContentFilter.Inspect(trimmedContent);

        var entity = new LeadMessage
        {
            LeadId = lead.Id,
            SenderUserId = userId,
            Content = trimmedContent,
            IsFlagged = filterResult.IsFlagged,
            FlagReason = filterResult.FlagReason,
            Status = "visible",
            RemovedByUserId = null,
        };

        Lead? leadToUpdate = null;
        if (string.Equals(lead.Status, "new", StringComparison.OrdinalIgnoreCase))
        {
            lead.Status = "contacted";
            lead.UpdatedAt = DateTime.UtcNow;
            leadToUpdate = lead;
        }

        var saved = await leads.AddLeadMessageAsync(entity, leadToUpdate, ct);

        if (auditLogs is not null)
        {
            var detailJson = JsonSerializer.Serialize(new
            {
                leadId = lead.Id,
                studyCenterId = lead.StudyCenterId,
                isFlagged = saved.IsFlagged,
                flagReason = saved.FlagReason,
                contentLength = saved.Content.Length,
                leadStatus = lead.Status,
            });

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = saved.IsFlagged ? "lead_message.send_flagged" : "lead_message.send",
                EntityType = "lead_message",
                EntityId = saved.Id.ToString(),
                DetailJson = detailJson,
            }, ct);
        }

        var dto = LeadMessageMapper.ToDto(
            saved,
            userRole,
            senderFullName: null,
            sharedProfile: lead.SharedFields.Contains(LeadSharedFields.Contact),
            centerName: centerName);

        return new SendLeadMessageResultDto(dto, lead.Status);
    }
}

