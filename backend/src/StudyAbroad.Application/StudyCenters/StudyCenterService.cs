using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System.Text.Json;

namespace StudyAbroad.Application.StudyCenters;

public record CreateStudyCenterResult(StudyCenterDto? Center, string? ErrorMessage = null)
{
    public bool Success => ErrorMessage is null && Center is not null;
    public static CreateStudyCenterResult Ok(StudyCenterDto center) => new(center);
    public static CreateStudyCenterResult Fail(string message) => new(null, message);
}

public interface IStudyCenterService
{
    Task<PagedResult<StudyCenterDto>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default);
    Task<StudyCenterDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudyCenterDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCenterDto>> ReferAsync(CenterReferralQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetReferralCitiesAsync(CancellationToken ct = default);
    /// <summary>[UC-71] Thêm một trung tâm tư vấn vào danh bạ (kèm ghi audit).</summary>
    Task<CreateStudyCenterResult> CreateAsync(CreateStudyCenterRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-59] Moderator / Admin xác minh hoặc hủy xác minh một trung tâm tư vấn.</summary>
    Task<VerifyStudyCenterResult> VerifyCenterAsync(Guid id, bool isVerified, Guid actorUserId, string? verificationNote = null, CancellationToken ct = default);
    /// <summary>[UC-72] Moderator / Admin sửa thông tin một trung tâm tư vấn trong danh bạ.</summary>
    Task<UpdateStudyCenterResult> UpdateCenterAsync(Guid id, UpdateStudyCenterRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-72] Moderator / Admin thay đổi trạng thái hoạt động (ẩn hoặc hiện khỏi gợi ý).</summary>
    Task<SetStudyCenterActiveResult> SetCenterActiveAsync(Guid id, bool isActive, Guid actorUserId, CancellationToken ct = default);

    // --- Quy trình Tự đăng ký & Xác minh pháp lý (UC-71 mới, UC-59 mới, UC-92) ---
    Task<CenterVerificationStatusDto> GetVerificationStatusAsync(Guid userId, CancellationToken ct = default);
    Task<CreateStudyCenterResult> SubmitVerificationAsync(Guid userId, SubmitVerificationDto request, CancellationToken ct = default);
    Task<PagedResult<CenterVerificationDto>> GetVerificationRequestsAsync(CenterVerificationQuery query, CancellationToken ct = default);
    Task<CenterVerificationDto?> GetVerificationRequestByIdAsync(Guid requestId, CancellationToken ct = default);
    Task<CenterVerificationDocument?> GetVerificationDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<VerifyStudyCenterResult> ReviewVerificationAsync(Guid requestId, ReviewCenterVerificationRequest request, Guid reviewerUserId, CancellationToken ct = default);
    Task<UpdateStudyCenterResult> UpdateCenterProfileAsync(Guid userId, UpdateCenterProfileRequest request, CancellationToken ct = default);
}

public class StudyCenterService(
    IStudyCenterRepository repository,
    IAuditLogRepository auditLogs,
    ICenterVerificationRepository? verificationRepository = null) : IStudyCenterService
{
    private const int MaxPageSize = 100;
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 50;
    public const int MaxTaxCodeLength = 32;
    public const int MaxLegalRepresentativeLength = 150;
    public const int MaxLicenseNumberLength = 128;
    public const int MaxLicenseIssuedByLength = 256;
    public const int MaxVerificationNoteLength = 1000;
    public const int MaxLegalDocumentUrlLength = 1024;
    public const string ComplianceModeLicensed = "licensed";
    public const string ComplianceModeNotified = "notified";

    private sealed record NormalizedLegalFields(
        string? LicenseNumber,
        string? TaxCode,
        string? LegalRepresentative,
        string? LicenseIssuedBy,
        DateOnly? LicenseIssuedDate,
        string LegalComplianceMode,
        string? TrainingLicenseNumber,
        int? CertifiedCounselorCount,
        List<string>? LegalDocumentUrls,
        string? VerificationNote,
        bool HasVerificationNote);

    public async Task<CreateStudyCenterResult> CreateAsync(CreateStudyCenterRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
            return CreateStudyCenterResult.Fail("Tên trung tâm không được để trống.");
        if (name.Length > MaxNameLength)
            return CreateStudyCenterResult.Fail($"Tên trung tâm tối đa {MaxNameLength} ký tự.");

        if (string.IsNullOrEmpty(code))
            return CreateStudyCenterResult.Fail("Mã trung tâm không được để trống.");
        if (code.Length > MaxCodeLength)
            return CreateStudyCenterResult.Fail($"Mã trung tâm tối đa {MaxCodeLength} ký tự.");

        if (await repository.ExistsByCodeAsync(code, ct))
            return CreateStudyCenterResult.Fail($"Mã trung tâm '{code}' đã tồn tại trong hệ thống.");

        var (legal, legalError) = ValidateLegalFields(
            staffCount: request.StaffCount,
            licenseNumber: request.LicenseNumber,
            taxCode: request.TaxCode,
            legalRepresentative: request.LegalRepresentative,
            licenseIssuedBy: request.LicenseIssuedBy,
            licenseIssuedDate: request.LicenseIssuedDate,
            legalComplianceMode: request.LegalComplianceMode,
            fallbackComplianceMode: ComplianceModeLicensed,
            trainingLicenseNumber: request.TrainingLicenseNumber,
            certifiedCounselorCount: request.CertifiedCounselorCount,
            legalDocumentUrls: request.LegalDocumentUrls,
            verificationNote: request.VerificationNote,
            defaultEmptyDocs: true);

        if (legalError is not null || legal is null)
            return CreateStudyCenterResult.Fail(legalError ?? "Thông tin pháp lý không hợp lệ.");

        var center = new StudyCenter
        {
            Code = code,
            Name = name,
            Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            StudyLevels = request.StudyLevels?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? [],
            Services = request.Services?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? [],
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SurveyedAt = request.SurveyedAt,
            VerificationStatus = "unverified",
            IsActive = true,
            CenterType = string.IsNullOrWhiteSpace(request.CenterType) ? "study_abroad" : request.CenterType.Trim(),
            LogoUrl = request.LogoUrl,
            CoverImageUrl = request.CoverImageUrl,
            GalleryImages = request.GalleryImages ?? [],
            District = request.District,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            StaffCount = request.StaffCount,
            EstablishedYear = request.EstablishedYear,
            Accreditations = request.Accreditations ?? [],
            VisaSuccessRate = request.VisaSuccessRate,
        };

        ApplyLegalFields(center, legal);

        await repository.AddAsync(center, ct);

        var detailJson = JsonSerializer.Serialize(new
        {
            code = center.Code,
            name = center.Name,
            city = center.City,
            taxCode = center.TaxCode,
            legalComplianceMode = center.LegalComplianceMode,
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "study_center.create",
            EntityType = "study_center",
            EntityId = center.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        return CreateStudyCenterResult.Ok(ToDto(center));
    }

    public async Task<PagedResult<StudyCenterDto>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };

        var result = await repository.SearchAsync(normalized, ct);
        return new PagedResult<StudyCenterDto>(
            result.Items.Select(ToDto).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<StudyCenterDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var center = await repository.GetByIdAsync(id, ct);
        return center is null ? null : ToDto(center);
    }

    // Tối đa 3 trung tâm cho chat gợi ý (B6); luật chọn nằm ở CenterReferral
    public async Task<IReadOnlyList<StudyCenterDto>> ReferAsync(CenterReferralQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            throw new ArgumentException("Service is required.", nameof(query));
        var centers = await repository.ListReferableAsync(ct);
        return CenterReferral.Pick(centers, query.Service, query.StudyLevel, query.City).Select(ToDto).ToList();
    }

    public async Task<VerifyStudyCenterResult> VerifyCenterAsync(Guid id, bool isVerified, Guid actorUserId, string? verificationNote = null, CancellationToken ct = default)
    {
        var trimmedNote = TrimOrNull(verificationNote);
        if (trimmedNote is { Length: > MaxVerificationNoteLength })
            return VerifyStudyCenterResult.Fail($"Ghi chú xác minh tối đa {MaxVerificationNoteLength} ký tự.");

        var center = await repository.GetByIdForUpdateAsync(id, ct);
        if (center is null)
            return VerifyStudyCenterResult.Fail("Không tìm thấy trung tâm tư vấn trong hệ thống.");

        var previousStatus = center.VerificationStatus;
        var newStatus = isVerified ? CenterReferral.Verified : "unverified";

        center.VerificationStatus = newStatus;
        center.VerifiedAt = isVerified ? DateTime.UtcNow : null;
        center.VerifiedByUserId = isVerified ? actorUserId : null;
        if (verificationNote is not null)
            center.VerificationNote = trimmedNote;

        await repository.UpdateAsync(center, ct);

        var action = isVerified ? "study_center.verify" : "study_center.unverify";
        var detailJson = JsonSerializer.Serialize(new
        {
            centerId = center.Id,
            centerName = center.Name,
            centerCode = center.Code,
            previousStatus,
            newStatus,
            verifiedAt = center.VerifiedAt,
            verificationNote = center.VerificationNote,
            actorUserId
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = "study_center",
            EntityId = center.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        return VerifyStudyCenterResult.Ok(ToDto(center));
    }

    public async Task<UpdateStudyCenterResult> UpdateCenterAsync(Guid id, UpdateStudyCenterRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return UpdateStudyCenterResult.Fail("Tên trung tâm không được để trống.");

        if (name.Length > MaxNameLength)
            return UpdateStudyCenterResult.Fail($"Tên trung tâm không được vượt quá {MaxNameLength} ký tự.");

        var center = await repository.GetByIdForUpdateAsync(id, ct);
        if (center is null)
            return UpdateStudyCenterResult.Fail("Không tìm thấy trung tâm tư vấn trong hệ thống.");

        var (legal, legalError) = ValidateLegalFields(
            staffCount: request.StaffCount,
            licenseNumber: request.LicenseNumber,
            taxCode: request.TaxCode,
            legalRepresentative: request.LegalRepresentative,
            licenseIssuedBy: request.LicenseIssuedBy,
            licenseIssuedDate: request.LicenseIssuedDate,
            legalComplianceMode: request.LegalComplianceMode,
            fallbackComplianceMode: string.IsNullOrWhiteSpace(center.LegalComplianceMode) ? ComplianceModeLicensed : center.LegalComplianceMode,
            trainingLicenseNumber: request.TrainingLicenseNumber,
            certifiedCounselorCount: request.CertifiedCounselorCount,
            legalDocumentUrls: request.LegalDocumentUrls,
            verificationNote: request.VerificationNote,
            defaultEmptyDocs: false);

        if (legalError is not null || legal is null)
            return UpdateStudyCenterResult.Fail(legalError ?? "Thông tin pháp lý không hợp lệ.");

        var oldSnapshot = BuildAuditSnapshot(center);

        center.Name = name;
        center.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        center.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        center.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        center.District = string.IsNullOrWhiteSpace(request.District) ? null : request.District.Trim();
        center.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        center.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        center.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        center.CenterType = string.IsNullOrWhiteSpace(request.CenterType) ? center.CenterType : request.CenterType.Trim();
        center.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        center.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        center.StaffCount = request.StaffCount;
        center.EstablishedYear = request.EstablishedYear;
        center.VisaSuccessRate = request.VisaSuccessRate;
        center.Latitude = request.Latitude;
        center.Longitude = request.Longitude;

        if (request.StudyLevels is not null)
            center.StudyLevels = request.StudyLevels;

        if (request.Services is not null)
            center.Services = request.Services;

        if (request.GalleryImages is not null)
            center.GalleryImages = request.GalleryImages;

        if (request.Accreditations is not null)
            center.Accreditations = request.Accreditations;

        ApplyLegalFields(center, legal);

        await repository.UpdateAsync(center, ct);

        var detailJson = JsonSerializer.Serialize(new
        {
            centerId = center.Id,
            centerCode = center.Code,
            actorUserId,
            old = oldSnapshot,
            updated = BuildAuditSnapshot(center)
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "study_center.update",
            EntityType = "study_center",
            EntityId = center.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        return UpdateStudyCenterResult.Ok(ToDto(center));
    }

    public async Task<SetStudyCenterActiveResult> SetCenterActiveAsync(Guid id, bool isActive, Guid actorUserId, CancellationToken ct = default)
    {
        var center = await repository.GetByIdForUpdateAsync(id, ct);
        if (center is null)
            return SetStudyCenterActiveResult.Fail("Không tìm thấy trung tâm tư vấn trong hệ thống.");

        var previousIsActive = center.IsActive;
        center.IsActive = isActive;

        await repository.UpdateAsync(center, ct);

        var action = isActive ? "study_center.show" : "study_center.hide";
        var detailJson = JsonSerializer.Serialize(new
        {
            centerId = center.Id,
            centerCode = center.Code,
            centerName = center.Name,
            previousIsActive,
            newIsActive = isActive,
            actorUserId
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = "study_center",
            EntityId = center.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        return SetStudyCenterActiveResult.Ok(ToDto(center));
    }

    public async Task<IReadOnlyList<string>> GetReferralCitiesAsync(CancellationToken ct = default) =>
        CenterReferral.Cities(await repository.ListReferableAsync(ct));

    internal static StudyCenterDto ToDto(StudyCenter c) =>
        new(c.Id, c.Code, c.Name, c.Website, c.Address, c.City, c.StudyLevels, c.Services, c.SurveyedAt,
            c.Phone, c.Email, c.Description, c.VerificationStatus, c.IsActive,
            c.CenterType, c.LogoUrl, c.CoverImageUrl, c.GalleryImages, c.District,
            c.Latitude, c.Longitude, c.StaffCount, c.EstablishedYear, c.LicenseNumber,
            c.Accreditations, c.VisaSuccessRate, c.AverageRating, c.ReviewCount,
            c.VerifiedAt, c.VerifiedByUserId,
            c.TaxCode, c.LegalRepresentative, c.LicenseIssuedBy, c.LicenseIssuedDate,
            c.LegalComplianceMode, c.TrainingLicenseNumber, c.CertifiedCounselorCount,
            c.LegalDocumentUrls, c.VerificationNote,
            c.EmailVerifiedAt, c.PhoneVerifiedAt, c.FeeMin, c.FeeMax, c.FeeDescription,
            c.RegistrantName, c.RegistrantPosition);

    /// <summary>True nếu yêu cầu thay đổi một trường pháp lý so với giá trị hiện tại.</summary>
    private static bool TouchesLegalFields(StudyCenter c, UpdateCenterProfileRequest r)
    {
        static bool Differs(string? incoming, string? current) =>
            incoming is not null && !string.Equals(TrimOrNull(incoming), TrimOrNull(current), StringComparison.Ordinal);

        return Differs(r.Name, c.Name)
            || Differs(r.CenterType, c.CenterType)
            || Differs(r.TaxCode?.Replace(" ", ""), c.TaxCode)
            || Differs(r.LegalRepresentative, c.LegalRepresentative)
            || Differs(r.LicenseNumber, c.LicenseNumber)
            || Differs(r.LicenseIssuedBy, c.LicenseIssuedBy)
            || Differs(r.TrainingLicenseNumber, c.TrainingLicenseNumber)
            || Differs(r.LegalComplianceMode, c.LegalComplianceMode)
            || (r.LicenseIssuedDate.HasValue && r.LicenseIssuedDate != c.LicenseIssuedDate)
            || (r.CertifiedCounselorCount.HasValue && r.CertifiedCounselorCount != c.CertifiedCounselorCount);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static (NormalizedLegalFields? Result, string? Error) ValidateLegalFields(
        int? staffCount,
        string? licenseNumber,
        string? taxCode,
        string? legalRepresentative,
        string? licenseIssuedBy,
        DateOnly? licenseIssuedDate,
        string? legalComplianceMode,
        string fallbackComplianceMode,
        string? trainingLicenseNumber,
        int? certifiedCounselorCount,
        List<string>? legalDocumentUrls,
        string? verificationNote,
        bool defaultEmptyDocs)
    {
        var trimmedLicense = TrimOrNull(licenseNumber);
        if (trimmedLicense is { Length: > MaxLicenseNumberLength })
            return (null, $"Số giấy phép tư vấn du học tối đa {MaxLicenseNumberLength} ký tự.");

        var trimmedTaxCode = TrimOrNull(taxCode);
        if (trimmedTaxCode is { Length: > MaxTaxCodeLength })
            return (null, $"Mã số thuế tối đa {MaxTaxCodeLength} ký tự.");

        var trimmedRep = TrimOrNull(legalRepresentative);
        if (trimmedRep is { Length: > MaxLegalRepresentativeLength })
            return (null, $"Người đại diện pháp luật tối đa {MaxLegalRepresentativeLength} ký tự.");

        var trimmedIssuedBy = TrimOrNull(licenseIssuedBy);
        if (trimmedIssuedBy is { Length: > MaxLicenseIssuedByLength })
            return (null, $"Cơ quan cấp phép tối đa {MaxLicenseIssuedByLength} ký tự.");

        var trimmedTrainingLicense = TrimOrNull(trainingLicenseNumber);
        if (trimmedTrainingLicense is { Length: > MaxLicenseNumberLength })
            return (null, $"Số giấy phép đào tạo tối đa {MaxLicenseNumberLength} ký tự.");

        if (licenseIssuedDate.HasValue && licenseIssuedDate.Value > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            return (null, "Ngày cấp giấy phép không được lớn hơn ngày hiện tại.");

        var normalizedMode = string.IsNullOrWhiteSpace(legalComplianceMode)
            ? fallbackComplianceMode
            : legalComplianceMode.Trim().ToLowerInvariant();

        if (normalizedMode is not (ComplianceModeLicensed or ComplianceModeNotified))
            return (null, $"Hình thức tuân thủ pháp lý chỉ chấp nhận '{ComplianceModeLicensed}' hoặc '{ComplianceModeNotified}'.");

        if (certifiedCounselorCount is < 0)
            return (null, "Số tư vấn viên có chứng chỉ không được âm.");

        if (staffCount.HasValue && certifiedCounselorCount.HasValue && certifiedCounselorCount.Value > staffCount.Value)
            return (null, "Số tư vấn viên có chứng chỉ không được vượt quá tổng nhân sự.");

        List<string>? sanitizedDocs = defaultEmptyDocs ? [] : null;
        if (legalDocumentUrls is not null)
        {
            sanitizedDocs = [];
            foreach (var raw in legalDocumentUrls)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var url = raw.Trim();
                if (url.Length > MaxLegalDocumentUrlLength)
                    return (null, $"Đường dẫn tài liệu pháp lý tối đa {MaxLegalDocumentUrlLength} ký tự.");

                var isRelative = url.StartsWith('/');
                var isHttp = Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                    && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
                if (!isRelative && !isHttp)
                    return (null, $"Đường dẫn tài liệu pháp lý không hợp lệ: '{url}'.");

                if (!sanitizedDocs.Contains(url, StringComparer.Ordinal))
                    sanitizedDocs.Add(url);
            }
        }

        var trimmedNote = TrimOrNull(verificationNote);
        if (trimmedNote is { Length: > MaxVerificationNoteLength })
            return (null, $"Ghi chú kiểm duyệt tối đa {MaxVerificationNoteLength} ký tự.");

        return (new NormalizedLegalFields(
            trimmedLicense,
            trimmedTaxCode,
            trimmedRep,
            trimmedIssuedBy,
            licenseIssuedDate,
            normalizedMode,
            trimmedTrainingLicense,
            certifiedCounselorCount,
            sanitizedDocs,
            trimmedNote,
            verificationNote is not null), null);
    }

    private static void ApplyLegalFields(StudyCenter center, NormalizedLegalFields legal)
    {
        center.LicenseNumber = legal.LicenseNumber;
        center.TaxCode = legal.TaxCode;
        center.LegalRepresentative = legal.LegalRepresentative;
        center.LicenseIssuedBy = legal.LicenseIssuedBy;
        center.LicenseIssuedDate = legal.LicenseIssuedDate;
        center.LegalComplianceMode = legal.LegalComplianceMode;
        center.TrainingLicenseNumber = legal.TrainingLicenseNumber;
        center.CertifiedCounselorCount = legal.CertifiedCounselorCount;
        if (legal.LegalDocumentUrls is not null)
            center.LegalDocumentUrls = legal.LegalDocumentUrls;
        if (legal.HasVerificationNote)
            center.VerificationNote = legal.VerificationNote;
    }

    private static object BuildAuditSnapshot(StudyCenter center) => new
    {
        center.Name,
        center.City,
        center.District,
        center.Address,
        center.Phone,
        center.Email,
        center.Website,
        center.CenterType,
        center.LicenseNumber,
        center.TaxCode,
        center.LegalRepresentative,
        center.LicenseIssuedBy,
        center.LicenseIssuedDate,
        center.LegalComplianceMode,
        center.TrainingLicenseNumber,
        center.CertifiedCounselorCount,
        center.LegalDocumentUrls,
        center.VerificationNote,
        center.StaffCount,
        center.EstablishedYear,
        center.VisaSuccessRate
    };

    public async Task<StudyCenterDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        return center is null ? null : ToDto(center);
    }

    public async Task<CenterVerificationStatusDto> GetVerificationStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
        {
            return new CenterVerificationStatusDto(
                CenterVerificationStatuses.Unverified,
                false, null, false, null, null, null);
        }

        CenterVerificationDto? currentReqDto = null;
        // Phản hồi của moderator (lý do từ chối / yêu cầu bổ sung / thu hồi) lấy từ dữ liệu thật, không tự đặt sẵn
        string? rejectionReason = center.VerificationStatus is CenterVerificationStatuses.Rejected or CenterVerificationStatuses.Suspended
            ? center.VerificationNote
            : null;

        if (verificationRepository is not null)
        {
            var req = await verificationRepository.GetCurrentByCenterIdAsync(center.Id, ct);
            if (req is not null)
            {
                currentReqDto = ToVerificationDto(req, center);
                if (!string.IsNullOrWhiteSpace(req.RejectionReason))
                    rejectionReason = req.RejectionReason;
            }
        }

        return new CenterVerificationStatusDto(
            center.VerificationStatus,
            center.EmailVerifiedAt.HasValue,
            center.EmailVerifiedAt,
            center.PhoneVerifiedAt.HasValue,
            center.PhoneVerifiedAt,
            currentReqDto,
            rejectionReason);
    }

    public async Task<CreateStudyCenterResult> SubmitVerificationAsync(Guid userId, SubmitVerificationDto request, CancellationToken ct = default)
    {
        if (verificationRepository is null)
            return CreateStudyCenterResult.Fail("Dịch vụ xác minh tạm thời chưa sẵn sàng.");

        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
            return CreateStudyCenterResult.Fail("Không tìm thấy thông tin trung tâm cho tài khoản này.");

        // Điều kiện 1 (email đã xác thực bằng OTP) là bắt buộc trước khi nộp hồ sơ; không còn xác thực SMS.
        if (!center.EmailVerifiedAt.HasValue)
            return CreateStudyCenterResult.Fail("Bạn cần xác thực email bằng mã OTP trước khi nộp hồ sơ xác minh.");

        var existingPending = await verificationRepository.GetCurrentByCenterIdAsync(center.Id, ct);
        if (existingPending is not null && existingPending.Status == CenterVerificationStatuses.PendingReview)
            return CreateStudyCenterResult.Fail("Hồ sơ xác minh của bạn đang trong trạng thái chờ duyệt. Vui lòng chờ kết quả từ Moderator.");

        var legalName = request.LegalName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(legalName) || legalName.Length > 256)
            return CreateStudyCenterResult.Fail("Tên pháp lý theo ĐKKD không được để trống và tối đa 256 ký tự.");

        var taxCode = request.TaxCode?.Trim().Replace(" ", "").ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(taxCode) || !Regex.IsMatch(taxCode, @"^\d{10}(-\d{3})?$"))
            return CreateStudyCenterResult.Fail("Mã số doanh nghiệp / Mã số thuế không đúng định dạng (10 hoặc 13 chữ số).");

        var address = request.Address?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(address) || address.Length > 512)
            return CreateStudyCenterResult.Fail("Địa chỉ trụ sở không được để trống và tối đa 512 ký tự.");

        var legalRep = request.LegalRepresentative?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(legalRep) || legalRep.Length > 150)
            return CreateStudyCenterResult.Fail("Họ tên người đại diện theo pháp luật không được để trống và tối đa 150 ký tự.");

        if (await verificationRepository.ExistsVerifiedTaxCodeAsync(taxCode, center.Id, ct))
            return CreateStudyCenterResult.Fail("Mã số doanh nghiệp này đã thuộc về một trung tâm đã được xác minh. Nếu bạn cho rằng có sự nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.");

        var docs = request.Documents ?? [];
        var hasBizReg = docs.Any(d => d.DocumentType == CenterDocumentTypes.BusinessRegistration && !string.IsNullOrWhiteSpace(d.FileUrl));
        if (!hasBizReg)
            return CreateStudyCenterResult.Fail("Bản scan hoặc ảnh Giấy chứng nhận đăng ký doanh nghiệp là tài liệu bắt buộc.");

        var verificationReq = new CenterVerificationRequest
        {
            CenterId = center.Id,
            RequestedByUserId = userId,
            LegalName = legalName,
            TaxCode = taxCode,
            Address = address,
            LegalRepresentative = legalRep,
            Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim(),
            Status = CenterVerificationStatuses.PendingReview,
            SubmittedAt = DateTime.UtcNow,
            Documents = docs.Select(d => new CenterVerificationDocument
            {
                CenterId = center.Id,
                DocumentType = d.DocumentType,
                FileUrl = d.FileUrl,
                FileName = d.FileName,
                FileSize = d.FileSize,
                ContentType = d.ContentType,
                UploadedAt = DateTime.UtcNow
            }).ToList()
        };

        await verificationRepository.AddRequestAsync(verificationReq, ct);

        center.VerificationStatus = CenterVerificationStatuses.PendingReview;
        center.TaxCode = taxCode;
        center.LegalRepresentative = legalRep;
        center.Address = address;
        if (!string.IsNullOrWhiteSpace(verificationReq.Website) && string.IsNullOrWhiteSpace(center.Website))
            center.Website = verificationReq.Website;

        await repository.UpdateAsync(center, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center.verification_submitted",
            EntityType = "CenterVerificationRequest",
            EntityId = verificationReq.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { centerId = center.Id, taxCode, legalName, documentCount = docs.Count })
        }, ct);

        return CreateStudyCenterResult.Ok(ToDto(center));
    }

    public async Task<PagedResult<CenterVerificationDto>> GetVerificationRequestsAsync(CenterVerificationQuery query, CancellationToken ct = default)
    {
        if (verificationRepository is null)
            return new PagedResult<CenterVerificationDto>([], query.Page, query.PageSize, 0);

        var paged = await verificationRepository.SearchAsync(query, ct);
        var dtos = new List<CenterVerificationDto>(paged.Items.Count);

        foreach (var req in paged.Items)
        {
            var center = await repository.GetByIdAsync(req.CenterId, ct);
            dtos.Add(ToVerificationDto(req, center));
        }

        return new PagedResult<CenterVerificationDto>(dtos, paged.Page, paged.PageSize, paged.TotalCount);
    }

    public async Task<CenterVerificationDto?> GetVerificationRequestByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        if (verificationRepository is null) return null;
        var req = await verificationRepository.GetByIdWithDocumentsAsync(requestId, ct);
        if (req is null) return null;
        var center = await repository.GetByIdAsync(req.CenterId, ct);
        return ToVerificationDto(req, center);
    }

    public async Task<CenterVerificationDocument?> GetVerificationDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        if (verificationRepository is null) return null;
        return await verificationRepository.GetDocumentByIdAsync(documentId, ct);
    }

    public async Task<VerifyStudyCenterResult> ReviewVerificationAsync(Guid requestId, ReviewCenterVerificationRequest request, Guid reviewerUserId, CancellationToken ct = default)
    {
        if (verificationRepository is null)
            return VerifyStudyCenterResult.Fail("Dịch vụ xác minh tạm thời chưa sẵn sàng.");

        var req = await verificationRepository.GetByIdWithDocumentsAsync(requestId, ct);
        if (req is null)
            return VerifyStudyCenterResult.Fail("Không tìm thấy yêu cầu xác minh.");

        var center = await repository.GetByIdForUpdateAsync(req.CenterId, ct);
        if (center is null)
            return VerifyStudyCenterResult.Fail("Không tìm thấy thông tin trung tâm tư vấn.");

        var action = request.Action?.Trim().ToLowerInvariant() ?? string.Empty;

        if (action == "approve")
        {
            if (!center.EmailVerifiedAt.HasValue)
                return VerifyStudyCenterResult.Fail("Điều kiện (1) chưa thỏa: Email trung tâm chưa được xác thực.");

            if (!request.IsNationalRegistryMatched)
                return VerifyStudyCenterResult.Fail("Điều kiện (2) chưa thỏa: Moderator chưa xác nhận đối chiếu khớp tên pháp lý và MST trên Cổng ĐKDN.");

            if (!request.IsBusinessOperating)
                return VerifyStudyCenterResult.Fail("Điều kiện (3) chưa thỏa: Moderator chưa xác nhận doanh nghiệp đang hoạt động bình thường.");

            if (!request.IsRepresentativeValid)
                return VerifyStudyCenterResult.Fail("Điều kiện (4) chưa thỏa: Moderator chưa xác nhận tính hợp lệ của người đại diện hoặc giấy ủy quyền.");

            req.Status = CenterVerificationStatuses.Verified;
            req.ReviewedByUserId = reviewerUserId;
            req.ReviewedAt = DateTime.UtcNow;
            req.IsNationalRegistryMatched = true;
            req.IsBusinessOperating = true;
            req.IsRepresentativeValid = true;
            req.ReviewNotes = request.ReviewNotes?.Trim();
            req.RejectionReason = null;

            center.VerificationStatus = CenterVerificationStatuses.Verified;
            center.VerifiedAt = DateTime.UtcNow;
            center.VerifiedByUserId = reviewerUserId;
            center.VerificationNote = request.ReviewNotes?.Trim();

            await verificationRepository.UpdateRequestAsync(req, ct);
            await repository.UpdateAsync(center, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = reviewerUserId,
                Action = "center.verified",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { requestId, notes = request.ReviewNotes })
            }, ct);

            return VerifyStudyCenterResult.Ok(ToDto(center));
        }
        else if (action == "reject" || action == "changes_requested")
        {
            var reason = request.RejectionReason?.Trim();
            if (string.IsNullOrEmpty(reason))
                return VerifyStudyCenterResult.Fail("Lý do từ chối / yêu cầu bổ sung không được để trống.");

            var newReqStatus = action == "reject" ? CenterVerificationStatuses.Rejected : "changes_requested";
            req.Status = newReqStatus;
            req.ReviewedByUserId = reviewerUserId;
            req.ReviewedAt = DateTime.UtcNow;
            req.RejectionReason = reason;
            req.ReviewNotes = request.ReviewNotes?.Trim();
            req.IsNationalRegistryMatched = request.IsNationalRegistryMatched;
            req.IsBusinessOperating = request.IsBusinessOperating;
            req.IsRepresentativeValid = request.IsRepresentativeValid;

            center.VerificationStatus = CenterVerificationStatuses.Rejected;
            center.VerificationNote = reason;

            await verificationRepository.UpdateRequestAsync(req, ct);
            await repository.UpdateAsync(center, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = reviewerUserId,
                Action = $"center.{action}",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { requestId, reason, notes = request.ReviewNotes })
            }, ct);

            return VerifyStudyCenterResult.Ok(ToDto(center));
        }
        else if (action == "revoke")
        {
            var reason = request.RejectionReason?.Trim();
            if (string.IsNullOrEmpty(reason))
                return VerifyStudyCenterResult.Fail("Lý do thu hồi dấu xác minh không được để trống.");

            req.Status = CenterVerificationStatuses.Suspended;
            req.ReviewedByUserId = reviewerUserId;
            req.ReviewedAt = DateTime.UtcNow;
            req.RejectionReason = reason;

            center.VerificationStatus = CenterVerificationStatuses.Suspended;
            center.VerificationNote = reason;

            await verificationRepository.UpdateRequestAsync(req, ct);
            await repository.UpdateAsync(center, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = reviewerUserId,
                Action = "center.revoked",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { requestId, reason })
            }, ct);

            return VerifyStudyCenterResult.Ok(ToDto(center));
        }

        return VerifyStudyCenterResult.Fail($"Hành động kiểm duyệt '{action}' không hợp lệ.");
    }

    public async Task<UpdateStudyCenterResult> UpdateCenterProfileAsync(Guid userId, UpdateCenterProfileRequest request, CancellationToken ct = default)
    {
        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
            return UpdateStudyCenterResult.Fail("Không tìm thấy thông tin trung tâm cho tài khoản này.");

        // Đã xác minh: khóa thông tin pháp lý (tên, loại hình, MST, đại diện, giấy phép...).
        // Muốn đổi phải nộp lại hồ sơ xác minh.
        var legalLocked = center.VerificationStatus == CenterVerificationStatuses.Verified;
        if (legalLocked && TouchesLegalFields(center, request))
            return UpdateStudyCenterResult.Fail(
                "Trung tâm đã được xác minh nên không thể sửa thông tin pháp lý. Vui lòng nộp lại hồ sơ xác minh nếu có thay đổi đăng ký kinh doanh.");

        if (!legalLocked)
        {
            var taxCodeInput = request.TaxCode is null ? null : request.TaxCode.Trim().Replace(" ", "").ToUpperInvariant();
            if (!string.IsNullOrEmpty(taxCodeInput) && !Regex.IsMatch(taxCodeInput, @"^\d{10}(-\d{3})?$"))
                return UpdateStudyCenterResult.Fail("Mã số doanh nghiệp / Mã số thuế không đúng định dạng (10 hoặc 13 chữ số).");

            var (legal, legalError) = ValidateLegalFields(
                request.StaffCount ?? center.StaffCount,
                request.LicenseNumber ?? center.LicenseNumber,
                taxCodeInput ?? center.TaxCode,
                request.LegalRepresentative ?? center.LegalRepresentative,
                request.LicenseIssuedBy ?? center.LicenseIssuedBy,
                request.LicenseIssuedDate ?? center.LicenseIssuedDate,
                request.LegalComplianceMode,
                center.LegalComplianceMode,
                request.TrainingLicenseNumber ?? center.TrainingLicenseNumber,
                request.CertifiedCounselorCount ?? center.CertifiedCounselorCount,
                legalDocumentUrls: null,
                verificationNote: null,
                defaultEmptyDocs: false);
            if (legalError is not null || legal is null)
                return UpdateStudyCenterResult.Fail(legalError ?? "Thông tin pháp lý không hợp lệ.");

            ApplyLegalFields(center, legal);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            if (request.Name.Trim().Length > MaxNameLength)
                return UpdateStudyCenterResult.Fail($"Tên trung tâm tối đa {MaxNameLength} ký tự.");
            center.Name = request.Name.Trim();
        }

        if (request.Description != null)
            center.Description = TrimOrNull(request.Description);

        if (request.Website != null)
            center.Website = TrimOrNull(request.Website);

        if (request.Address != null)
            center.Address = TrimOrNull(request.Address);

        if (request.City != null)
            center.City = TrimOrNull(request.City);

        if (request.District != null)
            center.District = TrimOrNull(request.District);

        if (request.CenterType != null)
            center.CenterType = request.CenterType.Trim();

        if (request.EstablishedYear.HasValue)
            center.EstablishedYear = request.EstablishedYear.Value;

        if (request.StaffCount.HasValue)
            center.StaffCount = request.StaffCount.Value;

        if (request.Services != null)
            center.Services = request.Services.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        if (request.StudyLevels != null)
            center.StudyLevels = request.StudyLevels.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        if (request.FeeMin.HasValue && request.FeeMax.HasValue && request.FeeMin > request.FeeMax)
            return UpdateStudyCenterResult.Fail("Mức phí tối thiểu không được lớn hơn mức phí tối đa.");

        if (request.FeeMin.HasValue) center.FeeMin = request.FeeMin.Value;
        if (request.FeeMax.HasValue) center.FeeMax = request.FeeMax.Value;
        if (request.FeeDescription != null) center.FeeDescription = TrimOrNull(request.FeeDescription);

        if (request.LogoUrl != null) center.LogoUrl = TrimOrNull(request.LogoUrl);
        if (request.CoverImageUrl != null) center.CoverImageUrl = TrimOrNull(request.CoverImageUrl);

        await repository.UpdateAsync(center, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center.profile_updated",
            EntityType = "StudyCenter",
            EntityId = center.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { centerId = center.Id, name = center.Name })
        }, ct);

        return UpdateStudyCenterResult.Ok(ToDto(center));
    }

    private static CenterVerificationDto ToVerificationDto(CenterVerificationRequest req, StudyCenter? center) =>
        new(
            req.Id,
            req.CenterId,
            req.RequestedByUserId,
            req.LegalName,
            req.TaxCode,
            req.Address,
            req.LegalRepresentative,
            req.Website,
            req.Status,
            req.SubmittedAt,
            req.ReviewedByUserId,
            req.ReviewedAt,
            req.RejectionReason,
            req.ReviewNotes,
            req.IsNationalRegistryMatched,
            req.IsBusinessOperating,
            req.IsRepresentativeValid,
            req.Documents.Select(d => new CenterVerificationDocumentDto(
                d.Id, d.RequestId, d.DocumentType, d.FileUrl, d.FileName, d.FileSize, d.ContentType, d.UploadedAt
            )).ToList(),
            center?.Name,
            center?.Code,
            center?.Email,
            center?.Phone,
            center?.EmailVerifiedAt,
            center?.PhoneVerifiedAt);
}
