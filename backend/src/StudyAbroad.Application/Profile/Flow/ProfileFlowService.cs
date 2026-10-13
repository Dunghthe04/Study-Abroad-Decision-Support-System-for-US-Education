using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service xử lý luồng nhập hồ sơ một cửa (Unified Profile Flow 6 bước):
/// - Nháp lưu đúng 1 định dạng (draft của FE) vào DraftFlowStateJson, khôi phục đúng bước đang làm dở
/// - Quản lý tệp bảng điểm nháp + OCR (kết quả OCR trả về cho FE tự ghép vào draft)
/// - Commit: validate rồi ánh xạ draft vào student_profiles / transcript_scores / profile_activities trong 1 transaction
/// - Mở lại hồ sơ đã gửi: dựng draft từ DB bằng BuildDraftFromProfile
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
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex TermNameRegex = new(@"^(?<grade>.*?)\s*-?\s*HK\s*(?<sem>[12])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Nhãn cấp học cũ (luồng trước lưu nhãn tiếng Việt) → mã</summary>
    private static readonly Dictionary<string, string> EducationLevelLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trung học cơ sở"] = "middle_school",
        ["Trung học phổ thông"] = "high_school",
        ["Cao đẳng / Đại học"] = "college_university",
        ["Thạc sĩ"] = "master",
    };

    private static readonly HashSet<string> EducationLevelCodes =
        ["middle_school", "high_school", "college_university", "master"];

    private static readonly HashSet<string> SchoolTypes = ["all", "public", "private"];

    /// <summary>Định dạng lưu trong draft_flow_state_json</summary>
    private sealed record StoredFlowState(
        int CurrentStep,
        List<int>? CompletedSteps,
        ProfileFlowDraftDto? Draft,
        DraftTranscriptInfoDto? DraftTranscript,
        DateTime? LastUpdatedAt);

    private sealed record BudgetTier(string Id, decimal? MinUsd, decimal? MaxUsd);

    public async Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null)
            return EmptyState(hasCommittedProfile: false);

        var committed = HasCommittedProfile(profile);

        // 1. Có nháp đang làm dở → trả nguyên nháp
        var stored = ReadStoredState(profile.DraftFlowStateJson);
        if (stored?.Draft is not null)
            return ToStateDto(stored, committed);

        // 2. Đã gửi hồ sơ (hoặc có dữ liệu cũ) → dựng draft từ DB
        if (committed)
        {
            var scores = await repository.GetTranscriptScoresAsync(profile.Id, ct);
            var activities = await repository.GetProfileActivitiesAsync(profile.Id, ct);
            return new ProfileFlowStateDto(
                CurrentStep: 6,
                CompletedSteps: [1, 2, 3, 4, 5],
                Draft: BuildDraftFromProfile(profile, scores, activities),
                DraftTranscript: stored?.DraftTranscript,
                LastUpdatedAt: profile.UpdatedAt ?? profile.CreatedAt,
                HasCommittedProfile: true,
                LastCompletedStep: 5);
        }

        // 3. Người dùng mới: draft rỗng, không chọn sẵn ngân sách / nhu cầu học bổng
        return EmptyState(hasCommittedProfile: false) with { DraftTranscript = stored?.DraftTranscript };
    }

    public async Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default)
    {
        if (request.CurrentStep < 1 || request.CurrentStep > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CurrentStep), "Bước hiện tại phải nằm trong khoảng từ 1 đến 6.");
        }

        var profile = await repository.EnsureProfileExistsAsync(userId, CancellationToken.None);
        var previous = ReadStoredState(profile.DraftFlowStateJson);

        var stored = new StoredFlowState(
            CurrentStep: request.CurrentStep,
            CompletedSteps: NormalizeCompletedSteps(request.CompletedSteps),
            Draft: NormalizeDraft(request.Draft ?? previous?.Draft ?? new ProfileFlowDraftDto()),
            DraftTranscript: previous?.DraftTranscript,
            LastUpdatedAt: DateTime.UtcNow);

        profile.DraftFlowStateJson = JsonSerializer.Serialize(stored, JsonOptions);
        await repository.UpdateProfileAsync(profile, CancellationToken.None);

        return ToStateDto(stored, HasCommittedProfile(profile));
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

        // 3. Chỉ ghi thông tin file vào nháp; điểm OCR do FE ghép vào draft.step2 rồi tự lưu nháp
        var stored = ReadStoredState(profile.DraftFlowStateJson)
            ?? new StoredFlowState(2, [], null, null, null);
        stored = stored with { DraftTranscript = draftInfo, LastUpdatedAt = DateTime.UtcNow };

        profile.DraftFlowStateJson = JsonSerializer.Serialize(stored, JsonOptions);
        await repository.UpdateProfileAsync(profile, ct);

        return new UploadDraftTranscriptResultDto(
            DraftTranscript: draftInfo,
            ExtractedTerms: ocrResponse?.Terms ?? [],
            CalculatedGpa4: ocrResponse?.CalculatedGpa4,
            OcrMessage: ocrResponse?.Message,
            Warnings: ocrResponse?.Warnings,
            DraftFile: draftInfo
        );
    }

    public async Task<CommitProfileFlowResponse> CommitFlowAsync(
        Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default)
    {
        var profile = await repository.EnsureProfileExistsAsync(userId, ct);
        var stored = ReadStoredState(profile.DraftFlowStateJson);

        var draft = request?.Draft ?? stored?.Draft
            ?? throw new InvalidOperationException("Không tìm thấy dữ liệu hồ sơ để xác nhận lưu chính thức.");
        draft = NormalizeDraft(draft);

        var budgetTiers = await LoadBudgetTiersAsync(ct);
        ValidateForCommit(draft, budgetTiers);

        var (scores, activities) = ApplyDraftToProfile(profile, draft, budgetTiers, DateTime.UtcNow);

        // Chuyển file bảng điểm nháp (theo trạng thái trên server) sang thư mục chính thức
        var draftTranscript = stored?.DraftTranscript;
        if (draftTranscript is not null && !string.IsNullOrWhiteSpace(draftTranscript.RelativePath))
        {
            var officialUrl = await fileStorageService.CommitDraftTranscriptAsync(userId, draftTranscript.RelativePath, ct);
            if (!string.IsNullOrWhiteSpace(officialUrl))
            {
                profile.TranscriptFileUrl = officialUrl;
            }
        }

        profile.FlowSubmittedAt = DateTimeOffset.UtcNow;
        profile.DraftFlowStateJson = null;

        await repository.CommitProfileFlowAsync(profile, scores, activities, ct);

        return new CommitProfileFlowResponse(
            Succeeded: true,
            ProfileId: profile.Id,
            TargetLevel: profile.TargetLevel,
            IntendedMajor: profile.IntendedMajor,
            OverallGpa: profile.OverallGpa,
            ExtracurricularScore: profile.ExtracurricularScore,
            BudgetMinUsd: profile.BudgetMinUsd,
            BudgetMaxUsd: profile.BudgetMaxUsd,
            TargetSchoolType: profile.TargetSchoolType,
            TranscriptFileUrl: profile.TranscriptFileUrl,
            Message: "Hồ sơ du học đã được lưu chính thức thành công vào hệ thống.",
            Success: true
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

    // ───────────────────────────── Trạng thái nháp ─────────────────────────────

    /// <summary>Đã gửi qua luồng 6 bước, hoặc có dữ liệu nhập từ màn hình cũ</summary>
    private static bool HasCommittedProfile(StudentProfile profile) =>
        profile.FlowSubmittedAt.HasValue
        || profile.OverallGpa.HasValue
        || profile.BudgetTier is not null
        || !string.IsNullOrWhiteSpace(profile.IntendedMajor);

    private static ProfileFlowStateDto EmptyState(bool hasCommittedProfile) => new(
        CurrentStep: 1,
        CompletedSteps: [],
        Draft: EmptyDraft(),
        DraftTranscript: null,
        LastUpdatedAt: null,
        HasCommittedProfile: hasCommittedProfile,
        LastCompletedStep: 0);

    private static ProfileFlowDraftDto EmptyDraft() => new(
        Step1: new FlowGoalDraftDto(TargetStates: [], TargetSchoolType: "all"),
        Step2: new FlowTranscriptDraftDto(AcademicYears: []),
        Step3: new FlowCertificationDraftDto(EnglishTestType: "none", OtherTests: []),
        Step4: new FlowFinancialDraftDto(),
        Step5: new FlowActivitiesDraftDto([]));

    private static ProfileFlowStateDto ToStateDto(StoredFlowState stored, bool hasCommittedProfile)
    {
        var completed = stored.CompletedSteps ?? [];
        return new ProfileFlowStateDto(
            CurrentStep: stored.CurrentStep is >= 1 and <= 6 ? stored.CurrentStep : 1,
            CompletedSteps: completed,
            Draft: stored.Draft ?? EmptyDraft(),
            DraftTranscript: stored.DraftTranscript,
            LastUpdatedAt: stored.LastUpdatedAt,
            HasCommittedProfile: hasCommittedProfile,
            LastCompletedStep: completed.Count > 0 ? completed.Max() : 0);
    }

    /// <summary>Đọc nháp; JSON hỏng thì coi như không có nháp</summary>
    private static StoredFlowState? ReadStoredState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<StoredFlowState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<int> NormalizeCompletedSteps(IEnumerable<int>? steps) =>
        (steps ?? []).Where(s => s is >= 1 and <= 6).Distinct().Order().ToList();

    /// <summary>Chuẩn hóa giá trị cũ còn sót trong nháp (nhãn cấp học, mã tier thiếu tiền tố, not_needed...)</summary>
    private static ProfileFlowDraftDto NormalizeDraft(ProfileFlowDraftDto draft)
    {
        var step1 = draft.Step1 is null ? null : draft.Step1 with
        {
            CurrentEducationLevel = NormalizeEducationLevel(draft.Step1.CurrentEducationLevel),
        };

        var step4 = draft.Step4 is null ? null : draft.Step4 with
        {
            AnnualBudgetTier = string.IsNullOrWhiteSpace(draft.Step4.AnnualBudgetTier)
                ? null
                : draft.Step4.AnnualBudgetTier.StartsWith("tier_", StringComparison.Ordinal)
                    ? draft.Step4.AnnualBudgetTier
                    : "tier_" + draft.Step4.AnnualBudgetTier,
            NeedFinancialAid = draft.Step4.NeedFinancialAid == "not_needed" ? ProfileConstants.ScholarshipNeeds.Optional
                : string.IsNullOrWhiteSpace(draft.Step4.NeedFinancialAid) ? null : draft.Step4.NeedFinancialAid,
        };

        return draft with { Step1 = step1, Step4 = step4 };
    }

    private static string? NormalizeEducationLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return EducationLevelLabels.TryGetValue(trimmed, out var code) ? code : trimmed;
    }

    // ───────────────────────────── Commit: validate + ánh xạ ─────────────────────────────

    private async Task<Dictionary<string, BudgetTier>> LoadBudgetTiersAsync(CancellationToken ct)
    {
        var tiers = new Dictionary<string, BudgetTier>(StringComparer.Ordinal);
        var json = await repository.GetAppSettingValueAsync(ProfileConstants.ProfileMetadataAppSettingKey, ct);
        if (string.IsNullOrWhiteSpace(json)) return tiers;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("budgetTiers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return tiers;

        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp) || idProp.GetString() is not { Length: > 0 } id) continue;
            tiers[id] = new BudgetTier(id, ReadDecimal(item, "minUsd"), ReadDecimal(item, "maxUsd"));
        }
        return tiers;

        static decimal? ReadDecimal(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
    }

    private static void ValidateForCommit(ProfileFlowDraftDto draft, IReadOnlyDictionary<string, BudgetTier> budgetTiers)
    {
        var errors = new List<string>();

        // Bước 1
        var step1 = draft.Step1;
        if (!StudyLevels.IsValid(step1?.TargetDegreeLevel))
            errors.Add("Bước 1: chưa chọn bậc học muốn theo học.");
        if (step1?.CurrentEducationLevel is null || !EducationLevelCodes.Contains(step1.CurrentEducationLevel))
            errors.Add("Bước 1: chưa chọn cấp học hiện tại.");

        // Bước 2
        foreach (var year in draft.Step2?.AcademicYears ?? [])
        foreach (var subject in year.Subjects ?? [])
        {
            if (subject.Semester1Score is < 0 or > 10 || subject.Semester2Score is < 0 or > 10)
            {
                errors.Add($"Bước 2: điểm môn {subject.SubjectName} ({year.GradeName}) phải từ 0 đến 10.");
            }
        }

        // Bước 3
        var englishType = draft.Step3?.EnglishTestType?.Trim().ToLowerInvariant();
        if (englishType is not null && !ProfileConstants.EnglishTestTypes.All.Contains(englishType))
            errors.Add("Bước 3: loại chứng chỉ tiếng Anh không hợp lệ.");
        if (englishType is not null && englishType != ProfileConstants.EnglishTestTypes.None
            && !string.IsNullOrWhiteSpace(draft.Step3?.EnglishTestDate) && ParseDateOnly(draft.Step3.EnglishTestDate) is null)
            errors.Add("Bước 3: ngày thi tiếng Anh không hợp lệ.");

        // Bước 4
        var tierId = draft.Step4?.AnnualBudgetTier;
        if (string.IsNullOrWhiteSpace(tierId))
            errors.Add("Bước 4: chưa chọn mức ngân sách.");
        else if (!budgetTiers.ContainsKey(tierId))
            errors.Add($"Bước 4: mức ngân sách '{tierId}' không tồn tại.");
        if (draft.Step4?.NeedFinancialAid is null || !ProfileConstants.ScholarshipNeeds.All.Contains(draft.Step4.NeedFinancialAid))
            errors.Add("Bước 4: chưa chọn nhu cầu học bổng.");

        // Bước 5
        var currentYear = DateTime.UtcNow.Year;
        foreach (var item in draft.Step5?.Activities ?? [])
        {
            var name = item.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Bước 5: có mục chưa nhập tên.");
                continue;
            }
            if (item.ImpactLevel is null || !ProfileConstants.ImpactLevelCodes.ToLevel.ContainsKey(item.ImpactLevel))
                errors.Add($"Bước 5: '{name}' chưa chọn phạm vi.");

            if (IsAward(item))
            {
                if (item.Year is null || item.Year < 1900 || item.Year > currentYear)
                    errors.Add($"Bước 5: giải thưởng '{name}' chưa có năm đạt giải hợp lệ.");
                continue;
            }

            if (item.Role is null || !ProfileConstants.ActivityRoles.All.Contains(item.Role))
                errors.Add($"Bước 5: hoạt động '{name}' chưa chọn vai trò.");
            var start = ParseDateOnly(item.StartDate);
            if (start is null)
                errors.Add($"Bước 5: hoạt động '{name}' chưa có tháng bắt đầu hợp lệ (MM/yyyy).");
            var end = item.IsOngoing ? null : ParseDateOnly(item.EndDate);
            if (!item.IsOngoing && end is null)
                errors.Add($"Bước 5: hoạt động '{name}' chưa có tháng kết thúc hợp lệ (MM/yyyy).");
            if (start is not null && end is not null && end < start)
                errors.Add($"Bước 5: hoạt động '{name}' có tháng kết thúc trước tháng bắt đầu.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    private static bool IsAward(FlowActivityDraftDto item) =>
        string.Equals(item.Category, "achievement", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Category, "award", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ánh xạ draft (đã validate) vào entity. Không ghi các cột luồng 6 bước không thu thập (funding_source, financial_notes, max_expected_tuition_usd, gre, gmat).</summary>
    private static (List<TranscriptScore> Scores, List<ProfileActivity> Activities) ApplyDraftToProfile(
        StudentProfile profile, ProfileFlowDraftDto draft, IReadOnlyDictionary<string, BudgetTier> budgetTiers, DateTime now)
    {
        // Bước 1
        var step1 = draft.Step1!;
        profile.TargetLevel = step1.TargetDegreeLevel!.Trim();
        profile.CurrentEducationLevel = step1.CurrentEducationLevel;
        profile.CurrentGrade = NullIfBlank(step1.CurrentGrade);
        profile.CurrentSchool = NullIfBlank(step1.CurrentSchool);
        profile.GraduationYear = step1.ExpectedGraduationYear;
        profile.IntendedMajor = NullIfBlank(step1.TargetMajor);
        profile.PreferredStates = (step1.TargetStates ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var schoolType = step1.TargetSchoolType?.Trim().ToLowerInvariant();
        profile.TargetSchoolType = schoolType is not null && SchoolTypes.Contains(schoolType) ? schoolType : "all";

        // Bước 2: mỗi (khối, học kỳ) là 1 kỳ; TermOrder tăng dần theo thứ tự khối trên UI
        profile.GradeScale = GradeScales.Scale10;
        var scores = new List<TranscriptScore>();
        var termOrder = 0;
        foreach (var year in draft.Step2?.AcademicYears ?? [])
        {
            var gradeName = NullIfBlank(year.GradeName) ?? "Không rõ";
            for (var semester = 1; semester <= 2; semester++)
            {
                termOrder++;
                foreach (var subject in year.Subjects ?? [])
                {
                    var score = semester == 1 ? subject.Semester1Score : subject.Semester2Score;
                    if (score is null || string.IsNullOrWhiteSpace(subject.SubjectName)) continue;
                    scores.Add(new TranscriptScore
                    {
                        TermName = $"{gradeName} HK{semester}",
                        TermOrder = termOrder,
                        GradeName = gradeName,
                        Semester = semester,
                        Subject = subject.SubjectName.Trim(),
                        Score = score.Value,
                        RawScore = score.Value.ToString(CultureInfo.InvariantCulture),
                        Credits = 1m,
                    });
                }
            }
        }
        profile.OverallGpa = ComputeGpa4(draft.Step2) ?? draft.Step2?.CalculatedGpa4Scale;

        // Bước 3
        var step3 = draft.Step3;
        var englishType = step3?.EnglishTestType?.Trim().ToLowerInvariant() ?? ProfileConstants.EnglishTestTypes.None;
        var hasEnglish = englishType != ProfileConstants.EnglishTestTypes.None;
        profile.EnglishTestType = englishType;
        profile.Ielts = englishType == ProfileConstants.EnglishTestTypes.Ielts ? step3?.EnglishOverallScore : null;
        profile.Toefl = englishType == ProfileConstants.EnglishTestTypes.Toefl ? step3?.EnglishOverallScore : null;
        profile.Duolingo = englishType == ProfileConstants.EnglishTestTypes.Duolingo ? step3?.EnglishOverallScore : null;
        profile.EnglishTestDate = hasEnglish ? ParseDateOnly(step3?.EnglishTestDate) : null;

        var otherTests = step3?.OtherTests ?? [];
        decimal? OtherTest(string type) => otherTests
            .FirstOrDefault(t => string.Equals(t.TestType, type, StringComparison.OrdinalIgnoreCase))?.Score;
        profile.Sat = OtherTest("SAT");
        profile.Act = OtherTest("ACT");
        var ap = OtherTest("AP");
        profile.OtherTestsJson = ap is null ? null : JsonSerializer.Serialize(new Dictionary<string, decimal> { ["ap"] = ap.Value });

        // Bước 4
        var tier = budgetTiers[draft.Step4!.AnnualBudgetTier!];
        profile.BudgetTier = tier.Id;
        profile.BudgetMinUsd = tier.MinUsd;
        profile.BudgetMaxUsd = tier.MaxUsd; // null với mức trên 70k = không giới hạn trần
        profile.ScholarshipNeed = draft.Step4.NeedFinancialAid;
        profile.NeedsScholarship = ProfileConstants.ScholarshipNeeds.NeedsScholarship(profile.ScholarshipNeed);

        // Điểm ngoại khóa cũ không còn đúng với dữ liệu mới; để bộ tính điểm tính lại
        profile.ExtracurricularScore = null;

        // Bước 5
        var today = DateOnly.FromDateTime(now);
        var activities = new List<ProfileActivity>();
        foreach (var item in draft.Step5?.Activities ?? [])
        {
            var impact = ProfileConstants.ImpactLevelCodes.ToLevel[item.ImpactLevel!];
            if (IsAward(item))
            {
                activities.Add(new ProfileActivity
                {
                    Kind = "award",
                    Title = item.Name!.Trim(),
                    ImpactLevel = impact,
                    AwardYear = item.Year,
                });
                continue;
            }

            var start = ParseDateOnly(item.StartDate)!.Value;
            var end = item.IsOngoing ? (DateOnly?)null : ParseDateOnly(item.EndDate);
            activities.Add(new ProfileActivity
            {
                Kind = "extracurricular",
                Title = item.Name!.Trim(),
                Role = item.Role,
                ImpactLevel = impact,
                StartDate = start,
                EndDate = end,
                IsOngoing = item.IsOngoing,
                DurationMonths = CountMonths(start, end ?? today),
            });
        }

        return (scores, activities);
    }

    /// <summary>Giống FE: trung bình các môn (mỗi môn = TB HK1, HK2 nếu có), quy đổi tuyến tính sang thang 4</summary>
    private static decimal? ComputeGpa4(FlowTranscriptDraftDto? step2)
    {
        var subjectAverages = new List<decimal>();
        foreach (var year in step2?.AcademicYears ?? [])
        foreach (var subject in year.Subjects ?? [])
        {
            var values = new[] { subject.Semester1Score, subject.Semester2Score }.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (values.Count > 0) subjectAverages.Add(values.Average());
        }

        if (subjectAverages.Count == 0) return null;
        var gpa10 = Math.Round(subjectAverages.Average(), 2);
        return Math.Round(gpa10 / 10m * 4m, 2);
    }

    /// <summary>Số tháng tính cả tháng đầu và tháng cuối (09/2024 → 05/2025 = 9 tháng)</summary>
    private static int CountMonths(DateOnly start, DateOnly end) =>
        Math.Max(1, (end.Year - start.Year) * 12 + end.Month - start.Month + 1);

    // ───────────────────────────── Dựng lại draft từ DB ─────────────────────────────

    private static ProfileFlowDraftDto BuildDraftFromProfile(
        StudentProfile profile, IReadOnlyList<TranscriptScore> scores, IReadOnlyList<ProfileActivity> activities)
    {
        var step1 = new FlowGoalDraftDto(
            CurrentEducationLevel: profile.CurrentEducationLevel ?? NormalizeEducationLevel(profile.EducationSystem) switch
            {
                { } code when EducationLevelCodes.Contains(code) => code,
                _ => null
            },
            CurrentGrade: profile.CurrentGrade,
            CurrentSchool: profile.CurrentSchool,
            ExpectedGraduationYear: profile.GraduationYear,
            TargetDegreeLevel: profile.TargetLevel,
            TargetMajor: profile.IntendedMajor,
            TargetStates: profile.PreferredStates?.ToList() ?? [],
            TargetSchoolType: profile.TargetSchoolType ?? "all");

        // Bảng điểm: gom theo khối, giữ thứ tự TermOrder; dữ liệu cũ không có grade_name/semester thì tách từ term_name
        var years = new List<FlowAcademicYearDraftDto>();
        foreach (var s in scores.OrderBy(s => s.TermOrder))
        {
            var (gradeName, semester) = ResolveGradeAndSemester(s);
            var year = years.FirstOrDefault(y => y.GradeName == gradeName);
            if (year is null)
            {
                year = new FlowAcademicYearDraftDto(gradeName, []);
                years.Add(year);
            }

            var index = year.Subjects!.FindIndex(x => x.SubjectName == s.Subject);
            var subject = index >= 0 ? year.Subjects[index] : new FlowSubjectDraftDto(s.Subject);
            subject = semester == 2 ? subject with { Semester2Score = s.Score } : subject with { Semester1Score = s.Score };
            if (index >= 0) year.Subjects[index] = subject;
            else year.Subjects.Add(subject);
        }
        var step2 = new FlowTranscriptDraftDto(years, profile.OverallGpa, profile.OverallGpa is { } g ? Math.Round(g / 4m * 10m, 2) : null);

        var englishType = profile.EnglishTestType
            ?? (profile.Ielts.HasValue ? ProfileConstants.EnglishTestTypes.Ielts
                : profile.Toefl.HasValue ? ProfileConstants.EnglishTestTypes.Toefl
                : profile.Duolingo.HasValue ? ProfileConstants.EnglishTestTypes.Duolingo
                : ProfileConstants.EnglishTestTypes.None);
        var otherTests = new List<FlowOtherTestDraftDto>();
        if (profile.Sat.HasValue) otherTests.Add(new FlowOtherTestDraftDto("SAT", profile.Sat));
        if (profile.Act.HasValue) otherTests.Add(new FlowOtherTestDraftDto("ACT", profile.Act));
        if (ReadApScore(profile.OtherTestsJson) is { } ap) otherTests.Add(new FlowOtherTestDraftDto("AP", ap));
        var step3 = new FlowCertificationDraftDto(
            EnglishTestType: englishType switch
            {
                ProfileConstants.EnglishTestTypes.Ielts => "IELTS",
                ProfileConstants.EnglishTestTypes.Toefl => "TOEFL",
                ProfileConstants.EnglishTestTypes.Duolingo => "Duolingo",
                _ => "none"
            },
            EnglishOverallScore: englishType switch
            {
                ProfileConstants.EnglishTestTypes.Ielts => profile.Ielts,
                ProfileConstants.EnglishTestTypes.Toefl => profile.Toefl,
                ProfileConstants.EnglishTestTypes.Duolingo => profile.Duolingo,
                _ => null
            },
            EnglishTestDate: profile.EnglishTestDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            OtherTests: otherTests);

        var step4 = new FlowFinancialDraftDto(profile.BudgetTier, profile.ScholarshipNeed);

        var step5 = new FlowActivitiesDraftDto(activities.Select(a => string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase)
            ? new FlowActivityDraftDto(
                Id: a.Id.ToString(),
                Name: a.Title,
                Category: "achievement",
                ImpactLevel: ProfileConstants.ImpactLevelCodes.FromLevel(a.ImpactLevel),
                Year: a.AwardYear ?? a.StartDate?.Year)
            : new FlowActivityDraftDto(
                Id: a.Id.ToString(),
                Name: a.Title,
                Category: "activity",
                Role: a.Role is not null && ProfileConstants.ActivityRoles.All.Contains(a.Role) ? a.Role : null,
                ImpactLevel: ProfileConstants.ImpactLevelCodes.FromLevel(a.ImpactLevel),
                StartDate: a.StartDate?.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                EndDate: a.EndDate?.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                IsOngoing: a.IsOngoing,
                DurationMonths: a.DurationMonths)).ToList());

        return new ProfileFlowDraftDto(step1, step2, step3, step4, step5);
    }

    private static (string GradeName, int Semester) ResolveGradeAndSemester(TranscriptScore s)
    {
        if (!string.IsNullOrWhiteSpace(s.GradeName) && s.Semester is 1 or 2)
            return (s.GradeName, s.Semester.Value);

        var match = TermNameRegex.Match(s.TermName ?? string.Empty);
        if (match.Success)
        {
            var grade = match.Groups["grade"].Value.Trim().TrimEnd('-').Trim();
            return (grade.Length > 0 ? grade : s.TermName!, int.Parse(match.Groups["sem"].Value));
        }
        return (string.IsNullOrWhiteSpace(s.TermName) ? "Không rõ" : s.TermName, 1);
    }

    private static decimal? ReadApScore(string? otherTestsJson)
    {
        if (string.IsNullOrWhiteSpace(otherTestsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(otherTestsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("ap", out var ap)
                && ap.ValueKind == JsonValueKind.Number
                    ? ap.GetDecimal()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Nhận MM/yyyy (tháng hoạt động), yyyy-MM (input type=month), ngày đầy đủ hoặc chỉ năm</summary>
    private static DateOnly? ParseDateOnly(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return null;
        var trimmed = str.Trim();
        if (DateOnly.TryParseExact(trimmed, ["MM/yyyy", "M/yyyy", "yyyy-MM"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthYear))
            return monthYear;
        if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, out var d)) return d;
        if (int.TryParse(trimmed, out var onlyYear) && onlyYear >= 1900 && onlyYear <= 2100)
            return new DateOnly(onlyYear, 1, 1);
        return null;
    }
}
