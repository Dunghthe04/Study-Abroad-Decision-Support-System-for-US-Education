namespace StudyAbroad.Application.Universities;

public record UniversityDetailDto(
    string Code, string Name, string? City, string? State, string? StateName,
    string? Website, string? Control, decimal? AcceptanceRate, int? InternationalStudentCount,
    bool IsIvy, string? LogoUrl, string? CoverUrl, IReadOnlyList<string> GalleryUrls,
    IReadOnlyList<UniversityOfferingDto> Offerings, IReadOnlyList<UniversityRankingDto> Rankings,
    IReadOnlyList<UniversitySourceDto> Sources,
    IReadOnlyList<UniversityImageCreditDto>? ImageCredits = null);

/// <summary>Ghi công ảnh bìa / ảnh trường theo giấy phép (Commons).</summary>
public record UniversityImageCreditDto(string Url, string? License, string? Author, string? SourcePageUrl);

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
    string? SourceUrl, DateOnly? RetrievedAt, string? SourceReliability,
    decimal? AvgGpaWeighted = null,
    string? GpaScaleType = null,
    decimal? FyOnCampusPct = null,
    decimal? UgOnCampusPct = null,
    string? HousingTypes = null,
    bool? IntlHousing = null,
    decimal? HousingOnlyUsd = null,
    decimal? HousingDepositUsd = null,
    int? StudentFacultyRatio = null,
    string? Activities = null,
    string? CampusSetting = null,
    string? ActPolicy = null,
    decimal? ActSubmitPct = null,
    decimal? ApplicationFeeUsd = null,
    decimal? TuitionInStateUsd = null,
    decimal? HealthInsuranceUsd = null,
    // Chi phí một năm theo AnnualCost; null khi chưa có học phí (giao diện ghi chưa đủ dữ liệu, không tự cộng)
    decimal? AnnualCostUsd = null);

public record UniversityRankingDto(
    string Publisher, string Name, int Rank, bool IsTie, string RankDisplay,
    string? Edition, int? EditionYear, string? SourceUrl);

public record UniversitySourceDto(
    string FieldName, string? FieldValue, string? SourceUrl, string? SourceSection,
    string Status, DateOnly? RetrievedAt, string? DataYear, bool? AppliedToSnapshot, string? Normalization);

public record UniversitySearchQuery(
    string? Search = null,
    string? State = null,
    string? Region = null,
    string? Control = null,
    string? Major = null,
    decimal? MinTuitionUsd = null,
    decimal? MaxTuitionUsd = null,
    decimal? MinTotalCostUsd = null,
    decimal? MaxTotalCostUsd = null,
    decimal? MinLivingUsd = null,
    decimal? MaxLivingUsd = null,
    decimal? MaxApplicationFeeUsd = null,
    decimal? MaxMinIelts = null,
    decimal? MaxMinToefl = null,
    decimal? MaxMinDuolingo = null,
    decimal? MaxMinGpa4 = null,
    decimal? MinAvgGpa4 = null,
    decimal? MaxAvgGpa4 = null,
    int? MinSat = null,
    int? MaxSat = null,
    int? MinAct = null,
    int? MaxAct = null,
    string? SatPolicy = null,
    string? ActPolicy = null,
    string? DeadlineRound = null,
    string? C7TestImportance = null,
    string? C7EcImportance = null,
    decimal? MinAcceptanceRate = null,
    decimal? MaxAcceptanceRate = null,
    decimal? MinIntlAcceptanceRate = null,
    decimal? MaxIntlAcceptanceRate = null,
    int? MinStudentFacultyRatio = null,
    int? MaxStudentFacultyRatio = null,
    decimal? MinRetentionRate = null,
    decimal? MaxRetentionRate = null,
    decimal? MinGradRate6Yr = null,
    decimal? MaxGradRate6Yr = null,
    int? MinIntlStudents = null,
    int? MaxIntlStudents = null,
    string? IntlAidPolicy = null,
    decimal? MinIntlAidAvgUsd = null,
    decimal? MaxIntlAidAvgUsd = null,
    decimal? MinFyOnCampusPct = null,
    decimal? MaxFyOnCampusPct = null,
    string? HousingType = null,
    string? Activity = null,
    bool? IsIvy = null,
    bool? TestOptional = null,
    bool? HasIntlAid = null,
    bool? HasScholarship = null,
    bool? IntlHousing = null,
    string? CampusSetting = null,
    int? MaxRank = null,
    string? SortBy = null,
    string? RankListId = null,
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
    string? BestRankList,
    string? SatPolicy,
    string? ActPolicy,
    string? IntlAidPolicy,
    IReadOnlyList<string> Majors);

public record UniversityFilterOptionItem(string Value, string Label);

public record UniversityFilterRangesDto(
    decimal MinLivingUsd,
    decimal MaxLivingUsd,
    int MinSat,
    int MaxSat,
    int MinAct,
    int MaxAct,
    int MinStudentFacultyRatio,
    int MaxStudentFacultyRatio,
    int MinIntlStudents,
    int MaxIntlStudents,
    decimal MaxIntlAidAvgUsd);

public record UniversityFilterOptionsDto(
    IReadOnlyList<UniversityFilterOptionItem> States,
    IReadOnlyList<UniversityFilterOptionItem> Controls,
    IReadOnlyList<UniversityFilterOptionItem> Majors,
    decimal MinTuitionUsd,
    decimal MaxTuitionUsd,
    decimal MinTotalCostUsd,
    decimal MaxTotalCostUsd,
    IReadOnlyList<UniversityFilterOptionItem> RankingLists,
    UniversityFilterRangesDto Ranges,
    IReadOnlyList<string> HousingTypes,
    IReadOnlyList<string> Activities);
