using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Siêu dữ liệu cấu hình biểu mẫu hồ sơ động, tuyệt đối không fix cứng code.
/// </summary>
public record ProfileMetadataResponse(
    [property: JsonPropertyName("studyLevels")] IReadOnlyList<StudyLevelMetadataItem> StudyLevels,
    [property: JsonPropertyName("currentEducationLevels")] IReadOnlyList<CurrentEducationLevelItem> CurrentEducationLevels,
    [property: JsonPropertyName("budgetTiers")] IReadOnlyList<BudgetTierMetadataItem> BudgetTiers,
    [property: JsonPropertyName("scholarshipNeeds")] IReadOnlyList<ScholarshipNeedMetadataItem> ScholarshipNeeds,
    [property: JsonPropertyName("fundingSources")] IReadOnlyList<IdLabelItem> FundingSources,
    [property: JsonPropertyName("educationSystems")] IReadOnlyList<IdLabelItem> EducationSystems,
    [property: JsonPropertyName("gradeScales")] IReadOnlyList<GradeScaleMetadataItem> GradeScales,
    [property: JsonPropertyName("gradeFormTemplates")] Dictionary<string, GradeFormTemplateItem> GradeFormTemplates,
    [property: JsonPropertyName("certifications")] IReadOnlyList<CertificationMetadataItem> Certifications,
    [property: JsonPropertyName("activityCategories")] IReadOnlyList<IdLabelItem> ActivityCategories,
    [property: JsonPropertyName("impactLevels")] IReadOnlyList<ImpactLevelMetadataItem> ImpactLevels,
    [property: JsonPropertyName("activityRoles")] IReadOnlyList<IdLabelItem> ActivityRoles,
    [property: JsonPropertyName("usStates")] IReadOnlyList<UsStateMetadataItem> UsStates,
    [property: JsonPropertyName("popularStateCodes")] IReadOnlyList<string> PopularStateCodes,
    [property: JsonPropertyName("usMajors")] IReadOnlyList<UsMajorMetadataItem> UsMajors,
    [property: JsonPropertyName("institutionTypes")] IReadOnlyList<IdLabelItem> InstitutionTypes
);

public record CurrentEducationLevelItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label
);

public record StudyLevelMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("subName")] string SubName,
    [property: JsonPropertyName("duration")] string Duration,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("defaultGradeScale")] string DefaultGradeScale,
    [property: JsonPropertyName("defaultEducationSystem")] string DefaultEducationSystem
);

public record BudgetTierMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("minUsd")] decimal MinUsd,
    /// <summary>null = không giới hạn trên (mức trên 70k)</summary>
    [property: JsonPropertyName("maxUsd")] decimal? MaxUsd,
    [property: JsonPropertyName("approxUsd")] decimal ApproxUsd,
    [property: JsonPropertyName("description")] string Description
);

public record ScholarshipNeedMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("subLabel")] string SubLabel,
    [property: JsonPropertyName("needsScholarship")] bool NeedsScholarship
);

public record IdLabelItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label
);

public record GradeScaleMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("desc")] string Desc,
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max,
    [property: JsonPropertyName("step")] decimal Step
);

public record GradeFormTemplateItem(
    [property: JsonPropertyName("levelType")] string LevelType,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("defaultGradeScale")] string DefaultGradeScale,
    [property: JsonPropertyName("terms")] IReadOnlyList<TemplateTermItem> Terms,
    [property: JsonPropertyName("defaultSubjects")] IReadOnlyList<TemplateSubjectItem> DefaultSubjects
);

public record TemplateTermItem(
    [property: JsonPropertyName("termOrder")] int TermOrder,
    [property: JsonPropertyName("termName")] string TermName
);

public record TemplateSubjectItem(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("credits")] decimal Credits
);

/// <summary>
/// [USAS-640] Một loại chứng chỉ trong danh mục (app_settings profile.metadata). min/max/step null khi điểm dạng chữ
/// (A-Level); validityYears null là không hết hạn; profileField là cột cũ trên student_profiles được điền điểm cao nhất
/// </summary>
public record CertificationMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("min")] decimal? Min,
    [property: JsonPropertyName("max")] decimal? Max,
    [property: JsonPropertyName("step")] decimal? Step,
    [property: JsonPropertyName("placeholder")] string? Placeholder,
    [property: JsonPropertyName("group")] string Group = "other",
    [property: JsonPropertyName("order")] int Order = 0,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string>? Aliases = null,
    [property: JsonPropertyName("scaleLabel")] string? ScaleLabel = null,
    [property: JsonPropertyName("validityYears")] int? ValidityYears = null,
    [property: JsonPropertyName("sections")] IReadOnlyList<CertificationSectionItem>? Sections = null,
    [property: JsonPropertyName("sectionsHint")] string? SectionsHint = null,
    [property: JsonPropertyName("requiresSubject")] bool RequiresSubject = false,
    [property: JsonPropertyName("subjectLabel")] string? SubjectLabel = null,
    [property: JsonPropertyName("profileField")] string? ProfileField = null
);

/// <summary>Điểm thành phần của một loại chứng chỉ (kỹ năng IELTS, phần thi SAT)</summary>
public record CertificationSectionItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max,
    [property: JsonPropertyName("step")] decimal Step
);

public record ImpactLevelMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("desc")] string Desc
);

public record UsStateMetadataItem(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isPopular")] bool IsPopular = false
);

public record UsMajorMetadataItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("englishName")] string EnglishName,
    [property: JsonPropertyName("category")] string? Category = null
);
