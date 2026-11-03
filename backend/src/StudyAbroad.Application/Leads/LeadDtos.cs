namespace StudyAbroad.Application.Leads;

public record CreateLeadRequest(
    Guid StudyCenterId,
    string? StudyLevel = null,
    string? ContactPhone = null,
    string? Message = null,
    bool SharedProfile = false);

public record LeadDto(
    Guid Id,
    Guid UserId,
    Guid StudyCenterId,
    string? StudyCenterName,
    Guid? ConsentId,
    string? StudyLevel,
    string? ContactPhone,
    string? Message,
    bool SharedProfile,
    string Status,
    DateTime CreatedAt);

/// <summary>[UC-44] Query phân trang và lọc trạng thái danh sách yêu cầu tư vấn.</summary>
public record LeadQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20);

