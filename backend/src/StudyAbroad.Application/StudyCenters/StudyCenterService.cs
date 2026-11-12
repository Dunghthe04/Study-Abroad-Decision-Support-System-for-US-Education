using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
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
}

public class StudyCenterService(
    IStudyCenterRepository repository,
    IAuditLogRepository auditLogs) : IStudyCenterService
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
            c.LegalDocumentUrls, c.VerificationNote);

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
}
