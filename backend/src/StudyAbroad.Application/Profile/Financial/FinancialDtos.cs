using System.ComponentModel.DataAnnotations;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Profile.Financial;

/// <summary>
/// [USAS-364] DTO hiển thị thông tin tài chính của học sinh.
/// </summary>
public record FinancialProfileDto(
    Guid Id,
    Guid UserId,
    decimal AnnualBudget,
    string FundingSource,
    bool NeedScholarship,
    decimal? MaxExpectedTuition,
    string Currency,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// [USAS-364] Request DTO để tạo mới hoặc cập nhật hồ sơ tài chính (Upsert).
/// Có kiểm tra biên (Validation constraints) chống dữ liệu phi thực tế.
/// </summary>
public record SaveFinancialProfileRequest(
    [Range(0, (double)ProfileConstants.ValidationLimits.MaxAnnualBudget, ErrorMessage = "Ngân sách hàng năm phải từ 0 đến 10,000,000 USD.")]
    decimal AnnualBudget,

    [Required(ErrorMessage = "Vui lòng chọn nguồn tài chính.")]
    [StringLength(50, ErrorMessage = "Nguồn tài chính không vượt quá 50 ký tự.")]
    string FundingSource,

    bool NeedScholarship,

    [Range(0, (double)ProfileConstants.ValidationLimits.MaxAnnualBudget, ErrorMessage = "Mức học phí mong muốn phải hợp lệ.")]
    decimal? MaxExpectedTuition = null,

    [StringLength(10, ErrorMessage = "Đơn vị tiền tệ không vượt quá 10 ký tự.")]
    string Currency = "USD",

    [StringLength(ProfileConstants.ValidationLimits.MaxDescriptionLength, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    string? Notes = null
);

/// <summary>
/// [USAS-364] DTO hoạt động ngoại khóa.
/// </summary>
public record ExtracurricularActivityDto(
    Guid Id,
    Guid UserId,
    string ActivityName,
    string Role,
    string Organization,
    int? DurationMonths,
    string? StartDate,
    string? EndDate,
    bool IsOngoing,
    int ImpactLevel,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// [USAS-364] Request DTO để thêm hoạt động ngoại khóa mới.
/// </summary>
public record CreateExtracurricularRequest(
    [Required(ErrorMessage = "Tên hoạt động không được để trống.")]
    [StringLength(ProfileConstants.ValidationLimits.MaxActivityNameLength, MinimumLength = 2, ErrorMessage = "Tên hoạt động từ 2 đến 150 ký tự.")]
    string ActivityName,

    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [StringLength(ProfileConstants.ValidationLimits.MaxRoleLength, ErrorMessage = "Vai trò không vượt quá 100 ký tự.")]
    string Role,

    [StringLength(ProfileConstants.ValidationLimits.MaxOrganizationLength, ErrorMessage = "Tổ chức không vượt quá 150 ký tự.")]
    string? Organization = null,

    [Range(1, 120, ErrorMessage = "Thời gian tham gia từ 1 đến 120 tháng.")]
    int? DurationMonths = null,

    string? StartDate = null,
    string? EndDate = null,
    bool IsOngoing = false,

    [Range(1, 5, ErrorMessage = "Mức độ ảnh hưởng phải từ 1 (Trường) đến 5 (Quốc tế).")]
    int ImpactLevel = 1,

    [StringLength(ProfileConstants.ValidationLimits.MaxDescriptionLength, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    string? Description = null
);

/// <summary>
/// [USAS-364] Request DTO để cập nhật hoạt động ngoại khóa.
/// </summary>
public record UpdateExtracurricularRequest(
    [Required(ErrorMessage = "Tên hoạt động không được để trống.")]
    [StringLength(ProfileConstants.ValidationLimits.MaxActivityNameLength, MinimumLength = 2, ErrorMessage = "Tên hoạt động từ 2 đến 150 ký tự.")]
    string ActivityName,

    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [StringLength(ProfileConstants.ValidationLimits.MaxRoleLength, ErrorMessage = "Vai trò không vượt quá 100 ký tự.")]
    string Role,

    [StringLength(ProfileConstants.ValidationLimits.MaxOrganizationLength, ErrorMessage = "Tổ chức không vượt quá 150 ký tự.")]
    string? Organization = null,

    [Range(1, 120, ErrorMessage = "Thời gian tham gia từ 1 đến 120 tháng.")]
    int? DurationMonths = null,

    string? StartDate = null,
    string? EndDate = null,
    bool IsOngoing = false,

    [Range(1, 5, ErrorMessage = "Mức độ ảnh hưởng phải từ 1 đến 5.")]
    int ImpactLevel = 1,

    [StringLength(ProfileConstants.ValidationLimits.MaxDescriptionLength, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    string? Description = null
);

/// <summary>
/// [USAS-364] DTO giải thưởng / thành tích.
/// </summary>
public record StudentAchievementDto(
    Guid Id,
    Guid UserId,
    string Category,
    string Title,
    string? Issuer,
    string? IssueDate,
    string Description,
    DateTime CreatedAt
);

/// <summary>
/// [USAS-364] Request DTO để tạo mới giải thưởng / thành tích.
/// </summary>
public record CreateAchievementRequest(
    [Required(ErrorMessage = "Phân loại thành tích không được để trống.")]
    string Category,

    [Required(ErrorMessage = "Tiêu đề thành tích không được để trống.")]
    [StringLength(ProfileConstants.ValidationLimits.MaxTitleLength, MinimumLength = 2, ErrorMessage = "Tiêu đề từ 2 đến 150 ký tự.")]
    string Title,

    [StringLength(ProfileConstants.ValidationLimits.MaxOrganizationLength, ErrorMessage = "Đơn vị cấp không vượt quá 150 ký tự.")]
    string? Issuer = null,

    string? IssueDate = null,

    [StringLength(ProfileConstants.ValidationLimits.MaxDescriptionLength, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    string? Description = null
);

/// <summary>
/// [USAS-364] DTO tổng quan toàn bộ hồ sơ phi học thuật của học sinh.
/// Dùng để AI Advisor đọc nhanh ở Bước B2-B4 hoặc trang tổng quan hồ sơ.
/// </summary>
public record StudentProfileSummaryDto(
    Guid UserId,
    FinancialProfileDto? Financial,
    IReadOnlyList<ExtracurricularActivityDto> Activities,
    IReadOnlyList<StudentAchievementDto> Achievements,
    int TotalActivitiesCount,
    int TotalAchievementsCount,
    int MaxImpactLevel
);
