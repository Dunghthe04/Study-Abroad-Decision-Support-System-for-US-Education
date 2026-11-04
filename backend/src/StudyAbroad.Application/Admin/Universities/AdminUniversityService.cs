using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Universities;

public interface IAdminUniversityService
{
    /// <summary>[UC-65] Danh sách trường cho admin/moderator — tìm, lọc, phân trang.</summary>
    Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default);
    Task<AdminUniversityDetailDto?> GetAsync(Guid universityId, CancellationToken ct = default);
    /// <summary>null nếu trường không tồn tại.</summary>
    Task<IReadOnlyList<AdminUniversityOfferingDto>?> GetOfferingsAsync(Guid universityId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminMajorOptionDto>> ListMajorsAsync(CancellationToken ct = default);
    /// <summary>[UC-66] Sửa thông tin trường (kèm ghi audit).</summary>
    Task<AdminUpdateResult<AdminUniversityDetailDto>> UpdateUniversityAsync(
        Guid universityId, UpdateUniversityRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-66] Sửa một bậc học của trường, gồm cả danh sách ngành (kèm ghi audit).</summary>
    Task<AdminUpdateResult<AdminUniversityOfferingDto>> UpdateOfferingAsync(
        Guid universityId, Guid offeringId, UpdateOfferingRequest request, Guid actorUserId, CancellationToken ct = default);
    /// <summary>[UC-67] Ẩn trường khỏi tìm kiếm và gợi ý, hoặc hiện lại (kèm ghi audit).</summary>
    Task<AdminUniversityDetailDto?> SetVisibilityAsync(Guid universityId, bool isActive, Guid actorUserId, CancellationToken ct = default);
}

public class AdminUniversityService(
    IAdminUniversityRepository repository,
    IAuditLogRepository auditLogs) : IAdminUniversityService
{
    private const int MaxPageSize = 100;
    private static readonly string[] Controls = ["public", "private"];
    private static readonly string[] SatPolicies = ["required", "optional", "not_accepted"];
    private static readonly string[] SourceReliabilities = ["official", "aggregated", "self_reported"];
    private static readonly Regex StateCode = new("^[A-Z]{2}$");

    public Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };
        return repository.SearchAsync(normalized, ct);
    }

    public async Task<AdminUniversityDetailDto?> GetAsync(Guid universityId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return null;
        return await ToDetailDtoAsync(university, ct);
    }

    public async Task<IReadOnlyList<AdminUniversityOfferingDto>?> GetOfferingsAsync(Guid universityId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return null;

        var offerings = await repository.ListOfferingsAsync(universityId, ct);
        var offeringIds = offerings.Select(o => o.Id).ToList();
        var majorRows = await repository.ListMajorRowsAsync(offeringIds, ct);

        var result = new List<AdminUniversityOfferingDto>();
        foreach (var offering in offerings)
        {
            var rowsOfOffering = majorRows.Where(r => r.UniversityOfferingId == offering.Id).ToList();
            var lastEditedBy = await repository.GetUserDisplayNameAsync(offering.LastEditedByUserId, ct);
            result.Add(ToOfferingDto(offering, rowsOfOffering, lastEditedBy));
        }
        return result;
    }

    public async Task<IReadOnlyList<AdminMajorOptionDto>> ListMajorsAsync(CancellationToken ct = default)
    {
        var majors = await repository.ListMajorsAsync(ct);
        return majors.Select(m => new AdminMajorOptionDto(m.Id, m.Name, m.CipCode)).ToList();
    }

    public async Task<AdminUpdateResult<AdminUniversityDetailDto>> UpdateUniversityAsync(
        Guid universityId, UpdateUniversityRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return AdminUpdateResult<AdminUniversityDetailDto>.Missing();

        var name = Clean(request.Name);
        var city = Clean(request.City);
        var state = Clean(request.State)?.ToUpperInvariant();
        var website = Clean(request.Website);
        var control = Clean(request.Control)?.ToLowerInvariant();
        var logoUrl = Clean(request.LogoUrl);
        var coverUrl = Clean(request.CoverUrl);

        var errors = new Dictionary<string, string>();
        if (name is null)
            errors["name"] = "Tên trường không được để trống.";
        else if (name.Length > 256)
            errors["name"] = "Tên trường tối đa 256 ký tự.";
        if (city is not null && city.Length > 128)
            errors["city"] = "Thành phố tối đa 128 ký tự.";
        if (state is not null && !StateCode.IsMatch(state))
            errors["state"] = "Mã bang gồm 2 chữ cái, ví dụ CA.";
        if (website is not null && (website.Length > 512 || !IsHttpUrl(website)))
            errors["website"] = "Website phải là đường dẫn http(s), tối đa 512 ký tự.";
        if (logoUrl is not null && (logoUrl.Length > 1000 || !IsHttpUrl(logoUrl)))
            errors["logoUrl"] = "Logo phải là đường dẫn http(s), tối đa 1000 ký tự.";
        if (coverUrl is not null && (coverUrl.Length > 1000 || !IsHttpUrl(coverUrl)))
            errors["coverUrl"] = "Ảnh bìa phải là đường dẫn http(s), tối đa 1000 ký tự.";
        if (control is not null && !Controls.Contains(control))
            errors["control"] = "Loại trường chỉ nhận public hoặc private.";
        if (request.AcceptanceRate is < 0 or > 1)
            errors["acceptanceRate"] = "Tỷ lệ nhận phải trong khoảng 0–1.";
        if (request.InternationalStudentCount is < 0)
            errors["internationalStudentCount"] = "Số du học sinh không được âm.";
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityDetailDto>.Invalid(errors);

        var currentMedia = await repository.GetUniversityMediaAsync(university.Id, ct);
        var currentLogo = currentMedia.FirstOrDefault(m => m.Type == "logo")?.Url;
        var currentCover = currentMedia.FirstOrDefault(m => m.Type == "cover")?.Url;

        var changes = new Dictionary<string, object?[]>();
        AddChange(changes, "name", university.Name, name);
        AddChange(changes, "city", university.City, city);
        AddChange(changes, "state", university.State, state);
        AddChange(changes, "website", university.Website, website);
        AddChange(changes, "control", university.Control, control);
        AddChange(changes, "acceptanceRate", university.AcceptanceRate, request.AcceptanceRate);
        AddChange(changes, "internationalStudentCount", university.InternationalStudentCount, request.InternationalStudentCount);
        AddChange(changes, "isIvy", university.IsIvy, request.IsIvy);
        AddChange(changes, "logoUrl", currentLogo, logoUrl);
        AddChange(changes, "coverUrl", currentCover, coverUrl);

        if (changes.Count == 0) return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));

        university.Name = name!;
        university.City = city;
        university.State = state;
        university.Website = website;
        university.Control = control;
        university.AcceptanceRate = request.AcceptanceRate;
        university.InternationalStudentCount = request.InternationalStudentCount;
        university.IsIvy = request.IsIvy;
        university.LastEditedByUserId = actorUserId;

        await repository.SaveUniversityMediaAsync(university.Id, logoUrl, coverUrl, ct);
        await repository.SaveChangesAsync(ct);

        await WriteAuditAsync(actorUserId, "university.update", "university", university.Id, new { code = university.Code, changes }, ct);
        return AdminUpdateResult<AdminUniversityDetailDto>.Ok(await ToDetailDtoAsync(university, ct));
    }

    public async Task<AdminUpdateResult<AdminUniversityOfferingDto>> UpdateOfferingAsync(
        Guid universityId, Guid offeringId, UpdateOfferingRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var offering = await repository.GetOfferingAsync(universityId, offeringId, ct);
        if (offering is null) return AdminUpdateResult<AdminUniversityOfferingDto>.Missing();

        var academicYear = Clean(request.AcademicYear);
        var satPolicy = Clean(request.SatPolicy)?.ToLowerInvariant();
        var sourceUrl = Clean(request.SourceUrl);
        var sourceReliability = Clean(request.SourceReliability)?.ToLowerInvariant();
        var notes = Clean(request.Notes);
        var requestedMajors = request.Majors ?? [];

        var errors = ValidateOffering(request, academicYear, satPolicy, sourceUrl, notes);
        if (sourceReliability is not null && !SourceReliabilities.Contains(sourceReliability))
            errors["sourceReliability"] = "Mức tin cậy chỉ nhận official, aggregated hoặc self_reported.";
        var knownMajors = await repository.ListMajorsAsync(ct);
        var knownMajorIds = knownMajors.Select(m => m.Id).ToHashSet();
        var seenMajorIds = new HashSet<Guid>();
        foreach (var major in requestedMajors)
        {
            if (!knownMajorIds.Contains(major.MajorId))
                errors["majors"] = "Có ngành không nằm trong danh mục ngành.";
            else if (!seenMajorIds.Add(major.MajorId))
                errors["majors"] = "Một ngành bị chọn hai lần.";
        }
        if (errors.Count > 0) return AdminUpdateResult<AdminUniversityOfferingDto>.Invalid(errors);

        var changes = new Dictionary<string, object?[]>();
        AddChange(changes, "academicYear", offering.AcademicYear, academicYear);
        AddChange(changes, "tuitionUsd", offering.TuitionUsd, request.TuitionUsd);
        AddChange(changes, "livingUsd", offering.LivingUsd, request.LivingUsd);
        AddChange(changes, "feesUsd", offering.FeesUsd, request.FeesUsd);
        AddChange(changes, "otherExpensesUsd", offering.OtherExpensesUsd, request.OtherExpensesUsd);
        AddChange(changes, "minGpa4", offering.MinGpa4, request.MinGpa4);
        AddChange(changes, "minIelts", offering.MinIelts, request.MinIelts);
        AddChange(changes, "minToefl", offering.MinToefl, request.MinToefl);
        AddChange(changes, "minDuolingo", offering.MinDuolingo, request.MinDuolingo);
        AddChange(changes, "avgGpa4", offering.AvgGpa4, request.AvgGpa4);
        AddChange(changes, "sat25", offering.Sat25, request.Sat25);
        AddChange(changes, "sat75", offering.Sat75, request.Sat75);
        AddChange(changes, "act25", offering.Act25, request.Act25);
        AddChange(changes, "act75", offering.Act75, request.Act75);
        AddChange(changes, "satPolicy", offering.SatPolicy, satPolicy);
        AddChange(changes, "sourceUrl", offering.SourceUrl, sourceUrl);
        AddChange(changes, "retrievedAt", offering.RetrievedAt, request.RetrievedAt);
        AddChange(changes, "sourceReliability", offering.SourceReliability, sourceReliability);
        AddChange(changes, "notes", offering.Notes, notes);

        offering.AcademicYear = academicYear;
        offering.TuitionUsd = request.TuitionUsd;
        offering.LivingUsd = request.LivingUsd;
        offering.FeesUsd = request.FeesUsd;
        offering.OtherExpensesUsd = request.OtherExpensesUsd;
        offering.MinGpa4 = request.MinGpa4;
        offering.MinIelts = request.MinIelts;
        offering.MinToefl = request.MinToefl;
        offering.MinDuolingo = request.MinDuolingo;
        offering.AvgGpa4 = request.AvgGpa4;
        offering.Sat25 = request.Sat25;
        offering.Sat75 = request.Sat75;
        offering.Act25 = request.Act25;
        offering.Act75 = request.Act75;
        offering.SatPolicy = satPolicy;
        offering.SourceUrl = sourceUrl;
        offering.RetrievedAt = request.RetrievedAt;
        offering.SourceReliability = sourceReliability;
        offering.Notes = notes;

        var majorChanges = await ApplyMajorChangesAsync(offering.Id, requestedMajors, knownMajors, ct);
        if (majorChanges.Count > 0)
            changes["majors"] = [.. majorChanges];

        if (changes.Count > 0)
        {
            offering.UpdatedAt = DateTime.UtcNow;
            offering.LastEditedByUserId = actorUserId;
            await repository.SaveChangesAsync(ct);
            await WriteAuditAsync(actorUserId, "university_offering.update", "university_offering", offering.Id,
                new { universityId, studyLevel = offering.StudyLevel, changes }, ct);
        }

        var majorRows = await repository.ListMajorRowsAsync([offering.Id], ct);
        var lastEditedBy = await repository.GetUserDisplayNameAsync(offering.LastEditedByUserId, ct);
        return AdminUpdateResult<AdminUniversityOfferingDto>.Ok(ToOfferingDto(offering, majorRows, lastEditedBy));
    }

    public async Task<AdminUniversityDetailDto?> SetVisibilityAsync(Guid universityId, bool isActive, Guid actorUserId, CancellationToken ct = default)
    {
        var university = await repository.GetUniversityAsync(universityId, ct);
        if (university is null) return null;

        // Bấm lại cùng trạng thái là thao tác hợp lệ, chỉ không ghi thêm audit.
        if (university.IsActive == isActive) return await ToDetailDtoAsync(university, ct);

        university.IsActive = isActive;
        university.LastEditedByUserId = actorUserId;
        await repository.SaveChangesAsync(ct);

        var action = isActive ? "university.show" : "university.hide";
        await WriteAuditAsync(actorUserId, action, "university", university.Id,
            new { code = university.Code, from = !isActive, to = isActive }, ct);
        return await ToDetailDtoAsync(university, ct);
    }

    private static Dictionary<string, string> ValidateOffering(
        UpdateOfferingRequest request, string? academicYear, string? satPolicy, string? sourceUrl, string? notes)
    {
        var errors = new Dictionary<string, string>();

        if (academicYear is not null && academicYear.Length > 16)
            errors["academicYear"] = "Năm học tối đa 16 ký tự, ví dụ 2026-27.";
        if (request.TuitionUsd is < 0) errors["tuitionUsd"] = "Học phí không được âm.";
        if (request.LivingUsd is < 0) errors["livingUsd"] = "Chi phí sinh hoạt không được âm.";
        if (request.FeesUsd is < 0) errors["feesUsd"] = "Phí bắt buộc không được âm.";
        if (request.OtherExpensesUsd is < 0) errors["otherExpensesUsd"] = "Chi phí khác không được âm.";

        if (request.MinGpa4 is < 0 or > 4) errors["minGpa4"] = "GPA tối thiểu phải trong thang 0–4.";
        if (request.AvgGpa4 is < 0 or > 4) errors["avgGpa4"] = "GPA trung bình phải trong thang 0–4.";
        if (request.MinIelts is < 0 or > 9) errors["minIelts"] = "IELTS phải trong khoảng 0–9.";
        if (request.MinToefl is < 0 or > 120) errors["minToefl"] = "TOEFL iBT phải trong khoảng 0–120.";
        if (request.MinDuolingo is < 10 or > 160) errors["minDuolingo"] = "Duolingo phải trong khoảng 10–160.";

        // SAT: công thức gợi ý cần đủ cả 2 mốc, nên nhập đủ cả cặp hoặc để trống cả cặp.
        var hasSat25 = request.Sat25 is not null;
        var hasSat75 = request.Sat75 is not null;
        if (hasSat25 != hasSat75)
            errors["sat25"] = "Nhập đủ cả SAT 25% và 75%, hoặc để trống cả hai.";
        else if (hasSat25 && (request.Sat25 < 400 || request.Sat75 > 1600 || request.Sat25 > request.Sat75))
            errors["sat25"] = "SAT phải trong khoảng 400–1600 và mốc 25% không lớn hơn mốc 75%.";

        if (request.Act25 is < 1 or > 36) errors["act25"] = "ACT phải trong khoảng 1–36.";
        if (request.Act75 is < 1 or > 36) errors["act75"] = "ACT phải trong khoảng 1–36.";
        if (request.Act25 is not null && request.Act75 is not null && request.Act25 > request.Act75)
            errors["act25"] = "ACT mốc 25% không được lớn hơn mốc 75%.";

        if (satPolicy is not null && !SatPolicies.Contains(satPolicy))
            errors["satPolicy"] = "Chính sách SAT chỉ nhận required, optional hoặc not_accepted.";
        if (sourceUrl is not null && (sourceUrl.Length > 512 || !IsHttpUrl(sourceUrl)))
            errors["sourceUrl"] = "Nguồn phải là đường dẫn http(s), tối đa 512 ký tự.";
        if (notes is not null && notes.Length > 2000)
            errors["notes"] = "Ghi chú tối đa 2000 ký tự.";

        return errors;
    }

    /// <summary>Đưa danh sách ngành của offering về đúng danh sách gửi lên; trả về các thay đổi để ghi audit.</summary>
    private async Task<List<object?>> ApplyMajorChangesAsync(
        Guid offeringId, IReadOnlyList<UpdateOfferingMajor> requestedMajors, IReadOnlyList<Major> knownMajors, CancellationToken ct)
    {
        var changes = new List<object?>();
        var currentLinks = await repository.GetMajorLinksAsync(offeringId, ct);
        var requestedById = requestedMajors.ToDictionary(m => m.MajorId);
        var nameById = knownMajors.ToDictionary(m => m.Id, m => m.Name);

        foreach (var link in currentLinks)
        {
            if (!requestedById.TryGetValue(link.MajorId, out var requested))
            {
                repository.RemoveMajorLink(link);
                changes.Add(new { major = nameById.GetValueOrDefault(link.MajorId), action = "remove" });
            }
            else if (link.IsStem != requested.IsStem)
            {
                link.IsStem = requested.IsStem;
                changes.Add(new { major = nameById.GetValueOrDefault(link.MajorId), action = "stem", to = requested.IsStem });
            }
        }

        var currentMajorIds = currentLinks.Select(l => l.MajorId).ToHashSet();
        foreach (var requested in requestedMajors)
        {
            if (currentMajorIds.Contains(requested.MajorId)) continue;

            // Ngành do moderator thêm tay coi là đã kiểm tra.
            repository.AddMajorLink(new UniversityOfferingMajor
            {
                UniversityOfferingId = offeringId,
                MajorId = requested.MajorId,
                IsStem = requested.IsStem,
                IsVerified = true,
            });
            changes.Add(new { major = nameById.GetValueOrDefault(requested.MajorId), action = "add", stem = requested.IsStem });
        }
        return changes;
    }

    private Task WriteAuditAsync(Guid actorUserId, string action, string entityType, Guid entityId, object detail, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            DetailJson = JsonSerializer.Serialize(detail),
        }, ct);

    private static void AddChange<T>(Dictionary<string, object?[]> changes, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            changes[field] = [oldValue, newValue];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task<AdminUniversityDetailDto> ToDetailDtoAsync(University u, CancellationToken ct)
    {
        var lastEditedBy = await repository.GetUserDisplayNameAsync(u.LastEditedByUserId, ct);
        var medias = await repository.GetUniversityMediaAsync(u.Id, ct);
        var logoUrl = medias.FirstOrDefault(m => m.Type == "logo")?.Url;
        var coverUrl = medias.FirstOrDefault(m => m.Type == "cover")?.Url;
        var galleryUrls = medias.Where(m => m.Type == "gallery").OrderBy(m => m.DisplayOrder).Select(m => m.Url).ToList();

        return new AdminUniversityDetailDto(
            u.Id, u.Code, u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate,
            u.InternationalStudentCount, u.IsIvy, u.IsActive, u.UpdatedAt, lastEditedBy,
            logoUrl, coverUrl, galleryUrls);
    }

    private static AdminUniversityOfferingDto ToOfferingDto(
        UniversityOffering o, IReadOnlyList<AdminOfferingMajorRow> majorRows, string? lastEditedBy)
    {
        var items = majorRows
            .OrderByDescending(r => r.IsStem)
            .ThenBy(r => r.Name)
            .Select(r => new AdminOfferingMajorDto(r.MajorId, r.Name, r.CipCode, r.IsStem, r.IsVerified))
            .ToList();
        var majors = items.Select(i => i.Name).ToList();
        var stemMajors = items.Where(i => i.IsStem).Select(i => i.Name).ToList();

        return new AdminUniversityOfferingDto(
            o.Id, o.StudyLevel, o.AcademicYear, o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd,
            o.MinGpa4, o.MinIelts, o.MinToefl, o.MinDuolingo, o.AvgGpa4, o.Sat25, o.Sat75, o.Act25, o.Act75,
            o.SatPolicy, majors, stemMajors, items, o.DeadlinesJson, o.Notes, o.SourceUrl, o.RetrievedAt,
            o.SourceReliability, o.UpdatedAt, lastEditedBy);
    }
}
