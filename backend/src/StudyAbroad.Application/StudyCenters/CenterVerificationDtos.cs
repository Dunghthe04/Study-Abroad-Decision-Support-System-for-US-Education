namespace StudyAbroad.Application.StudyCenters;

public record RegisterCenterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string CenterName,
    string Phone,
    string RegistrantName,
    string? RegistrantPosition = null,
    bool AgreeTerms = false,
    bool AgreeTruthfulnessCommitment = false);

public record VerifyCenterEmailRequest(string Email, string Code);

/// <summary>Yêu cầu gửi OTP tới số điện thoại / email liên lạc MỚI của trung tâm.</summary>
public record RequestContactChangeDto(string NewValue);

/// <summary>Đổi số điện thoại liên lạc (không cần OTP).</summary>
public record ChangeContactPhoneDto(string NewValue);

/// <summary>Xác nhận đổi số điện thoại / email liên lạc bằng OTP đã gửi tới giá trị mới.</summary>
public record ConfirmContactChangeDto(string NewValue, string Code);

public record VerificationDocumentUploadDto(
    string DocumentType,
    string FileUrl,
    string FileName,
    long FileSize,
    string ContentType);

public record SubmitVerificationDto(
    string LegalName,
    string TaxCode,
    string Address,
    string LegalRepresentative,
    string? Website = null,
    List<VerificationDocumentUploadDto>? Documents = null);

public record CenterVerificationDocumentDto(
    Guid Id,
    Guid RequestId,
    string DocumentType,
    string FileUrl,
    string FileName,
    long FileSize,
    string ContentType,
    DateTime UploadedAt);

public record CenterVerificationDto(
    Guid Id,
    Guid CenterId,
    Guid RequestedByUserId,
    string LegalName,
    string TaxCode,
    string Address,
    string LegalRepresentative,
    string? Website,
    string Status,
    DateTime SubmittedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? RejectionReason,
    string? ReviewNotes,
    bool IsNationalRegistryMatched,
    bool IsBusinessOperating,
    bool IsRepresentativeValid,
    List<CenterVerificationDocumentDto> Documents,
    string? CenterName = null,
    string? CenterCode = null,
    string? CenterEmail = null,
    string? CenterPhone = null,
    DateTime? EmailVerifiedAt = null,
    DateTime? PhoneVerifiedAt = null);

public record ReviewCenterVerificationRequest(
    string Action,
    bool IsNationalRegistryMatched = false,
    bool IsBusinessOperating = false,
    bool IsRepresentativeValid = false,
    string? ReviewNotes = null,
    string? RejectionReason = null);

public record UpdateCenterProfileRequest(
    string? Name = null,
    string? Description = null,
    string? Website = null,
    string? Address = null,
    string? City = null,
    string? District = null,
    List<string>? Services = null,
    List<string>? StudyLevels = null,
    string? CenterType = null,
    int? EstablishedYear = null,
    int? StaffCount = null,
    decimal? FeeMin = null,
    decimal? FeeMax = null,
    string? FeeDescription = null,
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    // Thông tin pháp lý: chỉ sửa được khi trung tâm CHƯA được xác minh.
    // Null = giữ nguyên; chuỗi rỗng = xóa giá trị.
    string? TaxCode = null,
    string? LegalRepresentative = null,
    string? LicenseNumber = null,
    string? LicenseIssuedBy = null,
    DateOnly? LicenseIssuedDate = null,
    string? LegalComplianceMode = null,
    string? TrainingLicenseNumber = null,
    int? CertifiedCounselorCount = null);

public record CenterVerificationStatusDto(
    string VerificationStatus,
    bool IsEmailVerified,
    DateTime? EmailVerifiedAt,
    bool IsPhoneVerified,
    DateTime? PhoneVerifiedAt,
    CenterVerificationDto? CurrentRequest,
    string? RejectionReason);
