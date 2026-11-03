using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [UC-42 / UC-44] Dịch vụ gửi và quản lý danh sách yêu cầu tư vấn tới Trung tâm du học.
/// </summary>
public class LeadService(
    ILeadRepository leads,
    IStudyCenterRepository studyCenters) : ILeadService
{
    public const int MaxMessageLength = 2000;
    public const int MaxPhoneLength = 32;

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

    public async Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var normalizedQuery = query with
        {
            Page = page,
            PageSize = pageSize,
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim()
        };

        return await leads.GetMyLeadsAsync(userId, normalizedQuery, ct);
    }
}

