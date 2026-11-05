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

