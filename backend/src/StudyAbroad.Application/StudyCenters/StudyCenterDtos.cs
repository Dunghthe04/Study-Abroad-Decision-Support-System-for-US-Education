namespace StudyAbroad.Application.StudyCenters;
public record CenterReferralQuery(string? Service = null, string? StudyLevel = null, string? City = null);

public record StudyCenterDto(
    Guid Id,
    string Code,
    string Name,
    string? Website,
    string? Address,
    string? City,
    IReadOnlyList<string> StudyLevels,
    IReadOnlyList<string> Services,
    DateOnly? SurveyedAt,
    string? Phone = null,
    string? Email = null,
    string? Description = null,
    string? VerificationStatus = "unverified",
    bool IsActive = true,
    string CenterType = "study_abroad",
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    IReadOnlyList<string>? GalleryImages = null,
    string? District = null,
    double? Latitude = null,
    double? Longitude = null,
    int? StaffCount = null,
    int? EstablishedYear = null,
    string? LicenseNumber = null,
    IReadOnlyList<string>? Accreditations = null,
    double? VisaSuccessRate = null,
    decimal AverageRating = 5.0m,
    int ReviewCount = 0,
    DateTime? VerifiedAt = null,
    Guid? VerifiedByUserId = null);

/// <summary>[UC-59] Moderator / Admin gửi yêu cầu xác minh hoặc hủy xác minh trung tâm tư vấn.</summary>
public record VerifyStudyCenterRequest(bool IsVerified);

/// <summary>[UC-59] Kết quả thực hiện xác minh trung tâm tư vấn.</summary>
public record VerifyStudyCenterResult(StudyCenterDto? Center, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && Center is not null;
    public static VerifyStudyCenterResult Ok(StudyCenterDto center) => new(center);
    public static VerifyStudyCenterResult Fail(string error) => new(null, error);
}

/// <summary>[UC-72] Moderator / Admin sửa thông tin một trung tâm tư vấn trong danh bạ.</summary>
public record UpdateStudyCenterRequest(
    string Name,
    string? Website = null,
    string? Address = null,
    string? City = null,
    List<string>? StudyLevels = null,
    List<string>? Services = null,
    string? Phone = null,
    string? Email = null,
    string? Description = null,
    string? CenterType = null,
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    List<string>? GalleryImages = null,
    string? District = null,
    double? Latitude = null,
    double? Longitude = null,
    int? StaffCount = null,
    int? EstablishedYear = null,
    string? LicenseNumber = null,
    List<string>? Accreditations = null,
    double? VisaSuccessRate = null);

/// <summary>[UC-72] Kết quả sửa thông tin trung tâm tư vấn.</summary>
public record UpdateStudyCenterResult(StudyCenterDto? Center, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && Center is not null;
    public static UpdateStudyCenterResult Ok(StudyCenterDto center) => new(center);
    public static UpdateStudyCenterResult Fail(string error) => new(null, error);
}

/// <summary>[UC-72] Moderator / Admin thay đổi trạng thái hoạt động (ẩn hoặc hiện khỏi gợi ý).</summary>
public record SetStudyCenterActiveRequest(bool IsActive);

/// <summary>[UC-72] Kết quả thay đổi trạng thái ẩn/hiện trung tâm tư vấn.</summary>
public record SetStudyCenterActiveResult(StudyCenterDto? Center, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && Center is not null;
    public static SetStudyCenterActiveResult Ok(StudyCenterDto center) => new(center);
    public static SetStudyCenterActiveResult Fail(string error) => new(null, error);
}

/// <summary>[UC-71] Moderator / Admin thêm trung tâm tư vấn vào danh bạ.</summary>
public record CreateStudyCenterRequest(
    string Code,
    string Name,
    string? Website = null,
    string? Address = null,
    string? City = null,
    List<string>? StudyLevels = null,
    List<string>? Services = null,
    string? Phone = null,
    string? Email = null,
    string? Description = null,
    DateOnly? SurveyedAt = null,
    string CenterType = "study_abroad",
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    List<string>? GalleryImages = null,
    string? District = null,
    double? Latitude = null,
    double? Longitude = null,
    int? StaffCount = null,
    int? EstablishedYear = null,
    string? LicenseNumber = null,
    List<string>? Accreditations = null,
    double? VisaSuccessRate = null);

public record StudyCenterQuery(
    string? Search = null,
    string? City = null,
    string? Service = null,
    string? StudyLevel = null,
    int Page = 1,
    int PageSize = 20);
