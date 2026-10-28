using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Trạng thái luồng nhập hồ sơ 6 bước. Nháp chỉ lưu 1 định dạng: đúng shape draft của FE.
/// </summary>
public record ProfileFlowStateDto(
    [property: JsonPropertyName("currentStep")] int CurrentStep,
    [property: JsonPropertyName("completedSteps")] IReadOnlyList<int> CompletedSteps,
    [property: JsonPropertyName("draft")] ProfileFlowDraftDto Draft,
    [property: JsonPropertyName("draftTranscript")] DraftTranscriptFileDto? DraftTranscript,
    [property: JsonPropertyName("lastUpdatedAt")] DateTime? LastUpdatedAt,
    [property: JsonPropertyName("hasCommittedProfile")] bool HasCommittedProfile = false,
    [property: JsonPropertyName("lastCompletedStep")] int LastCompletedStep = 0
);

/// <summary>Draft 5 bước nhập liệu (bước 6 là xem lại). Mirror `frontend/src/types/profile-flow.ts`.</summary>
public record ProfileFlowDraftDto(
    [property: JsonPropertyName("step1")] FlowGoalDraftDto? Step1 = null,
    [property: JsonPropertyName("step2")] FlowTranscriptDraftDto? Step2 = null,
    [property: JsonPropertyName("step3")] FlowCertificationDraftDto? Step3 = null,
    [property: JsonPropertyName("step4")] FlowFinancialDraftDto? Step4 = null,
    [property: JsonPropertyName("step5")] FlowActivitiesDraftDto? Step5 = null
);

/// <summary>
/// Bước 1 – Mục tiêu. currentEducationLevel: middle_school | high_school | college_university | master;
/// educationSystem: mã EducationSystems, chỉ hỏi học sinh cấp 3; targetIntake: kỳ nhập học dạng "Fall 2027"
/// </summary>
public record FlowGoalDraftDto(
    [property: JsonPropertyName("currentEducationLevel")] string? CurrentEducationLevel = null,
    [property: JsonPropertyName("currentGrade")] string? CurrentGrade = null,
    [property: JsonPropertyName("educationSystem")] string? EducationSystem = null,
    [property: JsonPropertyName("currentSchool")] string? CurrentSchool = null,
    [property: JsonPropertyName("expectedGraduationYear")] int? ExpectedGraduationYear = null,
    [property: JsonPropertyName("targetDegreeLevel")] string? TargetDegreeLevel = null,
    [property: JsonPropertyName("targetMajor")] string? TargetMajor = null,
    [property: JsonPropertyName("targetIntake")] string? TargetIntake = null,
    [property: JsonPropertyName("targetStates")] List<string>? TargetStates = null,
    [property: JsonPropertyName("targetSchoolType")] string? TargetSchoolType = null
);

/// <summary>Bước 2 – Bảng điểm theo khối/năm, mỗi môn có điểm HK1, HK2 (thang 10)</summary>
public record FlowTranscriptDraftDto(
    [property: JsonPropertyName("academicYears")] List<FlowAcademicYearDraftDto>? AcademicYears = null,
    [property: JsonPropertyName("calculatedGpa4Scale")] decimal? CalculatedGpa4Scale = null,
    [property: JsonPropertyName("manualGpa10Scale")] decimal? ManualGpa10Scale = null,
    [property: JsonPropertyName("gpaMode")] string? GpaMode = null
);

public record FlowAcademicYearDraftDto(
    [property: JsonPropertyName("gradeName")] string? GradeName = null,
    [property: JsonPropertyName("subjects")] List<FlowSubjectDraftDto>? Subjects = null
);

public record FlowSubjectDraftDto(
    [property: JsonPropertyName("subjectName")] string? SubjectName = null,
    [property: JsonPropertyName("semester1Score")] decimal? Semester1Score = null,
    [property: JsonPropertyName("semester2Score")] decimal? Semester2Score = null,
    // [UC-12] Lý do ô điểm cần kiểm tra (null = đã kiểm tra / nhập tay); còn cờ thì không cho commit
    [property: JsonPropertyName("semester1Review")] string? Semester1Review = null,
    [property: JsonPropertyName("semester2Review")] string? Semester2Review = null
);

/// <summary>
/// Bước 3 – danh sách chứng chỉ (certifications), kế hoạch thi tiếng Anh (englishPlanned, englishPlannedDate: yyyy-MM).
/// englishTestType / englishOverallScore / englishTestDate / otherTests là dạng nháp cũ, chỉ đọc để chuyển sang danh sách
/// </summary>
public record FlowCertificationDraftDto(
    [property: JsonPropertyName("certifications")] List<FlowCertificationItemDto>? Certifications = null,
    [property: JsonPropertyName("englishPlanned")] bool EnglishPlanned = false,
    [property: JsonPropertyName("englishPlannedDate")] string? EnglishPlannedDate = null,
    [property: JsonPropertyName("englishTestType")] string? EnglishTestType = null,
    [property: JsonPropertyName("englishOverallScore")] decimal? EnglishOverallScore = null,
    [property: JsonPropertyName("englishTestDate")] string? EnglishTestDate = null,
    [property: JsonPropertyName("otherTests")] List<FlowOtherTestDraftDto>? OtherTests = null
);

/// <summary>
/// Một chứng chỉ. type: id trong metadata certifications hoặc "other"; sections: điểm thành phần theo id section;
/// testDate: yyyy-MM
/// </summary>
public record FlowCertificationItemDto(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("category")] string? Category = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("customName")] string? CustomName = null,
    [property: JsonPropertyName("subject")] string? Subject = null,
    [property: JsonPropertyName("overall")] decimal? Overall = null,
    [property: JsonPropertyName("scaleText")] string? ScaleText = null,
    [property: JsonPropertyName("sections")] Dictionary<string, decimal?>? Sections = null,
    [property: JsonPropertyName("testDate")] string? TestDate = null
);

/// <summary>Nháp cũ – testType: SAT | ACT | AP</summary>
public record FlowOtherTestDraftDto(
    [property: JsonPropertyName("testType")] string? TestType = null,
    [property: JsonPropertyName("score")] decimal? Score = null
);

/// <summary>
/// Bước 4 – annualBudgetTier: id tier_* trong metadata; needFinancialAid: mandatory | preferred | optional;
/// fundingSource: mã ProfileConstants.FundingSources (ai chi trả chính)
/// </summary>
public record FlowFinancialDraftDto(
    [property: JsonPropertyName("annualBudgetTier")] string? AnnualBudgetTier = null,
    [property: JsonPropertyName("needFinancialAid")] string? NeedFinancialAid = null,
    [property: JsonPropertyName("fundingSource")] string? FundingSource = null
);

public record FlowActivitiesDraftDto(
    [property: JsonPropertyName("activities")] List<FlowActivityDraftDto>? Activities = null
);

/// <summary>
/// Bước 5 – category: activity | achievement; role: founder | leader | vice_leader | member;
/// impactLevel: school | district | province | national | international; startDate/endDate: MM/yyyy; year: năm đạt giải
/// </summary>
public record FlowActivityDraftDto(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("category")] string? Category = null,
    [property: JsonPropertyName("role")] string? Role = null,
    [property: JsonPropertyName("impactLevel")] string? ImpactLevel = null,
    [property: JsonPropertyName("startDate")] string? StartDate = null,
    [property: JsonPropertyName("endDate")] string? EndDate = null,
    [property: JsonPropertyName("isOngoing")] bool IsOngoing = false,
    [property: JsonPropertyName("durationMonths")] int? DurationMonths = null,
    [property: JsonPropertyName("year")] int? Year = null
);

/// <summary>Kết quả OCR (giữ dạng kỳ/môn); FE tự ghép vào draft.step2 rồi lưu nháp</summary>
public record FlowTranscriptTermDto(
    [property: JsonPropertyName("termOrder")] int TermOrder = 1,
    [property: JsonPropertyName("termName")] string? TermName = null,
    [property: JsonPropertyName("scores")] List<FlowTranscriptSubjectScoreDto>? Scores = null
);

public record FlowTranscriptSubjectScoreDto(
    [property: JsonPropertyName("subject")] string? Subject = null,
    [property: JsonPropertyName("score")] decimal Score = 0m,
    [property: JsonPropertyName("rawScore")] string? RawScore = null,
    [property: JsonPropertyName("credits")] decimal? Credits = 1m,
    // [UC-12] Advisor đánh dấu ô cần kiểm tra: "source_image" hoặc "outlier"
    [property: JsonPropertyName("needsReview")] bool NeedsReview = false,
    [property: JsonPropertyName("reviewReason")] string? ReviewReason = null
);

public record DraftTranscriptInfoDto(
    [property: JsonPropertyName("originalFileName")] string OriginalFileName,
    [property: JsonPropertyName("storedFileName")] string StoredFileName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileSizeBytes")] long FileSizeBytes,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("uploadedAt")] DateTime UploadedAt
);

/// <summary>Thông tin file nháp trả cho client: không có tên lưu trữ / đường dẫn trên server</summary>
public record DraftTranscriptFileDto(
    [property: JsonPropertyName("originalFileName")] string OriginalFileName,
    [property: JsonPropertyName("fileSizeBytes")] long FileSizeBytes,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("uploadedAt")] DateTime UploadedAt
)
{
    public static DraftTranscriptFileDto From(DraftTranscriptInfoDto info) =>
        new(info.OriginalFileName, info.FileSizeBytes, info.ContentType, info.UploadedAt);

    public static DraftTranscriptFileDto? FromOrNull(DraftTranscriptInfoDto? info) =>
        info is null ? null : From(info);
}

public record UploadDraftTranscriptResultDto(
    [property: JsonPropertyName("draftTranscript")] DraftTranscriptFileDto DraftTranscript,
    [property: JsonPropertyName("extractedTerms")] IReadOnlyList<FlowTranscriptTermDto> ExtractedTerms,
    [property: JsonPropertyName("calculatedGpa4")] decimal? CalculatedGpa4,
    [property: JsonPropertyName("ocrMessage")] string? OcrMessage,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string>? Warnings = null,
    [property: JsonPropertyName("draftFile")] DraftTranscriptFileDto? DraftFile = null
);

public record SaveProfileDraftRequest(
    [property: JsonPropertyName("currentStep")] int CurrentStep,
    [property: JsonPropertyName("completedSteps")] List<int>? CompletedSteps = null,
    [property: JsonPropertyName("draft")] ProfileFlowDraftDto? Draft = null
);

/// <summary>draft null thì commit theo nháp đang lưu trên server</summary>
public record CommitProfileFlowRequest(
    [property: JsonPropertyName("draft")] ProfileFlowDraftDto? Draft = null
);

public record CommitProfileFlowResponse(
    [property: JsonPropertyName("succeeded")] bool Succeeded,
    [property: JsonPropertyName("profileId")] Guid ProfileId,
    [property: JsonPropertyName("targetLevel")] string TargetLevel,
    [property: JsonPropertyName("intendedMajor")] string? IntendedMajor,
    [property: JsonPropertyName("overallGpa")] decimal? OverallGpa,
    [property: JsonPropertyName("extracurricularScore")] decimal? ExtracurricularScore,
    [property: JsonPropertyName("budgetMinUsd")] decimal? BudgetMinUsd,
    [property: JsonPropertyName("budgetMaxUsd")] decimal? BudgetMaxUsd,
    [property: JsonPropertyName("targetSchoolType")] string? TargetSchoolType = "all",
    [property: JsonPropertyName("transcriptFileUrl")] string? TranscriptFileUrl = null,
    [property: JsonPropertyName("message")] string Message = "",
    [property: JsonPropertyName("success")] bool Success = true
);
