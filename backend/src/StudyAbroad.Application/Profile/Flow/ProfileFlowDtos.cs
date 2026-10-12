using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Trạng thái bản nháp của toàn bộ luồng nhập hồ sơ 6 bước.
/// Bỏ dở giữa chừng có thể khôi phục lại đúng bước và dữ liệu đang làm.
/// </summary>
public record ProfileFlowStateDto(
    [property: JsonPropertyName("currentStep")] int CurrentStep,
    [property: JsonPropertyName("completedSteps")] IReadOnlyList<int> CompletedSteps,
    [property: JsonPropertyName("stepData")] ProfileFlowStepDataDto StepData,
    [property: JsonPropertyName("draftTranscript")] DraftTranscriptInfoDto? DraftTranscript,
    [property: JsonPropertyName("lastUpdatedAt")] DateTime? LastUpdatedAt,
    [property: JsonPropertyName("hasCommittedProfile")] bool HasCommittedProfile
);

public record ProfileFlowStepDataDto(
    [property: JsonPropertyName("step1")] Step1GeneralDto? Step1 = null,
    [property: JsonPropertyName("step2")] Step2TranscriptDto? Step2 = null,
    [property: JsonPropertyName("step3")] Step3CertificationsDto? Step3 = null,
    [property: JsonPropertyName("step4")] Step4FinancialDto? Step4 = null,
    [property: JsonPropertyName("step5")] Step5ActivitiesDto? Step5 = null
);

public record Step1GeneralDto(
    [property: JsonPropertyName("targetLevel")] string? TargetLevel,
    [property: JsonPropertyName("currentEducationLevel")] string? CurrentEducationLevel,
    [property: JsonPropertyName("currentSchool")] string? CurrentSchool,
    [property: JsonPropertyName("currentGrade")] string? CurrentGrade,
    [property: JsonPropertyName("graduationYear")] int? GraduationYear,
    [property: JsonPropertyName("intendedMajor")] string? IntendedMajor,
    [property: JsonPropertyName("preferredStates")] List<string> PreferredStates,
    [property: JsonPropertyName("institutionType")] string? InstitutionType
);

public record Step2TranscriptDto(
    [property: JsonPropertyName("educationSystem")] string? EducationSystem,
    [property: JsonPropertyName("gradeScale")] string? GradeScale,
    [property: JsonPropertyName("calculatedGpa4")] decimal? CalculatedGpa4,
    [property: JsonPropertyName("terms")] List<FlowTranscriptTermDto> Terms
);

public record FlowTranscriptTermDto(
    [property: JsonPropertyName("termOrder")] int TermOrder,
    [property: JsonPropertyName("termName")] string TermName,
    [property: JsonPropertyName("scores")] List<FlowTranscriptSubjectScoreDto> Scores
);

public record FlowTranscriptSubjectScoreDto(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("score")] decimal Score,
    [property: JsonPropertyName("rawScore")] string? RawScore = null,
    [property: JsonPropertyName("credits")] decimal? Credits = 1m
);

public record Step3CertificationsDto(
    [property: JsonPropertyName("englishTestType")] string? EnglishTestType,
    [property: JsonPropertyName("ielts")] decimal? Ielts,
    [property: JsonPropertyName("toefl")] decimal? Toefl,
    [property: JsonPropertyName("duolingo")] decimal? Duolingo,
    [property: JsonPropertyName("englishExamDate")] string? EnglishExamDate,
    [property: JsonPropertyName("selectedStandardizedTests")] List<string> SelectedStandardizedTests,
    [property: JsonPropertyName("sat")] decimal? Sat,
    [property: JsonPropertyName("act")] decimal? Act,
    [property: JsonPropertyName("ap")] decimal? Ap,
    [property: JsonPropertyName("gre")] decimal? Gre,
    [property: JsonPropertyName("gmat")] decimal? Gmat,
    [property: JsonPropertyName("otherTestsJson")] string? OtherTestsJson = null
);

public record Step4FinancialDto(
    [property: JsonPropertyName("budgetTierId")] string? BudgetTierId,
    [property: JsonPropertyName("annualBudgetUsd")] decimal? AnnualBudgetUsd,
    [property: JsonPropertyName("scholarshipNeedId")] string? ScholarshipNeedId,
    [property: JsonPropertyName("fundingSourceId")] string? FundingSourceId,
    [property: JsonPropertyName("financialNotes")] string? FinancialNotes
);

public record Step5ActivitiesDto(
    [property: JsonPropertyName("items")] List<FlowActivityItemDto> Items
);

public record FlowActivityItemDto(
    [property: JsonPropertyName("id")] Guid? Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("organization")] string? Organization,
    [property: JsonPropertyName("impactLevel")] int ImpactLevel,
    [property: JsonPropertyName("durationMonths")] int? DurationMonths,
    [property: JsonPropertyName("year")] string? Year,
    [property: JsonPropertyName("description")] string? Description
);

public record DraftTranscriptInfoDto(
    [property: JsonPropertyName("originalFileName")] string OriginalFileName,
    [property: JsonPropertyName("storedFileName")] string StoredFileName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileSizeBytes")] long FileSizeBytes,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("uploadedAt")] DateTime UploadedAt
);

public record SaveProfileDraftRequest(
    [property: JsonPropertyName("currentStep")] int CurrentStep,
    [property: JsonPropertyName("completedSteps")] List<int>? CompletedSteps,
    [property: JsonPropertyName("stepData")] ProfileFlowStepDataDto StepData
);

public record CommitProfileFlowRequest(
    [property: JsonPropertyName("stepData")] ProfileFlowStepDataDto? StepData = null
);

public record CommitProfileFlowResponse(
    [property: JsonPropertyName("succeeded")] bool Succeeded,
    [property: JsonPropertyName("profileId")] Guid ProfileId,
    [property: JsonPropertyName("targetLevel")] string TargetLevel,
    [property: JsonPropertyName("intendedMajor")] string? IntendedMajor,
    [property: JsonPropertyName("overallGpa")] decimal? OverallGpa,
    [property: JsonPropertyName("extracurricularScore")] decimal? ExtracurricularScore,
    [property: JsonPropertyName("annualBudgetUsd")] decimal? AnnualBudgetUsd,
    [property: JsonPropertyName("transcriptFileUrl")] string? TranscriptFileUrl,
    [property: JsonPropertyName("message")] string Message
);
