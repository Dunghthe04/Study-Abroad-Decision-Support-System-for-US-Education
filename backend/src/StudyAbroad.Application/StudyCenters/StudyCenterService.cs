using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
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
    /// <summary>Thành phố có trung tâm gợi ý được cho một dịch vụ (và bậc học), kèm số trung tâm: chat cho học sinh chọn nơi đang có.</summary>
    Task<IReadOnlyList<CityCount>> GetReferralCityCountsAsync(CenterReferralQuery query, CancellationToken ct = default);
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
    /// <summary>[UC-92] Yêu cầu xóa tài khoản mới nhất của trung tâm.</summary>
    Task<CenterDeletionRequestDto?> GetDeletionRequestAsync(Guid userId, CancellationToken ct = default);
    /// <summary>[UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu (UC-93 xử lý).</summary>
    Task<CenterDeletionRequestResult> RequestDeletionAsync(Guid userId, string? reason, CancellationToken ct = default);
}

public class StudyCenterService(
    IStudyCenterRepository repository,
    IAuditLogRepository auditLogs,
    ICenterVerificationRepository? verificationRepository = null,
    IEmailSender? emailSender = null,
    StudyAbroad.Application.Notifications.INotificationService? notifications = null,
    ICenterProfileChangeRepository? profileChanges = null,
    ICenterDeletionRequestRepository? deletionRequests = null) : IStudyCenterService
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
    /// <summary>[UC-71] Đặc tả 6.1: tối đa 5 tệp cho một lần nộp hồ sơ xác minh.</summary>
    public const int MaxVerificationDocuments = 5;
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

    // Các trung tâm cho chat gợi ý (B6), tối đa 20; luật chọn nằm ở CenterReferral
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

    public async Task<IReadOnlyList<CityCount>> GetReferralCityCountsAsync(CenterReferralQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            throw new ArgumentException("Service is required.", nameof(query));
        return CenterReferral.CityCounts(await repository.ListReferableAsync(ct), query.Service, query.StudyLevel);
    }

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
            c.RegistrantName, c.RegistrantPosition, c.Fanpage, c.ServiceAreas, c.Branches);

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

    /// <summary>
    /// [UC-71] Cần giấy ủy quyền khi họ tên người đăng ký khác người đại diện theo pháp luật (không phân biệt hoa thường, dấu, khoảng trắng).
    /// Chưa có tên người đăng ký (tài khoản cũ) thì không chặn.
    /// </summary>
    public static bool RegistrantNeedsPowerOfAttorney(string? registrantName, string? legalRepresentative)
    {
        if (string.IsNullOrWhiteSpace(registrantName)) return false;
        return NormalizePersonName(registrantName) != NormalizePersonName(legalRepresentative);
    }

    private static string NormalizePersonName(string? name)
    {
        var d = (name ?? string.Empty).Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString(), @"\s+", " ");
    }

    public async Task<CreateStudyCenterResult> SubmitVerificationAsync(Guid userId, SubmitVerificationDto request, CancellationToken ct = default)
    {
        if (verificationRepository is null)
            return CreateStudyCenterResult.Fail("Dịch vụ xác minh tạm thời chưa sẵn sàng.");

        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
            return CreateStudyCenterResult.Fail("Không tìm thấy thông tin trung tâm cho tài khoản này.");

        // [UC-91] Phải xác thực cả email (OTP email) và số điện thoại (OTP SMS) trước khi nộp hồ sơ xác minh.
        if (!center.EmailVerifiedAt.HasValue)
            return CreateStudyCenterResult.Fail("Bạn cần xác thực email bằng mã OTP trước khi nộp hồ sơ xác minh.");
        if (!center.PhoneVerifiedAt.HasValue)
            return CreateStudyCenterResult.Fail("Bạn cần xác thực số điện thoại bằng mã OTP qua SMS trước khi nộp hồ sơ xác minh.");

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
        if (docs.Count > MaxVerificationDocuments)
            return CreateStudyCenterResult.Fail($"Mỗi lần nộp hồ sơ tối đa {MaxVerificationDocuments} tệp. Vui lòng bỏ bớt tệp không cần thiết.");
        var hasBizReg = docs.Any(d => d.DocumentType == CenterDocumentTypes.BusinessRegistration && !string.IsNullOrWhiteSpace(d.FileUrl));
        if (!hasBizReg)
            return CreateStudyCenterResult.Fail("Bản scan hoặc ảnh Giấy chứng nhận đăng ký doanh nghiệp là tài liệu bắt buộc.");

        // [UC-71] Đặc tả 5.1: người đăng ký không phải người đại diện theo pháp luật thì phải nộp giấy ủy quyền
        if (RegistrantNeedsPowerOfAttorney(center.RegistrantName, legalRep)
            && !docs.Any(d => d.DocumentType == CenterDocumentTypes.PowerOfAttorney && !string.IsNullOrWhiteSpace(d.FileUrl)))
            return CreateStudyCenterResult.Fail("Người đăng ký không phải người đại diện theo pháp luật nên bắt buộc nộp Giấy ủy quyền người thực hiện thủ tục.");

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

        await NotifyCenterAsync(center, "submitted", null, null, ct);
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

    /// <summary>
    /// [UC-59] Báo kết quả xét duyệt cho trung tâm qua email (đặc tả 5.1: email lý do gửi cho trung tâm).
    /// Trung tâm không có email, hoặc gửi lỗi, thì bỏ qua: kết quả duyệt đã lưu, không được làm hỏng thao tác của Moderator.
    /// </summary>
    private async Task NotifyCenterAsync(StudyCenter center, string outcome, string? reason, IReadOnlyList<string>? unmet, CancellationToken ct)
    {
        // [UC-110 / UC-59] Thông báo trong hệ thống cho tài khoản sở hữu trung tâm (service tự nuốt lỗi)
        if (notifications is not null && center.OwnerUserId is { } ownerId)
        {
            var title = outcome switch
            {
                "verified" => "Hồ sơ xác minh đã được duyệt",
                "reject" => "Hồ sơ xác minh bị từ chối",
                "changes_requested" => "Hồ sơ xác minh cần bổ sung",
                "revoked" => "Dấu xác minh của trung tâm bị thu hồi",
                _ => "Cập nhật kết quả xác minh",
            };
            await notifications.NotifyAsync(ownerId, StudyAbroad.Application.Notifications.NotificationTypes.VerificationResult,
                title, string.IsNullOrWhiteSpace(reason) ? null : $"Lý do: {reason}", "/center/verification", ct);
        }

        if (emailSender is null || string.IsNullOrWhiteSpace(center.Email)) return;
        try
        {
            await emailSender.SendCenterVerificationOutcomeEmailAsync(center.Email, center.Name, outcome, reason, unmet, ct);
        }
        catch (Exception)
        {
            // đã lưu kết quả duyệt; email là thông báo phụ
        }
    }

    /// <summary>Những điều kiện xác minh chưa đạt (đặc tả 5.1: điều kiện 1 email do hệ thống kiểm, 3 đến 5 do Moderator kiểm).</summary>
    private static List<string> UnmetConditions(StudyCenter center, ReviewCenterVerificationRequest request)
    {
        var unmet = new List<string>();
        if (!center.EmailVerifiedAt.HasValue) unmet.Add("Email trung tâm chưa được xác thực");
        if (!request.IsNationalRegistryMatched) unmet.Add("Tên pháp lý và mã số thuế chưa khớp Cổng đăng ký doanh nghiệp quốc gia");
        if (!request.IsBusinessOperating) unmet.Add("Doanh nghiệp chưa được xác nhận là đang hoạt động");
        if (!request.IsRepresentativeValid) unmet.Add("Người đăng ký chưa được xác nhận là người đại diện theo pháp luật hoặc có giấy ủy quyền hợp lệ");
        return unmet;
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

            await NotifyCenterAsync(center, "verified", request.ReviewNotes?.Trim(), null, ct);
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

            await NotifyCenterAsync(center, action, reason, UnmetConditions(center, request), ct);
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

            await NotifyCenterAsync(center, "revoked", reason, null, ct);
            return VerifyStudyCenterResult.Ok(ToDto(center));
        }

        return VerifyStudyCenterResult.Fail($"Hành động kiểm duyệt '{action}' không hợp lệ.");
    }

    public const int MaxProfileListItems = 20;
    public const int MaxProfileListItemLength = 256;
    public const int MaxProfileUrlLength = 512;
    public const int MaxDeletionReasonLength = 1000;

    /// <summary>[UC-92] Trường pháp lý: đổi khi đã xác minh thì hồ sơ về Chờ duyệt (UC-59).</summary>
    private static readonly HashSet<string> LegalProfileFields = new(StringComparer.Ordinal)
    {
        "name", "centerType", "taxCode", "legalRepresentative", "licenseNumber", "licenseIssuedBy",
        "licenseIssuedDate", "legalComplianceMode", "trainingLicenseNumber", "certifiedCounselorCount",
    };

    /// <summary>[UC-92] Trường lưu ngay nhưng đánh dấu để Moderator kiểm tra (UC-114).</summary>
    private static readonly HashSet<string> ReviewProfileFields = new(StringComparer.Ordinal)
    {
        CenterProfileChangeFields.Website, CenterProfileChangeFields.Fanpage,
    };

    /// <summary>Ảnh chụp giá trị các trường trung tâm tự sửa được, để so trước / sau và phân loại.</summary>
    private static Dictionary<string, string?> SnapshotProfile(StudyCenter c)
    {
        static string? List(List<string> values) => values.Count == 0 ? null : string.Join("\n", values);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = c.Name,
            ["centerType"] = c.CenterType,
            ["taxCode"] = c.TaxCode,
            ["legalRepresentative"] = c.LegalRepresentative,
            ["licenseNumber"] = c.LicenseNumber,
            ["licenseIssuedBy"] = c.LicenseIssuedBy,
            ["licenseIssuedDate"] = c.LicenseIssuedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["legalComplianceMode"] = c.LegalComplianceMode,
            ["trainingLicenseNumber"] = c.TrainingLicenseNumber,
            ["certifiedCounselorCount"] = c.CertifiedCounselorCount?.ToString(CultureInfo.InvariantCulture),
            ["description"] = c.Description,
            [CenterProfileChangeFields.Website] = c.Website,
            [CenterProfileChangeFields.Fanpage] = c.Fanpage,
            ["address"] = c.Address,
            ["city"] = c.City,
            ["district"] = c.District,
            ["serviceAreas"] = List(c.ServiceAreas),
            ["branches"] = List(c.Branches),
            ["services"] = List(c.Services),
            ["studyLevels"] = List(c.StudyLevels),
            ["establishedYear"] = c.EstablishedYear?.ToString(CultureInfo.InvariantCulture),
            ["staffCount"] = c.StaffCount?.ToString(CultureInfo.InvariantCulture),
            ["feeMin"] = c.FeeMin?.ToString(CultureInfo.InvariantCulture),
            ["feeMax"] = c.FeeMax?.ToString(CultureInfo.InvariantCulture),
            ["feeDescription"] = c.FeeDescription,
            ["logoUrl"] = c.LogoUrl,
            ["coverImageUrl"] = c.CoverImageUrl,
        };
    }

    private static string? ValidateProfileUrl(string? value, string label)
    {
        var trimmed = TrimOrNull(value);
        if (trimmed is null) return null;
        if (trimmed.Length > MaxProfileUrlLength)
            return $"{label} tối đa {MaxProfileUrlLength} ký tự.";
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return $"{label} phải là đường dẫn bắt đầu bằng http:// hoặc https://.";
        return null;
    }

    private static (List<string>? Values, string? Error) NormalizeProfileList(List<string>? values, string label)
    {
        if (values is null) return (null, null);
        var cleaned = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
        if (cleaned.Count > MaxProfileListItems)
            return (null, $"{label} tối đa {MaxProfileListItems} mục.");
        if (cleaned.Any(v => v.Length > MaxProfileListItemLength))
            return (null, $"Mỗi mục {label.ToLowerInvariant()} tối đa {MaxProfileListItemLength} ký tự.");
        return (cleaned, null);
    }

    /// <summary>
    /// [UC-92] Trung tâm tự sửa hồ sơ. Phân loại theo mức ảnh hưởng:
    /// mô tả, dịch vụ, logo, địa chỉ, chi nhánh, mức phí... lưu ngay; website và fanpage lưu ngay nhưng đánh dấu cho Moderator
    /// kiểm tra (UC-114); thông tin pháp lý của trung tâm đã xác minh đưa hồ sơ về Chờ duyệt và tạm ẩn dấu xác minh (UC-59).
    /// Email và số điện thoại không đổi ở đây mà qua UC-91.
    /// </summary>
    public async Task<UpdateStudyCenterResult> UpdateCenterProfileAsync(Guid userId, UpdateCenterProfileRequest request, CancellationToken ct = default)
    {
        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
            return UpdateStudyCenterResult.Fail("Không tìm thấy thông tin trung tâm cho tài khoản này.");

        var pendingReview = center.VerificationStatus == CenterVerificationStatuses.PendingReview;
        var verified = center.VerificationStatus == CenterVerificationStatuses.Verified;
        var touchesLegal = TouchesLegalFields(center, request);

        // Thông tin pháp lý chỉ sửa được khi chưa xác minh hoặc bị Moderator từ chối (UC-59).
        // Đã xác minh: khóa. Đang chờ duyệt: khóa tới khi có kết quả.
        if (verified && touchesLegal)
            return UpdateStudyCenterResult.Fail(
                "Trung tâm đã được xác minh nên không thể sửa thông tin pháp lý (tên, mã số thuế, giấy phép...). Vui lòng nộp lại hồ sơ xác minh nếu cần thay đổi.");
        if (pendingReview && touchesLegal)
            return UpdateStudyCenterResult.Fail(
                "Hồ sơ xác minh đang chờ duyệt nên chưa thể sửa thông tin pháp lý (tên, mã số thuế, giấy phép...). Vui lòng chờ kết quả xét duyệt.");

        var websiteError = ValidateProfileUrl(request.Website, "Website") ?? ValidateProfileUrl(request.Fanpage, "Fanpage");
        if (websiteError is not null)
            return UpdateStudyCenterResult.Fail(websiteError);

        var (serviceAreas, areasError) = NormalizeProfileList(request.ServiceAreas, "Khu vực phục vụ");
        if (areasError is not null) return UpdateStudyCenterResult.Fail(areasError);
        var (branches, branchesError) = NormalizeProfileList(request.Branches, "Chi nhánh");
        if (branchesError is not null) return UpdateStudyCenterResult.Fail(branchesError);

        if (request.FeeMin.HasValue && request.FeeMax.HasValue && request.FeeMin > request.FeeMax)
            return UpdateStudyCenterResult.Fail("Mức phí tối thiểu không được lớn hơn mức phí tối đa.");

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length > MaxNameLength)
            return UpdateStudyCenterResult.Fail($"Tên trung tâm tối đa {MaxNameLength} ký tự.");

        var before = SnapshotProfile(center);

        // Thông tin pháp lý: chỉ trung tâm chưa xác minh / bị từ chối tới được đây (đã xác minh, chờ duyệt bị chặn ở trên).
        if (!verified && !pendingReview)
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

            if (!string.IsNullOrWhiteSpace(request.Name))
                center.Name = request.Name.Trim();
            if (request.CenterType != null)
                center.CenterType = request.CenterType.Trim();
        }

        if (request.Description != null) center.Description = TrimOrNull(request.Description);
        if (request.Website != null) center.Website = TrimOrNull(request.Website);
        if (request.Fanpage != null) center.Fanpage = TrimOrNull(request.Fanpage);
        if (request.Address != null) center.Address = TrimOrNull(request.Address);
        if (request.City != null) center.City = TrimOrNull(request.City);
        if (request.District != null) center.District = TrimOrNull(request.District);
        if (serviceAreas != null) center.ServiceAreas = serviceAreas;
        if (branches != null) center.Branches = branches;
        if (request.EstablishedYear.HasValue) center.EstablishedYear = request.EstablishedYear.Value;
        if (request.StaffCount.HasValue) center.StaffCount = request.StaffCount.Value;
        if (request.Services != null)
            center.Services = request.Services.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (request.StudyLevels != null)
            center.StudyLevels = request.StudyLevels.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (request.FeeMin.HasValue) center.FeeMin = request.FeeMin.Value;
        if (request.FeeMax.HasValue) center.FeeMax = request.FeeMax.Value;
        if (request.FeeDescription != null) center.FeeDescription = TrimOrNull(request.FeeDescription);
        if (request.LogoUrl != null) center.LogoUrl = TrimOrNull(request.LogoUrl);
        if (request.CoverImageUrl != null) center.CoverImageUrl = TrimOrNull(request.CoverImageUrl);

        if (center.FeeMin.HasValue && center.FeeMax.HasValue && center.FeeMin > center.FeeMax)
            return UpdateStudyCenterResult.Fail("Mức phí tối thiểu không được lớn hơn mức phí tối đa.");

        var after = SnapshotProfile(center);
        var changed = after.Keys.Where(k => !string.Equals(before[k], after[k], StringComparison.Ordinal)).ToList();
        var legalChanged = changed.Where(LegalProfileFields.Contains).ToList();
        var reviewChanged = changed.Where(ReviewProfileFields.Contains).ToList();
        var savedChanged = changed.Where(k => !LegalProfileFields.Contains(k) && !ReviewProfileFields.Contains(k)).ToList();

        // Đã xác minh mà đổi thông tin pháp lý: về Chờ duyệt, tạm ẩn dấu xác minh tới khi Moderator duyệt lại (UC-59).
        var verificationReset = verified && legalChanged.Count > 0;
        if (verificationReset)
            center.VerificationStatus = CenterVerificationStatuses.PendingReview;

        await repository.UpdateAsync(center, ct);

        if (verificationReset)
            await CreateReverificationRequestAsync(center, userId, legalChanged, ct);

        foreach (var field in reviewChanged)
            await FlagProfileChangeAsync(center.Id, field, before[field], after[field], ct);

        if (changed.Count > 0)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = "center.profile_updated",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    centerId = center.Id,
                    savedFields = savedChanged,
                    reviewFields = reviewChanged,
                    legalFields = legalChanged,
                    verificationReset,
                })
            }, ct);
        }

        var message = verificationReset
            ? "Đã lưu. Thông tin pháp lý thay đổi nên hồ sơ chuyển về Chờ duyệt và tạm ẩn dấu xác minh tới khi Moderator duyệt lại."
            : reviewChanged.Count > 0
                ? "Đã lưu. Website / fanpage mới hiển thị ngay và sẽ được Moderator kiểm tra."
                : changed.Count > 0 ? "Đã lưu thay đổi hồ sơ." : "Không có thay đổi nào.";

        return UpdateStudyCenterResult.Ok(ToDto(center)) with
        {
            Outcome = new CenterProfileUpdateOutcome(savedChanged, reviewChanged, legalChanged, verificationReset, message),
        };
    }

    /// <summary>[UC-92 / UC-114] Ghi một thay đổi cần kiểm tra; trường đã có bản chờ thì cập nhật giá trị mới, giữ giá trị cũ ban đầu.</summary>
    private async Task FlagProfileChangeAsync(Guid centerId, string field, string? oldValue, string? newValue, CancellationToken ct)
    {
        if (profileChanges is null) return;

        var pending = await profileChanges.GetPendingAsync(centerId, field, ct);
        if (pending is not null)
        {
            if (string.Equals(pending.OldValue, newValue, StringComparison.Ordinal))
            {
                // Sửa lại đúng giá trị đã được chấp nhận trước đó: không còn gì để kiểm tra.
                pending.Status = CenterProfileChangeStatuses.Passed;
                pending.ReviewReason = "Trung tâm đã đổi lại giá trị cũ.";
                pending.ReviewedAt = DateTime.UtcNow;
            }
            else
            {
                pending.NewValue = newValue;
            }
            await profileChanges.UpdateAsync(pending, ct);
            return;
        }

        await profileChanges.AddAsync(new CenterProfileChange
        {
            StudyCenterId = centerId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Status = CenterProfileChangeStatuses.Pending,
        }, ct);
    }

    /// <summary>
    /// [UC-92 / UC-59] Trung tâm đã xác minh đổi thông tin pháp lý: tạo hồ sơ xác minh mới ở Chờ duyệt từ thông tin hiện tại,
    /// mang theo tài liệu của lần nộp gần nhất để Moderator đối chiếu. Thay tài liệu thì trung tâm nộp lại ở UC-71.
    /// </summary>
    private async Task CreateReverificationRequestAsync(StudyCenter center, Guid userId, IReadOnlyList<string> legalFields, CancellationToken ct)
    {
        if (verificationRepository is not null)
        {
            var previous = await verificationRepository.GetCurrentByCenterIdAsync(center.Id, ct);
            var request = new CenterVerificationRequest
            {
                CenterId = center.Id,
                RequestedByUserId = userId,
                LegalName = center.Name,
                TaxCode = center.TaxCode ?? previous?.TaxCode ?? string.Empty,
                Address = center.Address ?? previous?.Address ?? string.Empty,
                LegalRepresentative = center.LegalRepresentative ?? previous?.LegalRepresentative ?? string.Empty,
                Website = center.Website,
                Status = CenterVerificationStatuses.PendingReview,
                SubmittedAt = DateTime.UtcNow,
                ReviewNotes = "Tự tạo khi trung tâm đã xác minh sửa thông tin pháp lý (UC-92): " + string.Join(", ", legalFields),
            };
            request.Documents = (previous?.Documents ?? []).Select(d => new CenterVerificationDocument
            {
                RequestId = request.Id,
                CenterId = center.Id,
                DocumentType = d.DocumentType,
                FileUrl = d.FileUrl,
                FileName = d.FileName,
                FileSize = d.FileSize,
                ContentType = d.ContentType,
                UploadedAt = d.UploadedAt,
            }).ToList();
            await verificationRepository.AddRequestAsync(request, ct);
        }

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center.verification_reset",
            EntityType = "StudyCenter",
            EntityId = center.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { centerId = center.Id, legalFields })
        }, ct);
    }

    private static CenterDeletionRequestDto ToDeletionDto(CenterDeletionRequest r) =>
        new(r.Id, r.StudyCenterId, r.Reason, r.Status, r.CreatedAt, r.HandledAt);

    /// <summary>[UC-92] Yêu cầu xóa mới nhất của trung tâm (null nếu chưa gửi).</summary>
    public async Task<CenterDeletionRequestDto?> GetDeletionRequestAsync(Guid userId, CancellationToken ct = default)
    {
        if (deletionRequests is null) return null;
        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null) return null;
        var latest = await deletionRequests.GetLatestByCenterIdAsync(center.Id, ct);
        return latest is null ? null : ToDeletionDto(latest);
    }

    /// <summary>
    /// [UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu; Administrator xử lý ở UC-93.
    /// Mỗi trung tâm chỉ có một yêu cầu đang mở; lý do bắt buộc. Ghi nhật ký.
    /// </summary>
    public async Task<CenterDeletionRequestResult> RequestDeletionAsync(Guid userId, string? reason, CancellationToken ct = default)
    {
        if (deletionRequests is null)
            return CenterDeletionRequestResult.Fail("Chức năng yêu cầu xóa tài khoản tạm thời chưa sẵn sàng.");

        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return CenterDeletionRequestResult.Fail("Vui lòng nhập lý do yêu cầu xóa tài khoản.");
        if (trimmed.Length > MaxDeletionReasonLength)
            return CenterDeletionRequestResult.Fail($"Lý do tối đa {MaxDeletionReasonLength} ký tự.");

        var center = await repository.GetByOwnerOrEmailAsync(userId, null, ct);
        if (center is null)
            return CenterDeletionRequestResult.Fail("Không tìm thấy thông tin trung tâm cho tài khoản này.");

        if (await deletionRequests.GetOpenByCenterIdAsync(center.Id, ct) is not null)
            return CenterDeletionRequestResult.Fail("Trung tâm đã có yêu cầu xóa đang chờ Quản trị viên xử lý.");

        var entity = new CenterDeletionRequest
        {
            StudyCenterId = center.Id,
            RequestedByUserId = userId,
            Reason = trimmed,
            Status = CenterDeletionRequestStatuses.Pending,
        };
        await deletionRequests.AddAsync(entity, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "center.deletion_requested",
            EntityType = "StudyCenter",
            EntityId = center.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { centerId = center.Id, requestId = entity.Id })
        }, ct);

        return CenterDeletionRequestResult.Ok(ToDeletionDto(entity));
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
