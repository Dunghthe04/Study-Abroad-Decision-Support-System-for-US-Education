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
    Task<VerifyStudyCenterResult> VerifyCenterAsync(Guid id, bool isVerified, Guid actorUserId, CancellationToken ct = default);
}

public class StudyCenterService(
    IStudyCenterRepository repository,
    IAuditLogRepository auditLogs) : IStudyCenterService
{
    private const int MaxPageSize = 100;
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 50;

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
            LicenseNumber = request.LicenseNumber,
            Accreditations = request.Accreditations ?? [],
            VisaSuccessRate = request.VisaSuccessRate,
        };

        await repository.AddAsync(center, ct);

        var detailJson = JsonSerializer.Serialize(new
        {
            code = center.Code,
            name = center.Name,
            city = center.City,
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

    public async Task<IReadOnlyList<string>> GetReferralCitiesAsync(CancellationToken ct = default) =>
        CenterReferral.Cities(await repository.ListReferableAsync(ct));

    internal static StudyCenterDto ToDto(StudyCenter c) =>
        new(c.Id, c.Code, c.Name, c.Website, c.Address, c.City, c.StudyLevels, c.Services, c.SurveyedAt,
            c.Phone, c.Email, c.Description, c.VerificationStatus, c.IsActive,
            c.CenterType, c.LogoUrl, c.CoverImageUrl, c.GalleryImages, c.District,
            c.Latitude, c.Longitude, c.StaffCount, c.EstablishedYear, c.LicenseNumber,
            c.Accreditations, c.VisaSuccessRate, c.AverageRating, c.ReviewCount);
}
