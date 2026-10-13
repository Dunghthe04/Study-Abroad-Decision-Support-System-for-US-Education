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
    [property: JsonPropertyName("hasCommittedProfile")] bool HasCommittedProfile,
    [property: JsonPropertyName("draft")] object? Draft = null,
    [property: JsonPropertyName("lastCompletedStep")] int? LastCompletedStep = 0
);

public record ProfileFlowStepDataDto(
    [property: JsonPropertyName("step1")] Step1GeneralDto? Step1 = null,
    [property: JsonPropertyName("step2")] Step2TranscriptDto? Step2 = null,
    [property: JsonPropertyName("step3")] Step3CertificationsDto? Step3 = null,
    [property: JsonPropertyName("step4")] Step4FinancialDto? Step4 = null,
    [property: JsonPropertyName("step5")] Step5ActivitiesDto? Step5 = null
);

public record Step1GeneralDto(
    [property: JsonPropertyName("targetLevel")] string? TargetLevel = null,
    [property: JsonPropertyName("currentEducationLevel")] string? CurrentEducationLevel = null,
    [property: JsonPropertyName("currentSchool")] string? CurrentSchool = null,
    [property: JsonPropertyName("currentGrade")] string? CurrentGrade = null,
    [property: JsonPropertyName("graduationYear")] int? GraduationYear = null,
    [property: JsonPropertyName("intendedMajor")] string? IntendedMajor = null,
    [property: JsonPropertyName("preferredStates")] List<string>? PreferredStates = null,
    [property: JsonPropertyName("institutionType")] string? InstitutionType = null
);

public record Step2TranscriptDto(
    [property: JsonPropertyName("educationSystem")] string? EducationSystem = null,
    [property: JsonPropertyName("gradeScale")] string? GradeScale = null,
    [property: JsonPropertyName("calculatedGpa4")] decimal? CalculatedGpa4 = null,
    [property: JsonPropertyName("terms")] List<FlowTranscriptTermDto>? Terms = null
);

public record FlowTranscriptTermDto(
    [property: JsonPropertyName("termOrder")] int TermOrder = 1,
    [property: JsonPropertyName("termName")] string? TermName = null,
    [property: JsonPropertyName("scores")] List<FlowTranscriptSubjectScoreDto>? Scores = null
);

public record FlowTranscriptSubjectScoreDto(
    [property: JsonPropertyName("subject")] string? Subject = null,
    [property: JsonPropertyName("score")] decimal Score = 0m,
    [property: JsonPropertyName("rawScore")] string? RawScore = null,
    [property: JsonPropertyName("credits")] decimal? Credits = 1m
);

public record Step3CertificationsDto(
    [property: JsonPropertyName("englishTestType")] string? EnglishTestType = null,
    [property: JsonPropertyName("ielts")] decimal? Ielts = null,
    [property: JsonPropertyName("toefl")] decimal? Toefl = null,
    [property: JsonPropertyName("duolingo")] decimal? Duolingo = null,
    [property: JsonPropertyName("englishExamDate")] string? EnglishExamDate = null,
    [property: JsonPropertyName("selectedStandardizedTests")] List<string>? SelectedStandardizedTests = null,
    [property: JsonPropertyName("sat")] decimal? Sat = null,
    [property: JsonPropertyName("act")] decimal? Act = null,
    [property: JsonPropertyName("ap")] decimal? Ap = null,
    [property: JsonPropertyName("gre")] decimal? Gre = null,
    [property: JsonPropertyName("gmat")] decimal? Gmat = null,
    [property: JsonPropertyName("otherTestsJson")] string? OtherTestsJson = null
);

public record Step4FinancialDto(
    [property: JsonPropertyName("budgetTierId")] string? BudgetTierId = null,
    [property: JsonPropertyName("annualBudgetUsd")] decimal? AnnualBudgetUsd = null,
    [property: JsonPropertyName("scholarshipNeedId")] string? ScholarshipNeedId = null,
    [property: JsonPropertyName("fundingSourceId")] string? FundingSourceId = null,
    [property: JsonPropertyName("financialNotes")] string? FinancialNotes = null
);

public record Step5ActivitiesDto(
    [property: JsonPropertyName("items")] List<FlowActivityItemDto>? Items = null
);

public record FlowActivityItemDto(
    [property: JsonPropertyName("id")] Guid? Id = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("role")] string? Role = null,
    [property: JsonPropertyName("organization")] string? Organization = null,
    [property: JsonPropertyName("impactLevel")] int? ImpactLevel = 1,
    [property: JsonPropertyName("durationMonths")] int? DurationMonths = null,
    [property: JsonPropertyName("year")] string? Year = null,
    [property: JsonPropertyName("description")] string? Description = null
);

public record DraftTranscriptInfoDto(
    [property: JsonPropertyName("originalFileName")] string OriginalFileName,
    [property: JsonPropertyName("storedFileName")] string StoredFileName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileSizeBytes")] long FileSizeBytes,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("uploadedAt")] DateTime UploadedAt
);

public record UploadDraftTranscriptResultDto(
    [property: JsonPropertyName("draftTranscript")] DraftTranscriptInfoDto DraftTranscript,
    [property: JsonPropertyName("extractedTerms")] IReadOnlyList<FlowTranscriptTermDto> ExtractedTerms,
    [property: JsonPropertyName("calculatedGpa4")] decimal? CalculatedGpa4,
    [property: JsonPropertyName("ocrMessage")] string? OcrMessage,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string>? Warnings = null
);


public record SaveProfileDraftRequest(
    [property: JsonPropertyName("currentStep")] int CurrentStep,
    [property: JsonPropertyName("completedSteps")] List<int>? CompletedSteps = null,
    [property: JsonPropertyName("stepData")] ProfileFlowStepDataDto? StepData = null,
    [property: JsonPropertyName("flowState")] object? FlowState = null,
    [property: JsonPropertyName("draft")] object? Draft = null
);

public record CommitProfileFlowRequest(
    [property: JsonPropertyName("stepData")] ProfileFlowStepDataDto? StepData = null,
    [property: JsonPropertyName("finalDraft")] object? FinalDraft = null
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
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("success")] bool Success = true
);
