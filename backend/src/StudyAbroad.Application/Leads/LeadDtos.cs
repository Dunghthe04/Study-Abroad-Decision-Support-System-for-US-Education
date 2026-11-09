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

/// <summary>
/// [UC-73] Tóm tắt hồ sơ học sinh được chia sẻ cho trung tâm (chỉ khi SharedProfile = true).
/// Tuân thủ quy định bảo vệ dữ liệu trẻ em và quyền riêng tư (Nghị định 356/2025/NĐ-CP).
/// </summary>
public record SharedProfileSummaryDto(
    decimal? OverallGpa,
    string? TargetLevel,
    string? IntendedMajor,
    string? EducationSystem,
    string? CurrentGrade,
    decimal? EnglishScore,
    string? EnglishTestType,
    decimal? SatScore,
    decimal? ActScore,
    int? ExtracurricularTier,
    string? BudgetTier);

/// <summary>
/// [UC-73] Chi tiết yêu cầu liên hệ nhận được phía trung tâm tư vấn.
/// Nếu SharedProfile = false: StudentName, StudentEmail và ProfileSummary hoàn toàn là null.
/// </summary>
public record CenterReceivedLeadDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    string? StudyLevel,
    string? ContactPhone,
    string? Message,
    bool SharedProfile,
    string Status,
    DateTime CreatedAt,
    string? StudentName,
    string? StudentEmail,
    SharedProfileSummaryDto? ProfileSummary);

/// <summary>
/// [UC-73] Danh sách phân trang kèm bộ đếm trạng thái dành cho cổng trung tâm.
/// </summary>
public record CenterLeadsSummaryDto(
    IReadOnlyList<CenterReceivedLeadDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int NewCount,
    int ContactedCount,
    int ClosedCount);

/// <summary>
/// [UC-73] Yêu cầu cập nhật trạng thái liên hệ (new | contacted | closed).
/// </summary>
public record UpdateLeadStatusRequest(string Status);

