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
                HasCommittedProfile: true,
                Draft: ConvertStepDataToRawDraft(stepData),
                LastCompletedStep: 5
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

        var rawDraft = request.Draft ?? request.FlowState;
        var stepData = request.StepData;
        if (stepData is null && rawDraft is not null)
        {
            stepData = ConvertRawDraftToStepData(rawDraft);
        }
        stepData ??= new ProfileFlowStepDataDto();

        var updatedState = new ProfileFlowStateDto(
            CurrentStep: request.CurrentStep,
            CompletedSteps: request.CompletedSteps ?? [],
            StepData: stepData,
            DraftTranscript: draftTranscript,
            LastUpdatedAt: DateTime.UtcNow,
            HasCommittedProfile: profile.OverallGpa.HasValue || profile.AnnualBudgetUsd.HasValue,
            Draft: rawDraft,
            LastCompletedStep: request.CompletedSteps?.Count > 0 ? request.CompletedSteps.Max() : (request.CurrentStep > 1 ? request.CurrentStep - 1 : 0)
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
                ocrResponse = await ocrClient.OcrTranscriptAsync(
                    new TranscriptOcrRequest(
                        Files: [new TranscriptFilePayload(base64, fileName, contentType)],
                        ImageBase64: base64,
                        FileName: fileName,
                        ContentType: contentType
                    ), ct);
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
            OcrMessage: ocrResponse?.Message,
            Warnings: ocrResponse?.Warnings
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

        if (stepData is null || (stepData.Step1 is null && stepData.Step2 is null))
        {
            var rawDraft = request?.FinalDraft;
            if (rawDraft is null && !string.IsNullOrWhiteSpace(profile.DraftFlowStateJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(profile.DraftFlowStateJson);
                    if (doc.RootElement.TryGetProperty("draft", out var dProp))
                    {
                        rawDraft = dProp.Clone();
                    }
                }
                catch
                {
                    // Bỏ qua lỗi đọc nháp
                }
            }

            if (rawDraft is not null)
            {
                stepData = ConvertRawDraftToStepData(rawDraft);
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
                // Quy đổi trực tiếp theo công thức chuẩn: (Điểm thang 10 / 10) * 4
                normScore = (s.Score / 10m) * 4m;
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

    private static ProfileFlowStepDataDto ConvertRawDraftToStepData(object rawDraft)
    {
        try
        {
            JsonElement root;
            if (rawDraft is JsonElement je)
            {
                root = je;
            }
            else
            {
                var json = JsonSerializer.Serialize(rawDraft, JsonOptions);
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
            }

            // 1. Parse Step 1
            Step1GeneralDto? step1 = null;
            if (root.TryGetProperty("step1", out var s1) && s1.ValueKind == JsonValueKind.Object)
            {
                string? targetLevel = s1.TryGetProperty("targetDegreeLevel", out var tl) && tl.ValueKind == JsonValueKind.String ? tl.GetString() : (s1.TryGetProperty("targetLevel", out var tl2) && tl2.ValueKind == JsonValueKind.String ? tl2.GetString() : "undergraduate");
                string? curLevel = s1.TryGetProperty("currentEducationLevel", out var cel) && cel.ValueKind == JsonValueKind.String ? cel.GetString() : null;
                string? curSchool = s1.TryGetProperty("currentSchool", out var cs) && cs.ValueKind == JsonValueKind.String ? cs.GetString() : null;
                string? curGrade = s1.TryGetProperty("currentGrade", out var cg) && cg.ValueKind == JsonValueKind.String ? cg.GetString() : null;
                int? gradYear = s1.TryGetProperty("expectedGraduationYear", out var gy) && gy.TryGetInt32(out var gyVal) ? gyVal : (s1.TryGetProperty("graduationYear", out var gy2) && gy2.TryGetInt32(out var gy2Val) ? gy2Val : null);
                string? major = s1.TryGetProperty("targetMajor", out var tm) && tm.ValueKind == JsonValueKind.String ? tm.GetString() : (s1.TryGetProperty("intendedMajor", out var im) && im.ValueKind == JsonValueKind.String ? im.GetString() : null);
                var prefStates = s1.TryGetProperty("targetStates", out var ts) && ts.ValueKind == JsonValueKind.Array
                    ? ts.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : (s1.TryGetProperty("preferredStates", out var ps) && ps.ValueKind == JsonValueKind.Array ? ps.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList() : new List<string>());
                string? instType = s1.TryGetProperty("targetSchoolType", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;

                step1 = new Step1GeneralDto(targetLevel, curLevel, curSchool, curGrade, gradYear, major, prefStates, instType);
            }

            // 2. Parse Step 2
            Step2TranscriptDto? step2 = null;
            if (root.TryGetProperty("step2", out var s2) && s2.ValueKind == JsonValueKind.Object)
            {
                decimal? gpa4 = null;
                if (s2.TryGetProperty("calculatedGpa4Scale", out var g4) && g4.TryGetDecimal(out var g4Val)) gpa4 = g4Val;
                else if (s2.TryGetProperty("calculatedGpa4", out var g4b) && g4b.TryGetDecimal(out var g4bVal)) gpa4 = g4bVal;

                var terms = new List<FlowTranscriptTermDto>();
                if (s2.TryGetProperty("academicYears", out var ay) && ay.ValueKind == JsonValueKind.Array)
                {
                    int order = 1;
                    foreach (var yearElem in ay.EnumerateArray())
                    {
                        var gradeName = yearElem.TryGetProperty("gradeName", out var gn) ? gn.GetString() ?? "Lớp" : "Lớp";
                        if (yearElem.TryGetProperty("subjects", out var subs) && subs.ValueKind == JsonValueKind.Array)
                        {
                            var s1Scores = new List<FlowTranscriptSubjectScoreDto>();
                            var s2Scores = new List<FlowTranscriptSubjectScoreDto>();

                            foreach (var sub in subs.EnumerateArray())
                            {
                                var subName = sub.TryGetProperty("subjectName", out var sn) ? sn.GetString() ?? "" : "";
                                if (string.IsNullOrWhiteSpace(subName)) continue;

                                if (sub.TryGetProperty("semester1Score", out var sc1) && sc1.ValueKind == JsonValueKind.Number && sc1.TryGetDecimal(out var sc1Val))
                                {
                                    s1Scores.Add(new FlowTranscriptSubjectScoreDto(subName, sc1Val, sc1Val.ToString(), 1m));
                                }
                                if (sub.TryGetProperty("semester2Score", out var sc2) && sc2.ValueKind == JsonValueKind.Number && sc2.TryGetDecimal(out var sc2Val))
                                {
                                    s2Scores.Add(new FlowTranscriptSubjectScoreDto(subName, sc2Val, sc2Val.ToString(), 1m));
                                }
                            }

                            if (s1Scores.Count > 0)
                            {
                                terms.Add(new FlowTranscriptTermDto(order++, $"{gradeName} - HK1", s1Scores));
                            }
                            if (s2Scores.Count > 0)
                            {
                                terms.Add(new FlowTranscriptTermDto(order++, $"{gradeName} - HK2", s2Scores));
                            }
                        }
                    }
                }

                step2 = new Step2TranscriptDto(
                    EducationSystem: step1?.CurrentEducationLevel ?? "Việt Nam",
                    GradeScale: "10",
                    CalculatedGpa4: gpa4,
                    Terms: terms
                );
            }

            // 3. Parse Step 3
            Step3CertificationsDto? step3 = null;
            if (root.TryGetProperty("step3", out var s3) && s3.ValueKind == JsonValueKind.Object)
            {
                var engType = s3.TryGetProperty("englishTestType", out var et) && et.ValueKind == JsonValueKind.String ? et.GetString() : null;
                decimal? engScore = s3.TryGetProperty("englishOverallScore", out var eos) && eos.TryGetDecimal(out var eosVal) ? eosVal : null;
                var engDate = s3.TryGetProperty("englishTestDate", out var ed) && ed.ValueKind == JsonValueKind.String ? ed.GetString() : null;

                decimal? sat = null;
                decimal? act = null;
                decimal? ap = null;
                decimal? gre = null;
                decimal? gmat = null;
                var selectedTests = new List<string>();

                if (s3.TryGetProperty("otherTests", out var ot) && ot.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in ot.EnumerateArray())
                    {
                        var tType = t.TryGetProperty("testType", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() : null;
                        var tScore = t.TryGetProperty("score", out var ts) && ts.TryGetDecimal(out var tsVal) ? tsVal : (decimal?)null;
                        if (!string.IsNullOrEmpty(tType))
                        {
                            selectedTests.Add(tType.ToLowerInvariant());
                            if (tType.Equals("SAT", StringComparison.OrdinalIgnoreCase)) sat = tScore;
                            else if (tType.Equals("ACT", StringComparison.OrdinalIgnoreCase)) act = tScore;
                            else if (tType.Equals("AP", StringComparison.OrdinalIgnoreCase)) ap = tScore;
                            else if (tType.Equals("GRE", StringComparison.OrdinalIgnoreCase)) gre = tScore;
                            else if (tType.Equals("GMAT", StringComparison.OrdinalIgnoreCase)) gmat = tScore;
                        }
                    }
                }

                step3 = new Step3CertificationsDto(
                    EnglishTestType: engType,
                    Ielts: engType == "IELTS" ? engScore : null,
                    Toefl: engType == "TOEFL" ? engScore : null,
                    Duolingo: engType == "Duolingo" ? engScore : null,
                    EnglishExamDate: engDate,
                    SelectedStandardizedTests: selectedTests,
                    Sat: sat,
                    Act: act,
                    Ap: ap,
                    Gre: gre,
                    Gmat: gmat
                );
            }

            // 4. Parse Step 4
            Step4FinancialDto? step4 = null;
            if (root.TryGetProperty("step4", out var s4) && s4.ValueKind == JsonValueKind.Object)
            {
                var tier = s4.TryGetProperty("annualBudgetTier", out var bt) && bt.ValueKind == JsonValueKind.String ? bt.GetString() : null;
                if (!string.IsNullOrEmpty(tier) && !tier.StartsWith("tier_")) tier = $"tier_{tier}";
                decimal? usd = s4.TryGetProperty("annualBudgetUsd", out var bu) && bu.TryGetDecimal(out var buVal) ? buVal : null;
                var need = s4.TryGetProperty("needFinancialAid", out var nf) && nf.ValueKind == JsonValueKind.String ? nf.GetString() : null;
                var notes = s4.TryGetProperty("financialNotes", out var fn) && fn.ValueKind == JsonValueKind.String ? fn.GetString() : null;

                step4 = new Step4FinancialDto(
                    BudgetTierId: tier,
                    AnnualBudgetUsd: usd,
                    ScholarshipNeedId: need,
                    FundingSourceId: null,
                    FinancialNotes: notes
                );
            }

            // 5. Parse Step 5
            Step5ActivitiesDto? step5 = null;
            if (root.TryGetProperty("step5", out var s5) && s5.ValueKind == JsonValueKind.Object)
            {
                var items = new List<FlowActivityItemDto>();
                if (s5.TryGetProperty("activities", out var acts) && acts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in acts.EnumerateArray())
                    {
                        var aName = a.TryGetProperty("name", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(aName)) continue;

                        var aCat = a.TryGetProperty("category", out var ac) && ac.ValueKind == JsonValueKind.String ? ac.GetString() : "activity";
                        var aRole = a.TryGetProperty("role", out var ar) && ar.ValueKind == JsonValueKind.String ? ar.GetString() : null;
                        var aImpStr = a.TryGetProperty("impactLevel", out var ai) && ai.ValueKind == JsonValueKind.String ? ai.GetString() : "school";
                        int impLevel = aImpStr switch
                        {
                            "international" => 4,
                            "national" => 3,
                            "province" => 2,
                            _ => 1
                        };
                        int? durMonths = a.TryGetProperty("durationYears", out var dy) && dy.TryGetInt32(out var dyVal) ? dyVal * 12 : 12;
                        var aYear = a.TryGetProperty("year", out var ayElem) ? ayElem.ToString() : null;

                        items.Add(new FlowActivityItemDto(
                            Id: Guid.NewGuid(),
                            Type: aCat == "achievement" ? "award" : "extracurricular",
                            Title: aName,
                            Role: aRole,
                            Organization: null,
                            ImpactLevel: impLevel,
                            DurationMonths: durMonths,
                            Year: aYear,
                            Description: aRole != null ? $"Vai trò: {aRole}" : null
                        ));
                    }
                }
                step5 = new Step5ActivitiesDto(items);
            }

            return new ProfileFlowStepDataDto(step1, step2, step3, step4, step5);
        }
        catch
        {
            return new ProfileFlowStepDataDto();
        }
    }

    private static object ConvertStepDataToRawDraft(ProfileFlowStepDataDto stepData)
    {
        var s1 = stepData.Step1;
        var s2 = stepData.Step2;
        var s3 = stepData.Step3;
        var s4 = stepData.Step4;
        var s5 = stepData.Step5;

        var otherTests = new List<object>();
        if (s3?.Sat.HasValue == true) otherTests.Add(new { testType = "SAT", score = s3.Sat.Value });
        if (s3?.Act.HasValue == true) otherTests.Add(new { testType = "ACT", score = s3.Act.Value });
        if (s3?.Ap.HasValue == true) otherTests.Add(new { testType = "AP", score = s3.Ap.Value });
        if (s3?.Gre.HasValue == true) otherTests.Add(new { testType = "GRE", score = s3.Gre.Value });
        if (s3?.Gmat.HasValue == true) otherTests.Add(new { testType = "GMAT", score = s3.Gmat.Value });

        string? engType = s3?.Ielts.HasValue == true ? "IELTS" : (s3?.Toefl.HasValue == true ? "TOEFL" : (s3?.Duolingo.HasValue == true ? "Duolingo" : (s3?.EnglishTestType ?? "none")));
        decimal? engScore = s3?.Ielts ?? s3?.Toefl ?? s3?.Duolingo;

        var activities = new List<object>();
        if (s5?.Items != null)
        {
            foreach (var item in s5.Items)
            {
                activities.Add(new
                {
                    id = item.Id?.ToString() ?? Guid.NewGuid().ToString(),
                    name = item.Title ?? "",
                    category = item.Type == "award" ? "achievement" : "activity",
                    role = item.Role,
                    impactLevel = item.ImpactLevel switch { 4 => "international", 3 => "national", 2 => "province", _ => "school" },
                    durationYears = item.DurationMonths.HasValue ? Math.Max(1, item.DurationMonths.Value / 12) : 1,
                    year = int.TryParse(item.Year, out var yr) ? yr : (int?)null
                });
            }
        }

        return new
        {
            step1 = new
            {
                targetDegreeLevel = s1?.TargetLevel ?? "undergraduate",
                currentEducationLevel = s1?.CurrentEducationLevel,
                currentSchool = s1?.CurrentSchool,
                currentGrade = s1?.CurrentGrade,
                expectedGraduationYear = s1?.GraduationYear,
                targetMajor = s1?.IntendedMajor,
                targetStates = s1?.PreferredStates ?? new List<string>(),
                targetSchoolType = s1?.InstitutionType ?? "all"
            },
            step2 = new
            {
                calculatedGpa4Scale = s2?.CalculatedGpa4,
                manualGpa10Scale = s2?.CalculatedGpa4.HasValue == true ? Math.Round((s2.CalculatedGpa4.Value / 4.0m) * 10m, 2) : (decimal?)null,
                academicYears = new object[] { }
            },
            step3 = new
            {
                englishTestType = engType,
                englishOverallScore = engScore,
                englishTestDate = s3?.EnglishExamDate,
                otherTests = otherTests
            },
            step4 = new
            {
                annualBudgetTier = s4?.BudgetTierId?.Replace("tier_", ""),
                annualBudgetUsd = s4?.AnnualBudgetUsd,
                needFinancialAid = s4?.ScholarshipNeedId ?? "preferred",
                financialNotes = s4?.FinancialNotes
            },
            step5 = new
            {
                activities = activities
            }
        };
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
