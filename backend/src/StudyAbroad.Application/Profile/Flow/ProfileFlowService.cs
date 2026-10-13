using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service xử lý luồng nhập hồ sơ một cửa (Unified Profile Flow 6 bước):
/// - Tự động lưu nháp từng bước vào DraftFlowStateJson
/// - Khôi phục đúng bước đang làm dở khi quay lại
/// - Quản lý tệp bảng điểm nháp (xem trước / chỉ lưu nháp)
/// - OCR đọc và bóc tách bảng điểm học bạ thành các môn học
/// - Xác nhận lưu chính thức (Commit) bóc tách toàn bộ 5 bước vào CSDL chính
/// </summary>
public class ProfileFlowService(
    IProfileFlowRepository repository,
    IProfileFileStorageService fileStorageService,
    ITranscriptOcrClient? ocrClient = null) : IProfileFlowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);

        // 1. Nếu có bản nháp đang làm dở, ưu tiên khôi phục trạng thái nháp
        if (profile?.DraftFlowStateJson is not null && !string.IsNullOrWhiteSpace(profile.DraftFlowStateJson))
        {
            try
            {
                var draftState = JsonSerializer.Deserialize<ProfileFlowStateDto>(profile.DraftFlowStateJson, JsonOptions);
                if (draftState is not null)
                {
                    return draftState with { HasCommittedProfile = profile.OverallGpa.HasValue || profile.AnnualBudgetUsd.HasValue };
                }
            }
            catch
            {
                // Nếu JSON bị lỗi cấu trúc, tiếp tục kiểm tra hồ sơ chính thức
            }
        }

        // 2. Nếu đã có hồ sơ chính thức trong DB nhưng chưa có nháp mới, khởi tạo từ hồ sơ chính thức
        if (profile is not null && (profile.OverallGpa.HasValue || profile.AnnualBudgetUsd.HasValue || !string.IsNullOrWhiteSpace(profile.IntendedMajor)))
        {
            var transcriptScores = await repository.GetTranscriptScoresAsync(profile.Id, ct);
            var activities = await repository.GetProfileActivitiesAsync(profile.Id, ct);

            var terms = transcriptScores
                .GroupBy(s => new { s.TermOrder, s.TermName })
                .OrderBy(g => g.Key.TermOrder)
                .Select(g => new FlowTranscriptTermDto(
                    TermOrder: g.Key.TermOrder,
                    TermName: g.Key.TermName,
                    Scores: g.Select(s => new FlowTranscriptSubjectScoreDto(s.Subject, s.Score, s.RawScore, s.Credits)).ToList()
                ))
                .ToList();

            var flowActivities = activities
                .Select(a => new FlowActivityItemDto(
                    Id: a.Id,
                    Type: string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase) ? "award" : "activity",
                    Title: a.Title,
                    Role: a.Role,
                    Organization: a.Organization,
                    ImpactLevel: a.ImpactLevel ?? 1,
                    DurationMonths: a.DurationMonths,
                    Year: a.StartDate?.Year.ToString(),
                    Description: a.Description
                ))
                .ToList();

            DraftTranscriptInfoDto? draftTranscript = null;
            if (!string.IsNullOrWhiteSpace(profile.TranscriptFileUrl))
            {
                draftTranscript = new DraftTranscriptInfoDto(
                    OriginalFileName: Path.GetFileName(profile.TranscriptFileUrl),
                    StoredFileName: Path.GetFileName(profile.TranscriptFileUrl),
                    RelativePath: profile.TranscriptFileUrl,
                    FileSizeBytes: 0,
                    ContentType: profile.TranscriptFileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "image/jpeg",
                    UploadedAt: profile.UpdatedAt ?? profile.CreatedAt
                );
            }

            var stepData = new ProfileFlowStepDataDto(
                Step1: new Step1GeneralDto(
                    TargetLevel: profile.TargetLevel,
                    CurrentEducationLevel: profile.EducationSystem,
                    CurrentSchool: profile.CurrentSchool,
                    CurrentGrade: profile.CurrentGrade,
                    GraduationYear: profile.GraduationYear,
                    IntendedMajor: profile.IntendedMajor,
                    PreferredStates: profile.PreferredStates ?? [],
                    InstitutionType: "any"
                ),
                Step2: new Step2TranscriptDto(
                    EducationSystem: profile.EducationSystem,
                    GradeScale: profile.GradeScale,
                    CalculatedGpa4: profile.OverallGpa,
                    Terms: terms
                ),
                Step3: new Step3CertificationsDto(
                    EnglishTestType: profile.Ielts.HasValue ? "ielts" : (profile.Toefl.HasValue ? "toefl" : (profile.Duolingo.HasValue ? "duolingo" : "none")),
                    Ielts: profile.Ielts,
                    Toefl: profile.Toefl,
                    Duolingo: profile.Duolingo,
                    EnglishExamDate: null,
                    SelectedStandardizedTests: GetStandardizedTestsList(profile),
                    Sat: profile.Sat,
                    Act: profile.Act,
                    Ap: null,
                    Gre: profile.Gre,
                    Gmat: profile.Gmat,
                    OtherTestsJson: profile.OtherTestsJson
                ),
                Step4: new Step4FinancialDto(
                    BudgetTierId: ResolveBudgetTierId(profile.AnnualBudgetUsd),
                    AnnualBudgetUsd: profile.AnnualBudgetUsd,
                    ScholarshipNeedId: profile.NeedsScholarship ? "preferred" : "optional",
                    FundingSourceId: profile.FundingSource,
                    FinancialNotes: profile.FinancialNotes
                ),
                Step5: new Step5ActivitiesDto(flowActivities)
            );

            return new ProfileFlowStateDto(
                CurrentStep: 1,
                CompletedSteps: [1, 2, 3, 4, 5],
                StepData: stepData,
                DraftTranscript: draftTranscript,
                LastUpdatedAt: profile.UpdatedAt ?? profile.CreatedAt,
                HasCommittedProfile: true
            );
        }

        // 3. Người dùng hoàn toàn mới: khởi tạo trạng thái mặc định bước 1
        return new ProfileFlowStateDto(
            CurrentStep: 1,
            CompletedSteps: [],
            StepData: new ProfileFlowStepDataDto(
                Step1: new Step1GeneralDto(
                    TargetLevel: StudyLevels.Undergraduate,
                    CurrentEducationLevel: "high_school",
                    CurrentSchool: null,
                    CurrentGrade: "Lớp 11",
                    GraduationYear: null,
                    IntendedMajor: null,
                    PreferredStates: [],
                    InstitutionType: "any"
                )
            ),
            DraftTranscript: null,
            LastUpdatedAt: null,
            HasCommittedProfile: false
        );
    }

    public async Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default)
    {
        if (request.CurrentStep < 1 || request.CurrentStep > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CurrentStep), "Bước hiện tại phải nằm trong khoảng từ 1 đến 6.");
        }

        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        // Khôi phục thông tin file nháp cũ nếu request không gửi kèm
        DraftTranscriptInfoDto? draftTranscript = null;
        if (!string.IsNullOrWhiteSpace(profile.DraftFlowStateJson))
        {
            try
            {
                var oldState = JsonSerializer.Deserialize<ProfileFlowStateDto>(profile.DraftFlowStateJson, JsonOptions);
                draftTranscript = oldState?.DraftTranscript;
            }
            catch
            {
                // Bỏ qua lỗi deserialize nháp cũ
            }
        }

        var updatedState = new ProfileFlowStateDto(
            CurrentStep: request.CurrentStep,
            CompletedSteps: request.CompletedSteps ?? [],
            StepData: request.StepData,
            DraftTranscript: draftTranscript,
            LastUpdatedAt: DateTime.UtcNow,
            HasCommittedProfile: profile.OverallGpa.HasValue || profile.AnnualBudgetUsd.HasValue
        );

        profile.DraftFlowStateJson = JsonSerializer.Serialize(updatedState, JsonOptions);
        await repository.UpdateProfileAsync(profile, ct);

        return updatedState;
    }

    public async Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(
        Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        // Đọc toàn bộ nội dung file ra bộ nhớ để vừa lưu nháp vừa gửi sang OCR
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        // 1. Lưu file vào thư mục nháp
        using var saveStream = new MemoryStream(fileBytes);
        var draftInfo = await fileStorageService.SaveDraftTranscriptAsync(userId, saveStream, fileName, contentType, length, ct);

        // 2. Thử OCR bóc tách điểm bảng điểm qua AI Advisor
        TranscriptOcrResponse? ocrResponse = null;
        if (ocrClient != null)
        {
            try
            {
                var base64 = Convert.ToBase64String(fileBytes);
                ocrResponse = await ocrClient.OcrTranscriptAsync(new TranscriptOcrRequest(base64, fileName, contentType), ct);
            }
            catch
            {
                // OCR lỗi thì không chặn luồng upload
            }
        }

        // 3. Cập nhật vào DraftFlowStateJson
        ProfileFlowStateDto currentState;
        if (!string.IsNullOrWhiteSpace(profile.DraftFlowStateJson))
        {
            try
            {
                currentState = JsonSerializer.Deserialize<ProfileFlowStateDto>(profile.DraftFlowStateJson, JsonOptions)
                    ?? CreateDefaultDraftState();
            }
            catch
            {
                currentState = CreateDefaultDraftState();
            }
        }
        else
        {
            currentState = CreateDefaultDraftState();
        }

        var updatedStepData = currentState.StepData;
        if (ocrResponse != null && ocrResponse.Terms.Count > 0)
        {
            var currentStep2 = currentState.StepData.Step2 ?? new Step2TranscriptDto("standard", "10", null, []);
            var updatedStep2 = currentStep2 with
            {
                Terms = ocrResponse.Terms.ToList(),
                CalculatedGpa4 = ocrResponse.CalculatedGpa4 ?? currentStep2.CalculatedGpa4
            };
            updatedStepData = currentState.StepData with { Step2 = updatedStep2 };
        }

        var newState = currentState with
        {
            DraftTranscript = draftInfo,
            StepData = updatedStepData,
            LastUpdatedAt = DateTime.UtcNow
        };

        profile.DraftFlowStateJson = JsonSerializer.Serialize(newState, JsonOptions);
        await repository.UpdateProfileAsync(profile, ct);

        return new UploadDraftTranscriptResultDto(
            DraftTranscript: draftInfo,
            ExtractedTerms: ocrResponse?.Terms ?? [],
            CalculatedGpa4: ocrResponse?.CalculatedGpa4,
            OcrMessage: ocrResponse?.Message
        );
    }


    public async Task<CommitProfileFlowResponse> CommitFlowAsync(
        Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default)
    {
        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        // 1. Lấy dữ liệu cần commit từ request hoặc từ nháp đang lưu
        ProfileFlowStepDataDto? stepData = request?.StepData;
        DraftTranscriptInfoDto? draftTranscript = null;

        if (!string.IsNullOrWhiteSpace(profile.DraftFlowStateJson))
        {
            try
            {
                var existingDraft = JsonSerializer.Deserialize<ProfileFlowStateDto>(profile.DraftFlowStateJson, JsonOptions);
                draftTranscript = existingDraft?.DraftTranscript;
                stepData ??= existingDraft?.StepData;
            }
            catch
            {
                // Bỏ qua lỗi đọc nháp
            }
        }

        if (stepData is null)
        {
            throw new InvalidOperationException("Không tìm thấy dữ liệu hồ sơ để xác nhận lưu chính thức.");
        }

        // 2. Bóc tách Bước 1: Mục tiêu & Học vấn chung
        var step1 = stepData.Step1;
        profile.TargetLevel = !string.IsNullOrWhiteSpace(step1?.TargetLevel)
            ? step1.TargetLevel.Trim()
            : StudyLevels.Undergraduate;

        profile.CurrentSchool = step1?.CurrentSchool?.Trim();
        profile.CurrentGrade = step1?.CurrentGrade?.Trim();
        profile.GraduationYear = step1?.GraduationYear;
        profile.IntendedMajor = step1?.IntendedMajor?.Trim();
        profile.PreferredStates = step1?.PreferredStates ?? [];

        // 3. Bóc tách Bước 2: Bảng điểm & Tính GPA 4.0
        var step2 = stepData.Step2;
        profile.EducationSystem = step2?.EducationSystem?.Trim() ?? step1?.CurrentEducationLevel?.Trim();
        profile.GradeScale = !string.IsNullOrWhiteSpace(step2?.GradeScale) ? step2.GradeScale.Trim() : "10";

        var transcriptScores = new List<TranscriptScore>();
        if (step2?.Terms is not null)
        {
            foreach (var term in step2.Terms)
            {
                if (term.Scores is null) continue;
                foreach (var s in term.Scores)
                {
                    if (string.IsNullOrWhiteSpace(s.Subject)) continue;
                    transcriptScores.Add(new TranscriptScore
                    {
                        TermOrder = term.TermOrder,
                        TermName = term.TermName.Trim(),
                        Subject = s.Subject.Trim(),
                        Score = s.Score,
                        RawScore = s.RawScore ?? s.Score.ToString(),
                        Credits = s.Credits ?? 1m
                    });
                }
            }
        }

        profile.OverallGpa = CalculateGpa4(step2?.CalculatedGpa4, profile.GradeScale, transcriptScores);

        // 4. Bóc tách Bước 3: Chứng chỉ & Bài thi chuẩn hóa
        var step3 = stepData.Step3;
        profile.Ielts = step3?.Ielts;
        profile.Toefl = step3?.Toefl;
        profile.Duolingo = step3?.Duolingo;
        profile.Sat = step3?.Sat;
        profile.Act = step3?.Act;
        profile.Gre = step3?.Gre;
        profile.Gmat = step3?.Gmat;

        if (step3 is not null)
        {
            var otherInfo = new Dictionary<string, object?>
            {
                ["englishTestType"] = step3.EnglishTestType,
                ["englishExamDate"] = step3.EnglishExamDate,
                ["ap"] = step3.Ap,
                ["selectedStandardizedTests"] = step3.SelectedStandardizedTests
            };
            profile.OtherTestsJson = JsonSerializer.Serialize(otherInfo);
        }

        // 5. Bóc tách Bước 4: Tài chính
        var step4 = stepData.Step4;
        profile.AnnualBudgetUsd = ResolveAnnualBudgetUsd(step4?.AnnualBudgetUsd, step4?.BudgetTierId);
        profile.FundingSource = step4?.FundingSourceId?.Trim();
        profile.NeedsScholarship = !string.Equals(step4?.ScholarshipNeedId, "optional", StringComparison.OrdinalIgnoreCase);
        profile.FinancialNotes = step4?.FinancialNotes?.Trim();

        // 6. Bóc tách Bước 5: Hoạt động & Giải thưởng ngoại khóa
        var step5 = stepData.Step5;
        var profileActivities = new List<ProfileActivity>();
        if (step5?.Items is not null)
        {
            foreach (var item in step5.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Title)) continue;
                profileActivities.Add(new ProfileActivity
                {
                    Kind = string.Equals(item.Type, "award", StringComparison.OrdinalIgnoreCase) ? "award" : "extracurricular",
                    Title = item.Title.Trim(),
                    Role = item.Role?.Trim(),
                    Organization = item.Organization?.Trim(),
                    ImpactLevel = item.ImpactLevel,
                    DurationMonths = item.DurationMonths,
                    Description = item.Description?.Trim(),
                    IsOngoing = false
                });
            }
        }

        profile.ExtracurricularScore = CalculateExtracurricularScore(profileActivities);

        // 7. Bóc tách Bước 5 (File bảng điểm): Chuyển file nháp sang chính thức
        if (draftTranscript is not null && !string.IsNullOrWhiteSpace(draftTranscript.RelativePath))
        {
            var officialUrl = await fileStorageService.CommitDraftTranscriptAsync(userId, draftTranscript.RelativePath, ct);
            if (!string.IsNullOrWhiteSpace(officialUrl))
            {
                profile.TranscriptFileUrl = officialUrl;
            }
        }

        // 8. Hoàn tất lưu chính thức: Xóa bản nháp sau khi commit thành công
        profile.DraftFlowStateJson = null;
        profile.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateProfileAsync(profile, ct);
        await repository.ReplaceTranscriptScoresAsync(profile.Id, transcriptScores, ct);
        await repository.ReplaceProfileActivitiesAsync(profile.Id, profileActivities, ct);

        return new CommitProfileFlowResponse(
            Succeeded: true,
            ProfileId: profile.Id,
            TargetLevel: profile.TargetLevel,
            IntendedMajor: profile.IntendedMajor,
            OverallGpa: profile.OverallGpa,
            ExtracurricularScore: profile.ExtracurricularScore,
            AnnualBudgetUsd: profile.AnnualBudgetUsd,
            TranscriptFileUrl: profile.TranscriptFileUrl,
            Message: "Hồ sơ du học đã được lưu chính thức thành công vào hệ thống."
        );
    }

    public async Task ClearDraftAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is not null)
        {
            profile.DraftFlowStateJson = null;
            await repository.UpdateProfileAsync(profile, ct);
        }

        await fileStorageService.DeleteDraftFilesAsync(userId, ct);
    }

    // Helper: Quy đổi GPA 4.0 chuẩn xác
    private static decimal? CalculateGpa4(decimal? explicitGpa, string gradeScale, List<TranscriptScore> scores)
    {
        if (explicitGpa.HasValue && explicitGpa.Value > 0)
        {
            return Math.Round(explicitGpa.Value, 2);
        }

        if (scores.Count == 0) return null;

        var totalScore = 0m;
        var totalCredits = 0m;

        foreach (var s in scores)
        {
            var cred = s.Credits.HasValue && s.Credits.Value > 0 ? s.Credits.Value : 1m;
            decimal normScore;

            if (gradeScale == "10")
            {
                // Thang 10 quy đổi sang 4.0 theo công thức tỷ lệ chuẩn
                normScore = s.Score >= 8.5m ? 4.0m : (s.Score >= 8.0m ? 3.5m : (s.Score >= 7.0m ? 3.0m : (s.Score >= 6.0m ? 2.5m : (s.Score >= 5.0m ? 2.0m : 1.0m))));
            }
            else if (gradeScale == "100")
            {
                normScore = s.Score / 100m * 4m;
            }
            else
            {
                normScore = Math.Clamp(s.Score, 0m, 4m);
            }

            totalScore += normScore * cred;
            totalCredits += cred;
        }

        return totalCredits > 0 ? Math.Round(totalScore / totalCredits, 2) : null;
    }

    // Helper: Tính điểm ngoại khóa theo thang 0–4 dựa trên cấp độ và số lượng
    private static decimal? CalculateExtracurricularScore(List<ProfileActivity> activities)
    {
        if (activities.Count == 0) return 0m;

        var maxLevel = activities.Max(a => a.ImpactLevel ?? 1);
        var baseScore = maxLevel switch
        {
            >= 4 => 3.5m, // Quốc tế
            3 => 3.0m,    // Quốc gia
            2 => 2.2m,    // Tỉnh / Thành
            _ => 1.5m     // Trường
        };

        var bonus = Math.Min(activities.Count, 4) * 0.15m;
        return Math.Clamp(Math.Round(baseScore + bonus, 2), 0m, 4.0m);
    }

    private static decimal? ResolveAnnualBudgetUsd(decimal? explicitUsd, string? budgetTierId)
    {
        if (explicitUsd.HasValue && explicitUsd.Value > 0) return explicitUsd.Value;

        return budgetTierId switch
        {
            "tier_under_20k" => 15000m,
            "tier_20k_35k" => 28000m,
            "tier_35k_50k" => 42000m,
            "tier_50k_70k" => 60000m,
            "tier_above_70k" => 85000m,
            _ => null
        };
    }

    private static string? ResolveBudgetTierId(decimal? annualBudgetUsd)
    {
        if (!annualBudgetUsd.HasValue) return null;
        var usd = annualBudgetUsd.Value;
        if (usd < 20000) return "tier_under_20k";
        if (usd <= 35000) return "tier_20k_35k";
        if (usd <= 50000) return "tier_35k_50k";
        if (usd <= 70000) return "tier_50k_70k";
        return "tier_above_70k";
    }

    private static List<string> GetStandardizedTestsList(StudentProfile profile)
    {
        var tests = new List<string>();
        if (profile.Sat.HasValue) tests.Add("sat");
        if (profile.Act.HasValue) tests.Add("act");
        if (profile.Gre.HasValue) tests.Add("gre");
        if (profile.Gmat.HasValue) tests.Add("gmat");
        return tests;
    }

    private static ProfileFlowStateDto CreateDefaultDraftState() =>
        new(
            CurrentStep: 1,
            CompletedSteps: [],
            StepData: new ProfileFlowStepDataDto(),
            DraftTranscript: null,
            LastUpdatedAt: DateTime.UtcNow,
            HasCommittedProfile: false
        );
}
