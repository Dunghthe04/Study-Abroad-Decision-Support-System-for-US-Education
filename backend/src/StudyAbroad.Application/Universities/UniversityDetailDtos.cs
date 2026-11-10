namespace StudyAbroad.Application.Universities;

public record UniversityDetailDto(
    string Code, string Name, string? City, string? State, string? StateName,
    string? Website, string? Control, decimal? AcceptanceRate, int? InternationalStudentCount,
    bool IsIvy, string? LogoUrl, string? CoverUrl, IReadOnlyList<string> GalleryUrls,
    IReadOnlyList<UniversityOfferingDto> Offerings, IReadOnlyList<UniversityRankingDto> Rankings,
    IReadOnlyList<UniversitySourceDto> Sources);

public record UniversityMajorDto(string Name, string? CipCode, bool IsStem, bool IsVerified);

public record UniversityOfferingDto(
    string StudyLevel, string? AcademicYear,
    decimal? TuitionUsd, decimal? LivingUsd, decimal? FeesUsd, decimal? OtherExpensesUsd,
    decimal? MinGpa4, decimal? MinIelts, decimal? MinToefl, decimal? MinDuolingo, decimal? AvgGpa4,
    int? Sat25, int? Sat75, int? Act25, int? Act75,
    int? ApplicantsTotal, int? AdmittedTotal, int? IntlApplicants, int? IntlAdmitted,
    decimal? IntlAcceptanceRate, string? SatPolicy, string? DeadlinesJson,
    string? C7GpaImportance, string? C7TestImportance, string? C7EcImportance,
    decimal? SatSubmitPct, decimal? GpaSubmitPct, decimal? GpaPct40, decimal? GpaPct375To399,
    decimal? GpaPct350To374, decimal? GpaPct325To349, decimal? GpaPct300To324,
    decimal? GpaPct250To299, decimal? GpaPctBelow250,
    string? IntlAidPolicy, decimal? IntlAidAvgUsd, int? IntlAidRecipients,
    decimal? RetentionRate1Yr, decimal? GradRate6Yr, IReadOnlyList<UniversityMajorDto> Majors,
    string? SourceUrl, DateOnly? RetrievedAt, string? SourceReliability);

public record UniversityRankingDto(
    string Publisher, string Name, int Rank, bool IsTie, string RankDisplay,
    string? Edition, int? EditionYear, string? SourceUrl);

public record UniversitySourceDto(
    string FieldName, string? FieldValue, string? SourceUrl, string? SourceSection,
    string Status, DateOnly? RetrievedAt, string? DataYear, bool? AppliedToSnapshot, string? Normalization);

public record UniversitySearchQuery(
    string? Search = null,
    string? State = null,
    string? Control = null,
    string? Major = null,
    decimal? MaxTuitionUsd = null,
    decimal? MaxTotalCostUsd = null,
    decimal? MaxMinIelts = null,
    decimal? MaxMinToefl = null,
    decimal? MaxMinDuolingo = null,
    decimal? MinAcceptanceRate = null,
    decimal? MaxAcceptanceRate = null,
    bool? IsIvy = null,
    string? SortBy = null,
    string? StudyLevel = "undergraduate",
    int Page = 1,
    int PageSize = 12);

public record UniversityListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? City,
    string? State,
    string? StateName,
    string? Control,
    decimal? AcceptanceRate,
    bool IsIvy,
    string? LogoUrl,
    string? CoverUrl,
    decimal? TuitionUsd,
    decimal? TotalCostUsd,
    decimal? MinIelts,
    decimal? MinToefl,
    decimal? MinDuolingo,
    int? BestRank,
    string? BestRankPublisher,
    string? SatPolicy,
    string? IntlAidPolicy,
    IReadOnlyList<string> Majors);

public record UniversityFilterOptionItem(string Value, string Label);

public record UniversityFilterOptionsDto(
    IReadOnlyList<UniversityFilterOptionItem> States,
    IReadOnlyList<UniversityFilterOptionItem> Controls,
    IReadOnlyList<string> Majors,
    decimal MinTuitionUsd,
    decimal MaxTuitionUsd,
    decimal MinTotalCostUsd,
    decimal MaxTotalCostUsd);
