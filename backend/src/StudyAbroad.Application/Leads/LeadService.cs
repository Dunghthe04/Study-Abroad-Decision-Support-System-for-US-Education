using System.Text.Json;
using StudyAbroad.Application.Audit;
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
    IAuditLogRepository? auditLogs = null) : ILeadService
{
    public const int MaxMessageLength = 2000;
    public const int MaxReplyMessageLength = 4000;
    public const int MaxPhoneLength = 32;

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

        var center = await studyCenters.GetByIdAsync(request.StudyCenterId, ct);
        if (center is null || !center.IsActive)
            throw new KeyNotFoundException("Trung tâm tư vấn không tồn tại hoặc đã tạm dừng hoạt động.");

        var message = request.Message?.Trim();
        if (!string.IsNullOrEmpty(message) && message.Length > MaxMessageLength)
            throw new ArgumentException($"Lời nhắn tối đa {MaxMessageLength} ký tự.");

        var phone = request.ContactPhone?.Trim();
        if (!string.IsNullOrEmpty(phone) && phone.Length > MaxPhoneLength)
            throw new ArgumentException($"Số điện thoại tối đa {MaxPhoneLength} ký tự.");

        Consent? consent = null;
        if (request.SharedProfile)
        {
            // [Q11 / UC-42] Ghi vết đồng ý riêng cho mục đích share_with_center (Nghị định 356/2025/NĐ-CP)
            consent = new Consent
            {
                UserId = userId,
                Purpose = "share_with_center",
                Granted = true,
                PolicyVersion = "v1.0",
                GrantedIp = clientIp,
            };
        }

        var lead = new Lead
        {
            UserId = userId,
            StudyCenterId = request.StudyCenterId,
            StudyLevel = request.StudyLevel?.Trim(),
            ContactPhone = phone,
            Message = message,
            SharedProfile = request.SharedProfile,
            Status = "new",
        };

        var created = await leads.CreateLeadAsync(lead, consent, ct);
        return (await leads.GetDtoByIdAsync(created.Id, ct))!;
    }

    public async Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default)
    {
        var lead = await leads.GetByIdAsync(leadId, ct);
        if (lead is null || lead.UserId != userId)
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
    /// Chặn IDOR theo trung tâm sở hữu và ẩn danh tên người gửi nếu học sinh chưa bật SharedProfile.
    /// </summary>
    public async Task<LeadMessagesThreadDto> GetReceivedLeadMessagesForCenterUserAsync(
        Guid userId,
        string? userEmail,
        string userRole,
        Guid leadId,
        CancellationToken ct = default)
    {
        var (lead, centerName) = await GetAuthorizedLeadForCenterAsync(userId, userEmail, userRole, leadId, ct);
        var messages = await leads.GetLeadMessagesAsync(lead.Id, lead.UserId, lead.SharedProfile, centerName, ct);
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
            sharedProfile: lead.SharedProfile,
            centerName: centerName);

        return new SendLeadMessageResultDto(dto, lead.Status);
    }
}

