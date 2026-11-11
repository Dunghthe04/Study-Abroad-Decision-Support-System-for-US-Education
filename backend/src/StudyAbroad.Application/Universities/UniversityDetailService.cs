using System.Globalization;
using System.Text.Json;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Universities;

public class UniversityDetailService(IUniversityDetailRepository repository) : IUniversityDetailService
{
    public async Task<UniversityDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var data = await repository.GetByCodeAsync(code.Trim().ToUpperInvariant(), ct);
        if (data is null || !data.University.IsActive) return null;

        var university = data.University;
        var media = data.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToList();
        string? MediaUrl(string type) => media.Where(m => m.Type == type)
            .Select(m => PublicUrl(m.Url)).FirstOrDefault(url => url is not null);
        var majors = data.Majors.ToLookup(m => m.OfferingId);
        var offerings = data.Offerings.Select(o => new UniversityOfferingDto(
            o.StudyLevel, o.AcademicYear, o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd,
            o.MinGpa4, o.MinIelts, o.MinToefl, o.MinDuolingo, o.AvgGpa4,
            o.Sat25, o.Sat75, o.Act25, o.Act75, o.ApplicantsTotal, o.AdmittedTotal,
            o.IntlApplicants, o.IntlAdmitted, o.IntlAcceptanceRate, o.SatPolicy, o.DeadlinesJson,
            o.C7GpaImportance, o.C7TestImportance, o.C7EcImportance, o.SatSubmitPct, o.GpaSubmitPct,
            o.GpaPct40, o.GpaPct375To399, o.GpaPct350To374, o.GpaPct325To349, o.GpaPct300To324,
            o.GpaPct250To299, o.GpaPctBelow250, o.IntlAidPolicy, o.IntlAidAvgUsd, o.IntlAidRecipients,
            o.RetentionRate1Yr, o.GradRate6Yr,
            majors[o.Id].OrderBy(m => m.Name).Select(m => new UniversityMajorDto(m.Name, m.CipCode, m.IsStem, m.IsVerified)).ToList(),
            PublicUrl(o.SourceUrl), o.RetrievedAt, o.SourceReliability,
            o.AvgGpaWeighted, o.GpaScaleType, o.FyOnCampusPct, o.UgOnCampusPct, o.HousingTypes,
            o.IntlHousing, o.HousingOnlyUsd, o.HousingDepositUsd, o.StudentFacultyRatio,
            o.Activities, o.CampusSetting)).ToList();
        var sources = data.Evidence.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.FieldName).ThenBy(e => e.Id)
            .Select(MapSource).Where(s => s.SourceUrl is not null)
            .DistinctBy(s => new { s.FieldName, s.SourceUrl, s.FieldValue, s.Status, s.DataYear, s.AppliedToSnapshot, s.Normalization })
            .Take(100).ToList();

        return new UniversityDetailDto(university.Code, university.Name, university.City, university.State,
            data.StateName, PublicUrl(university.Website), university.Control, university.AcceptanceRate,
            university.InternationalStudentCount, university.IsIvy, MediaUrl("logo"), MediaUrl("cover"),
            media.Where(m => m.Type == "gallery").Select(m => PublicUrl(m.Url)).OfType<string>().Distinct().Take(20).ToList(),
            offerings, data.Rankings.Select(r => r with { SourceUrl = PublicUrl(r.SourceUrl) }).ToList(), sources);
    }

    public Task<PagedResult<UniversityListItemDto>> SearchAsync(UniversitySearchQuery query, CancellationToken ct = default) =>
        repository.SearchAsync(query, ct);

    public Task<UniversityFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default) =>
        repository.GetFilterOptionsAsync(ct);

    public static string? PublicUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback || !uri.Host.Contains('.')) return null;
        if (System.Net.IPAddress.TryParse(uri.Host, out _)) return null;
        return value;
    }

    public static UniversitySourceDto MapSource(FieldEvidence evidence)
    {
        DateOnly? retrievedAt = null;
        string? dataYear = null;
        bool? appliedToSnapshot = null;
        string? normalization = null;
        if (!string.IsNullOrWhiteSpace(evidence.ExtractedQuote) && evidence.ExtractedQuote.Length <= 16384)
        {
            try
            {
                using var document = JsonDocument.Parse(evidence.ExtractedQuote);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var date = MetadataText(root, "retrieved_at") ?? MetadataText(root, "retrievedAt");
                    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                        retrievedAt = DateOnly.FromDateTime(parsed);
                    dataYear = MetadataText(root, "data_year") ?? MetadataText(root, "period_year");
                    normalization = MetadataText(root, "normalization");
                    if (root.TryGetProperty("applied_to_snapshot", out var applied) &&
                        (applied.ValueKind == JsonValueKind.True || applied.ValueKind == JsonValueKind.False))
                        appliedToSnapshot = applied.GetBoolean();
                }
            }
            catch (JsonException) { }
        }
        return new UniversitySourceDto(evidence.FieldName,
            appliedToSnapshot == true ? SafeText(evidence.FieldValue, 500) : null,
            PublicUrl(evidence.SourceUrl), SafeText(evidence.SourceSection, 300), evidence.Status,
            retrievedAt, dataYear, appliedToSnapshot, normalization);
    }

    private static string? MetadataText(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String && value.ValueKind != JsonValueKind.Number) return null;
        return SafeText(value.ToString(), 50);
    }

    private static string? SafeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength) return null;
        if (value.Contains("file:", StringComparison.OrdinalIgnoreCase) || value.Contains(":\\") || value.Contains("\\\\")) return null;
        if (value.StartsWith('/') || value.StartsWith('{') || value.StartsWith('[')) return null;
        return value;
    }
}
