using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service xử lý luồng nhập hồ sơ một cửa (Unified Profile Flow 6 bước):
/// - Nháp lưu đúng 1 định dạng (draft của FE) vào DraftFlowStateJson, khôi phục đúng bước đang làm dở
/// - Quản lý tệp bảng điểm nháp + OCR (kết quả OCR trả về cho FE tự ghép vào draft)
/// - Commit: validate rồi ánh xạ draft vào student_profiles / transcript_scores / profile_activities / profile_certifications
///   trong 1 transaction. Danh mục loại chứng chỉ lấy từ app_settings profile.metadata, không viết cứng trong code
/// - Mở lại hồ sơ đã gửi: dựng draft từ DB bằng BuildDraftFromProfile
/// aiCalls, usageCollector, logger: ghi token của các lần gọi advisor ngoài chat (OCR bảng điểm, bậc ngoại khóa) vào ai_calls.
///   Các client OCR và ngoại khóa bỏ số đo vào giỏ chung (usageCollector), service này đổ giỏ xuống ai_calls kèm user_id
///   (không có chat_message_id vì nằm ngoài chat). Null thì không ghi, dùng trong test cũ
/// </summary>
public class ProfileFlowService(
    IProfileFlowRepository repository,
    IProfileFileStorageService fileStorageService,
    ITranscriptOcrClient? ocrClient = null,
    IEcTierClient? ecTierClient = null,
    IGradeScaleProvider? gradeScale = null,
    IAiCallRepository? aiCalls = null,
    IAiUsageCollector? usageCollector = null,
    ILogger<ProfileFlowService>? logger = null,
    AiPricing? pricing = null) : IProfileFlowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Kỳ nhập học: giống INTAKE_PATTERN bên chat (skill_bac_hoc)
    private static readonly Regex IntakeRegex = new(@"^(Fall|Spring|Summer) 20\d\d$", RegexOptions.Compiled);
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

    /// <summary>type của chứng chỉ học sinh tự nhập tên (không có trong danh mục)</summary>
    private const string OtherCertificationType = "other";

    // Giới hạn kích thước nháp và độ dài theo cột DB: dữ liệu vượt bị từ chối thay vì lỗi 500 khi ghi
    private const int MaxDraftJsonChars = 256 * 1024;
    private const int MaxAcademicYears = 8;
    private const int MaxSubjectsPerYear = 40;
    private const int MaxCertifications = 30;
    private const int MaxActivities = 50;
    private const int MaxTargetStates = 60;
    private const int MinGraduationYear = 1950;
    private const int MaxGraduationYearsAhead = 15;

    /// <summary>Nhóm chứng chỉ trên UI bước 3; chứng chỉ "other" phải thuộc một trong các nhóm này</summary>
    private static readonly HashSet<string> CertificationCategories = ["english", "standardized", "graduate"];

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
            var certifications = await repository.GetProfileCertificationsAsync(profile.Id, ct);
            var certificationTypes = await LoadCertificationTypesAsync(ct);
            var scale = await GetGradeScaleAsync(ct);
            return new ProfileFlowStateDto(
                CurrentStep: 6,
                CompletedSteps: [1, 2, 3, 4, 5],
                Draft: BuildDraftFromProfile(profile, scores, activities, certifications, certificationTypes, scale),
                DraftTranscript: DraftTranscriptFileDto.FromOrNull(stored?.DraftTranscript),
                LastUpdatedAt: profile.UpdatedAt ?? profile.CreatedAt,
                HasCommittedProfile: true,
                LastCompletedStep: 5);
        }

        // 3. Người dùng mới: draft rỗng, không chọn sẵn ngân sách / nhu cầu học bổng
        return EmptyState(hasCommittedProfile: false) with { DraftTranscript = DraftTranscriptFileDto.FromOrNull(stored?.DraftTranscript) };
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

        await WriteStoredStateAsync(profile, stored);
        return ToStateDto(stored, HasCommittedProfile(profile));
    }

    /// <summary>
    /// C3: chat ghi các khóa vừa đổi (profile_changed) vào nháp, các ô khác giữ nguyên.
    /// Chưa có nháp thì sửa trên nháp form đang thấy: dựng từ hồ sơ đã gửi, hoặc nháp rỗng.
    /// Không đổi bước đang làm, các bước đã xong và file học bạ nháp
    /// </summary>
    public async Task<ProfileFlowStateDto> PatchDraftAsync(
        Guid userId, JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default)
    {
        var current = await GetFlowStateAsync(userId, ct);
        if (changedKeys.Count == 0) return current;

        var profile = await repository.EnsureProfileExistsAsync(userId, CancellationToken.None);
        var stored = new StoredFlowState(
            CurrentStep: current.CurrentStep,
            CompletedSteps: current.CompletedSteps.ToList(),
            Draft: NormalizeDraft(ChatProfileMapper.ApplyChatProfile(current.Draft, chatProfile, changedKeys, await GetGradeScaleAsync(ct))),
            DraftTranscript: ReadStoredState(profile.DraftFlowStateJson)?.DraftTranscript,
            LastUpdatedAt: DateTime.UtcNow);

        await WriteStoredStateAsync(profile, stored);
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

        // 1. Lưu file vào thư mục nháp (storage kiểm tra đuôi, nội dung thật và dung lượng)
        using var saveStream = new MemoryStream(fileBytes);
        var draftInfo = await fileStorageService.SaveDraftTranscriptAsync(userId, saveStream, fileName, contentType, length, ct);

        // 2. Thử OCR bóc tách điểm bảng điểm qua AI Advisor, dùng content type suy ra từ nội dung file
        TranscriptOcrResponse? ocrResponse = null;
        if (ocrClient != null)
        {
            try
            {
                var base64 = Convert.ToBase64String(fileBytes);
                ocrResponse = await ocrClient.OcrTranscriptAsync(
                    new TranscriptOcrRequest(
                        Files: [new TranscriptFilePayload(base64, fileName, draftInfo.ContentType)],
                        ImageBase64: base64,
                        FileName: fileName,
                        ContentType: draftInfo.ContentType
                    ), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // OCR lỗi (client OCR đã ghi log) thì không chặn luồng upload; FE báo người dùng tự nhập điểm
            }
            // Token của OCR (AI local đọc ảnh, Claude bóc tách) ghi ngay, tới đây profile chưa bị sửa nên an toàn
            await RecordAiCallsAsync(userId, ct);
        }

        // 3. Chỉ ghi thông tin file vào nháp; điểm OCR do FE ghép vào draft.step2 rồi tự lưu nháp
        var stored = ReadStoredState(profile.DraftFlowStateJson)
            ?? new StoredFlowState(2, [], null, null, null);
        var previousDraftPath = stored.DraftTranscript?.RelativePath;
        stored = stored with { DraftTranscript = draftInfo, LastUpdatedAt = DateTime.UtcNow };

        profile.DraftFlowStateJson = JsonSerializer.Serialize(stored, JsonOptions);
        await repository.UpdateProfileAsync(profile, ct);

        // Mỗi user chỉ giữ 1 file nháp: xoá file của lần tải trước sau khi đã lưu file mới
        if (!string.IsNullOrWhiteSpace(previousDraftPath) && previousDraftPath != draftInfo.RelativePath)
        {
            await fileStorageService.DeleteDraftFileAsync(userId, previousDraftPath, ct);
        }

        var publicInfo = DraftTranscriptFileDto.From(draftInfo);
        return new UploadDraftTranscriptResultDto(
            DraftTranscript: publicInfo,
            ExtractedTerms: ocrResponse?.Terms ?? [],
            CalculatedGpa4: await OcrGpa4Async(ocrResponse, ct),
            OcrMessage: ocrResponse?.Message,
            Warnings: ocrResponse?.Warnings,
            DraftFile: publicInfo
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
        var certificationTypes = await LoadCertificationTypesAsync(ct);
        var majorNames = (await repository.GetMajorOptionsAsync(ct)).Select(m => m.Name).ToList();
        var now = DateTime.UtcNow;
        ValidateForCommit(draft, budgetTiers, certificationTypes, majorNames, now);

        var scale = await GetGradeScaleAsync(ct);
        var (scores, activities, certifications) = ApplyDraftToProfile(profile, draft, budgetTiers, certificationTypes, scale, now);

        // Chuyển file bảng điểm nháp (theo trạng thái trên server) sang thư mục chính thức
        var previousOfficialUrl = profile.TranscriptFileUrl;
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
        // Tính bậc ngoại khóa trước khi lưu để bậc vào cùng transaction với hoạt động mới
        profile.ExtracurricularTier = await ComputeEcTierAsync(activities, ct);

        await repository.CommitProfileFlowAsync(profile, scores, activities, certifications, ct);

        // Token của lần gọi advisor đọc bậc ngoại khóa. Phải ghi SAU khi commit: ghi sớm thì SaveChanges của ai_calls sẽ lưu luôn
        // các sửa đổi dở của profile ra ngoài transaction của CommitProfileFlowAsync
        await RecordAiCallsAsync(userId, ct);

        // Đã lưu xong hồ sơ mới thì xoá file bảng điểm chính thức cũ, không giữ dữ liệu cá nhân thừa
        if (!string.IsNullOrWhiteSpace(previousOfficialUrl) && previousOfficialUrl != profile.TranscriptFileUrl)
        {
            await fileStorageService.DeleteTranscriptFileAsync(userId, previousOfficialUrl, ct);
        }

        return new CommitProfileFlowResponse(
            Succeeded: true,
            ProfileId: profile.Id,
            TargetLevel: profile.TargetLevel,
            IntendedMajor: profile.IntendedMajor,
            OverallGpa: profile.OverallGpa,
            ExtracurricularTier: profile.ExtracurricularTier,
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
        Step3: new FlowCertificationDraftDto(Certifications: []),
        Step4: new FlowFinancialDraftDto(),
        Step5: new FlowActivitiesDraftDto([]));

    private static ProfileFlowStateDto ToStateDto(StoredFlowState stored, bool hasCommittedProfile)
    {
        var completed = stored.CompletedSteps ?? [];
        return new ProfileFlowStateDto(
            CurrentStep: stored.CurrentStep is >= 1 and <= 6 ? stored.CurrentStep : 1,
            CompletedSteps: completed,
            Draft: stored.Draft ?? EmptyDraft(),
            DraftTranscript: DraftTranscriptFileDto.FromOrNull(stored.DraftTranscript),
            LastUpdatedAt: stored.LastUpdatedAt,
            HasCommittedProfile: hasCommittedProfile,
            LastCompletedStep: completed.Count > 0 ? completed.Max() : 0);
    }

    /// <summary>Kiểm tra giới hạn rồi ghi nháp; dùng chung cho form (SaveDraftAsync) và chat (PatchDraftAsync)</summary>
    private async Task WriteStoredStateAsync(StudentProfile profile, StoredFlowState stored)
    {
        var limitErrors = new List<string>();
        ValidateDraftLimits(stored.Draft!, limitErrors);
        if (limitErrors.Count > 0)
            throw new ArgumentException(string.Join(" ", limitErrors));

        var json = JsonSerializer.Serialize(stored, JsonOptions);
        if (json.Length > MaxDraftJsonChars)
            throw new ArgumentException("Dữ liệu nháp quá lớn, vui lòng rút gọn nội dung.");

        profile.DraftFlowStateJson = json;
        await repository.UpdateProfileAsync(profile, CancellationToken.None);
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

        // Mã lạ thì bỏ; không lưu chế độ đoán được, vì form chưa có nút chọn (D5) sẽ giữ nguyên chế độ cũ khi thêm điểm môn
        var step2 = draft.Step2?.GpaMode is { } mode && !ProfileConstants.GpaModes.All.Contains(mode)
            ? draft.Step2 with { GpaMode = null }
            : draft.Step2;

        return draft with { Step1 = step1, Step2 = step2, Step3 = NormalizeCertificationStep(draft.Step3), Step4 = step4 };
    }

    /// <summary>
    /// Nháp cũ lưu 1 chứng chỉ tiếng Anh (englishTestType + englishOverallScore) và otherTests (SAT/ACT/AP)
    /// → chuyển sang danh sách certifications; id loại cũ trùng id trong metadata nên chỉ cần viết thường
    /// </summary>
    private static FlowCertificationDraftDto? NormalizeCertificationStep(FlowCertificationDraftDto? step3)
    {
        if (step3 is null) return null;

        var items = step3.Certifications;
        if (items is null)
        {
            items = [];
            var englishType = step3.EnglishTestType?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(englishType) && englishType != ProfileConstants.EnglishTestTypes.None
                && step3.EnglishOverallScore is not null)
            {
                items.Add(new FlowCertificationItemDto(
                    Id: $"legacy_{englishType}",
                    Category: "english",
                    Type: englishType,
                    Overall: step3.EnglishOverallScore,
                    TestDate: ParseDateOnly(step3.EnglishTestDate)?.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
            }
            foreach (var test in step3.OtherTests ?? [])
            {
                var type = test.TestType?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(type) || test.Score is null) continue;
                items.Add(new FlowCertificationItemDto(Id: $"legacy_{type}", Category: "standardized", Type: type, Overall: test.Score));
            }
        }

        return new FlowCertificationDraftDto(
            Certifications: items,
            EnglishPlanned: step3.EnglishPlanned,
            EnglishPlannedDate: step3.EnglishPlanned ? NullIfBlank(step3.EnglishPlannedDate) : null,
            EnglishPlannedTestType: step3.EnglishPlanned ? NullIfBlank(step3.EnglishPlannedTestType) : null,
            EnglishPlannedTargetScore: step3.EnglishPlanned ? step3.EnglishPlannedTargetScore : null,
            StandardizedPlanned: step3.StandardizedPlanned,
            StandardizedPlannedDate: step3.StandardizedPlanned ? NullIfBlank(step3.StandardizedPlannedDate) : null,
            StandardizedPlannedTestType: step3.StandardizedPlanned ? NullIfBlank(step3.StandardizedPlannedTestType) : null,
            StandardizedPlannedTargetScore: step3.StandardizedPlanned ? step3.StandardizedPlannedTargetScore : null);
    }

    private static string? NormalizeEducationLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return EducationLevelLabels.TryGetValue(trimmed, out var code) ? code : trimmed;
    }

    // GPA thang 10 học sinh tự nhập lúc lưu hồ sơ. Chỉ dùng khi quy đổi theo bảng hiện tại vẫn ra đúng overall_gpa:
    // OCR bảng điểm hay mod đổi bảng quy đổi có thể đã sửa overall_gpa, lúc đó chỉ còn số thang 4.
    // Không đổi ngược thang 4 sang thang 10: bảng quy đổi không tuyến tính nên đổi ngược ra số học sinh không nhập
    private static decimal? SavedGpa10(StudentProfile profile, GradeScaleConfigDto? scale) =>
        profile.GpaMode == ProfileConstants.GpaModes.Overall
        && profile.OverallGpa10 is { } gpa10
        && profile.OverallGpa is { } gpa4
        && Round2(TranscriptGpa.To4(gpa10, scale)) == Round2(gpa4)
            ? gpa10
            : null;

    // overall_gpa là numeric(5,2): Postgres làm tròn 2 số lẻ, nửa lên
    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    // ───────────────────────────── Commit: validate + ánh xạ ─────────────────────────────

    /// <summary>Bảng quy đổi grade_scale.10 trong DB; không có provider (test cũ) thì null = bảng mặc định</summary>
    private async Task<GradeScaleConfigDto?> GetGradeScaleAsync(CancellationToken ct) =>
        gradeScale is null ? null : await gradeScale.GetAsync(ct);

    // GPA thang 4 của bảng điểm OCR: advisor tra bảng mặc định, nên .NET tra lại TB thang 10 theo bảng trong DB
    // (moderator sửa). Advisor không trả TB thang 10 thì giữ số advisor tính
    private async Task<decimal?> OcrGpa4Async(TranscriptOcrResponse? ocr, CancellationToken ct) =>
        ocr?.AverageScoreScale10 is { } average10 ? TranscriptGpa.To4(average10, await GetGradeScaleAsync(ct)) : ocr?.CalculatedGpa4;

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

    /// <summary>Danh mục loại chứng chỉ (mảng certifications trong profile.metadata), khóa theo id</summary>
    private async Task<Dictionary<string, CertificationMetadataItem>> LoadCertificationTypesAsync(CancellationToken ct)
    {
        var types = new Dictionary<string, CertificationMetadataItem>(StringComparer.OrdinalIgnoreCase);
        var json = await repository.GetAppSettingValueAsync(ProfileConstants.ProfileMetadataAppSettingKey, ct);
        if (string.IsNullOrWhiteSpace(json)) return types;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("certifications", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return types;

        foreach (var item in arr.EnumerateArray())
        {
            var type = item.Deserialize<CertificationMetadataItem>(JsonOptions);
            if (type is null || string.IsNullOrWhiteSpace(type.Id)) continue;
            types[type.Id] = type;
        }
        return types;
    }

    private static void ValidateForCommit(
        ProfileFlowDraftDto draft,
        IReadOnlyDictionary<string, BudgetTier> budgetTiers,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        IReadOnlyList<string> majorNames,
        DateTime now)
    {
        var errors = new List<string>();
        ValidateDraftLimits(draft, errors);

        // Bước 1
        var step1 = draft.Step1;
        if (!StudyLevels.IsValid(step1?.TargetDegreeLevel))
            errors.Add("Bước 1: chưa chọn bậc học muốn theo học.");
        if (step1?.CurrentEducationLevel is null || !EducationLevelCodes.Contains(step1.CurrentEducationLevel))
            errors.Add("Bước 1: chưa chọn cấp học hiện tại.");
        if (step1?.EducationSystem is { } system && !EducationSystems.IsValid(system))
            errors.Add($"Bước 1: hệ chương trình '{system}' không hợp lệ.");
        if (step1?.CurrentSchool?.Trim().Length > 256)
            errors.Add("Bước 1: tên trường tối đa 256 ký tự.");
        if (step1?.CurrentGrade?.Trim().Length > 64)
            errors.Add("Bước 1: lớp / năm học hiện tại tối đa 64 ký tự.");
        if (step1?.TargetMajor?.Trim().Length > 128)
            errors.Add("Bước 1: ngành dự định tối đa 128 ký tự.");
        // Ngành phải đúng tên trong bảng majors thì mới lọc được trường; để trống là "Chưa chọn ngành"
        if (NullIfBlank(step1?.TargetMajor) is { } major
            && !majorNames.Contains(major, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Bước 1: ngành '{major}' không có trong danh sách.");
        if (NullIfBlank(step1?.TargetIntake) is { } intake && !IntakeRegex.IsMatch(intake))
            errors.Add($"Bước 1: kỳ nhập học '{intake}' không hợp lệ (dạng Fall 2027).");
        if (step1?.ExpectedGraduationYear is { } gradYear
            && (gradYear < MinGraduationYear || gradYear > now.Year + MaxGraduationYearsAhead))
            errors.Add($"Bước 1: năm tốt nghiệp phải từ {MinGraduationYear} đến {now.Year + MaxGraduationYearsAhead}.");
        if (step1?.TargetStates?.Any(s => s?.Trim().Length > 64) == true)
            errors.Add("Bước 1: tên bang tối đa 64 ký tự.");

        // Bước 2
        if (draft.Step2?.AcademicYears?.Any(y => y.Subjects?.Any(s => s.Semester1Review is not null || s.Semester2Review is not null) == true) == true)
            errors.Add("Bước 2: bạn chưa xác nhận đã kiểm tra bảng điểm.");

        foreach (var year in draft.Step2?.AcademicYears ?? [])
        {
            if (year.GradeName?.Trim().Length > 32)
                errors.Add("Bước 2: tên khối / năm học tối đa 32 ký tự.");

            var seenSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in year.Subjects ?? [])
            {
                var subjectName = subject.SubjectName?.Trim();
                if (subjectName?.Length > 128)
                    errors.Add($"Bước 2: tên môn trong {year.GradeName} tối đa 128 ký tự.");
                else if (!string.IsNullOrEmpty(subjectName) && !seenSubjects.Add(subjectName))
                    errors.Add($"Bước 2: môn {subjectName} bị trùng trong {year.GradeName}.");

                if (subject.Semester1Score is < 0 or > 10 || subject.Semester2Score is < 0 or > 10)
                {
                    errors.Add($"Bước 2: điểm môn {subject.SubjectName} ({year.GradeName}) phải từ 0 đến 10.");
                }
            }
        }

        if (TranscriptGpa.Mode(draft.Step2) == ProfileConstants.GpaModes.Transcript)
        {
            if (TranscriptGpa.SubjectAverage10(draft.Step2) is null)
                errors.Add("Bước 2: chưa nhập điểm môn nào.");
        }
        else if (draft.Step2?.ManualGpa10Scale is { } gpa10)
        {
            if (gpa10 is < 0 or > 10) errors.Add("Bước 2: GPA thang 10 phải từ 0 đến 10.");
        }
        else if (draft.Step2?.CalculatedGpa4Scale is { } gpa4)
        {
            if (gpa4 is < 0 or > 4) errors.Add("Bước 2: GPA thang 4 phải từ 0 đến 4.");
        }
        else errors.Add("Bước 2: chưa nhập GPA.");

        // Bước 3
        ValidateCertifications(draft.Step3, certificationTypes, now, errors);

        // Bước 4
        var tierId = draft.Step4?.AnnualBudgetTier;
        if (string.IsNullOrWhiteSpace(tierId))
            errors.Add("Bước 4: chưa chọn mức ngân sách.");
        else if (!budgetTiers.ContainsKey(tierId))
            errors.Add($"Bước 4: mức ngân sách '{tierId}' không tồn tại.");
        // C6: chat cho xác nhận hồ sơ khi chưa hỏi học bổng (chỉ bắt buộc lúc chọn trường); form vẫn bắt chọn ở giao diện
        if (draft.Step4?.NeedFinancialAid is { } need && !ProfileConstants.ScholarshipNeeds.All.Contains(need))
            errors.Add($"Bước 4: nhu cầu học bổng '{need}' không hợp lệ.");
        if (draft.Step4?.FundingSource is { } funding && !ProfileConstants.FundingSources.All.Contains(funding))
            errors.Add($"Bước 4: nguồn tài chính '{funding}' không hợp lệ.");

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
            if (name.Length > 256)
            {
                errors.Add("Bước 5: tên hoạt động / giải thưởng tối đa 256 ký tự.");
                continue;
            }
            if (item.Description?.Trim().Length > 1000)
                errors.Add($"Bước 5: mô tả cho '{name}' tối đa 1000 ký tự.");
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

    /// <summary>Số phần tử tối đa của từng danh sách trong nháp (dùng cho cả lưu nháp và commit)</summary>
    private static void ValidateDraftLimits(ProfileFlowDraftDto draft, List<string> errors)
    {
        if (draft.Step1?.TargetStates?.Count > MaxTargetStates)
            errors.Add($"Bước 1: chọn tối đa {MaxTargetStates} bang.");

        var years = draft.Step2?.AcademicYears ?? [];
        if (years.Count > MaxAcademicYears)
            errors.Add($"Bước 2: tối đa {MaxAcademicYears} năm học.");
        if (years.Any(y => y.Subjects?.Count > MaxSubjectsPerYear))
            errors.Add($"Bước 2: mỗi năm học tối đa {MaxSubjectsPerYear} môn.");
        if (years.Any(y => y.Subjects?.Any(s => s.Semester1Review?.Length > 32 || s.Semester2Review?.Length > 32) == true))
            errors.Add("Bước 2: lý do kiểm tra ô điểm không hợp lệ.");

        if (draft.Step3?.Certifications?.Count > MaxCertifications)
            errors.Add($"Bước 3: tối đa {MaxCertifications} chứng chỉ.");

        if (draft.Step5?.Activities?.Count > MaxActivities)
            errors.Add($"Bước 5: tối đa {MaxActivities} hoạt động / giải thưởng.");
    }

    /// <summary>Thang điểm, điểm thành phần, môn bắt buộc đều theo danh mục trong DB</summary>
    private static void ValidateCertifications(
        FlowCertificationDraftDto? step3,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        DateTime now,
        List<string> errors)
    {
        if (step3 is null) return;
        var thisMonth = new DateOnly(now.Year, now.Month, 1);

        foreach (var item in step3.Certifications ?? [])
        {
            var typeId = item.Type?.Trim();
            if (string.IsNullOrEmpty(typeId))
            {
                errors.Add("Bước 3: có chứng chỉ chưa chọn loại.");
                continue;
            }

            string label;
            if (string.Equals(typeId, OtherCertificationType, StringComparison.OrdinalIgnoreCase))
            {
                label = NullIfBlank(item.CustomName) ?? "Khác";
                if (string.IsNullOrWhiteSpace(item.CustomName))
                    errors.Add("Bước 3: chứng chỉ \"Khác\" chưa nhập tên.");
                else if (item.CustomName.Trim().Length > 128)
                    errors.Add("Bước 3: tên chứng chỉ tối đa 128 ký tự.");
                if (item.Category is null || !CertificationCategories.Contains(item.Category))
                    errors.Add($"Bước 3: chứng chỉ '{label}' không thuộc nhóm hợp lệ.");
                if (item.Overall is < 0)
                    errors.Add($"Bước 3: điểm của '{label}' không được âm.");
                if (item.ScaleText?.Trim().Length > 32)
                    errors.Add($"Bước 3: thang điểm của '{label}' tối đa 32 ký tự.");
            }
            else if (!certificationTypes.TryGetValue(typeId, out var type))
            {
                errors.Add($"Bước 3: loại chứng chỉ '{typeId}' không có trong danh mục.");
                continue;
            }
            else
            {
                label = type.Name;
                if (type.Min is { } min && type.Max is { } max)
                {
                    // Dự định thi được để trống điểm mục tiêu
                    if (item.Overall is not { } overall)
                    {
                        if (!item.IsTarget) errors.Add($"Bước 3: {label} chưa nhập điểm.");
                    }
                    else if (!IsOnScale(overall, min, max, type.Step))
                        errors.Add($"Bước 3: điểm {label} phải từ {Fmt(min)} đến {Fmt(max)}{StepText(type.Step)}.");
                }
                else if (string.IsNullOrWhiteSpace(item.ScaleText))
                {
                    if (!item.IsTarget) errors.Add($"Bước 3: {label} chưa nhập điểm.");
                }
                else if (item.ScaleText.Trim().Length > 32)
                {
                    errors.Add($"Bước 3: điểm {label} tối đa 32 ký tự.");
                }

                foreach (var (sectionId, value) in item.Sections ?? [])
                {
                    if (value is null) continue;
                    var section = type.Sections?.FirstOrDefault(x => x.Id == sectionId);
                    if (section is null)
                        errors.Add($"Bước 3: {label} không có phần thi '{sectionId}'.");
                    else if (!IsOnScale(value.Value, section.Min, section.Max, section.Step))
                        errors.Add($"Bước 3: điểm {section.Label} của {label} phải từ {Fmt(section.Min)} đến {Fmt(section.Max)}{StepText(section.Step)}.");
                }

                if (type.RequiresSubject && !item.IsTarget && string.IsNullOrWhiteSpace(item.Subject))
                    errors.Add($"Bước 3: {label} chưa nhập {(type.SubjectLabel ?? "tên môn").ToLowerInvariant()}.");
                else if (item.Subject?.Trim().Length > 128)
                    errors.Add($"Bước 3: tên môn {label} tối đa 128 ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(item.TestDate))
            {
                var date = ParseDateOnly(item.TestDate);
                if (date is null)
                    errors.Add($"Bước 3: {(item.IsTarget ? "ngày dự kiến thi" : "ngày thi")} {label} không hợp lệ.");
                else if (!item.IsTarget && date > thisMonth)
                    errors.Add($"Bước 3: ngày thi {label} không được ở tương lai.");
                else if (item.IsTarget && date < thisMonth)
                    errors.Add($"Bước 3: ngày dự kiến thi {label} không được ở quá khứ.");
            }
        }

        if (step3.EnglishPlanned)
        {
            if (!string.IsNullOrWhiteSpace(step3.EnglishPlannedDate))
            {
                var planned = ParseDateOnly(step3.EnglishPlannedDate);
                if (planned is null)
                    errors.Add("Bước 3: tháng dự kiến thi tiếng Anh không hợp lệ.");
                else if (planned < thisMonth)
                    errors.Add("Bước 3: tháng dự kiến thi tiếng Anh không được ở quá khứ.");
            }

            if (!string.IsNullOrWhiteSpace(step3.EnglishPlannedTestType)
                && (!certificationTypes.TryGetValue(step3.EnglishPlannedTestType.Trim(), out var plannedEnglishType)
                    || !IsEnglishCategory(plannedEnglishType.Category)))
                errors.Add($"Bước 3: bài tiếng Anh dự kiến '{step3.EnglishPlannedTestType}' không có trong danh mục.");

            if (step3.EnglishPlannedTargetScore is { } engScore && !string.IsNullOrWhiteSpace(step3.EnglishPlannedTestType))
            {
                if (certificationTypes.TryGetValue(step3.EnglishPlannedTestType, out var engType) && engType.Min is { } min && engType.Max is { } max)
                {
                    if (!IsOnScale(engScore, min, max, engType.Step))
                        errors.Add($"Bước 3: điểm mục tiêu tiếng Anh ({engType.Name}) phải từ {Fmt(min)} đến {Fmt(max)}{StepText(engType.Step)}.");
                }
            }
        }

        if (step3.StandardizedPlanned)
        {
            if (!string.IsNullOrWhiteSpace(step3.StandardizedPlannedDate))
            {
                var planned = ParseDateOnly(step3.StandardizedPlannedDate);
                if (planned is null)
                    errors.Add("Bước 3: tháng dự kiến thi bài thi chuẩn hóa không hợp lệ.");
                else if (planned < thisMonth)
                    errors.Add("Bước 3: tháng dự kiến thi bài thi chuẩn hóa không được ở quá khứ.");
            }

            if (!string.IsNullOrWhiteSpace(step3.StandardizedPlannedTestType)
                && (!certificationTypes.TryGetValue(step3.StandardizedPlannedTestType.Trim(), out var plannedStandardizedType)
                    || IsEnglishCategory(plannedStandardizedType.Category)))
                errors.Add($"Bước 3: bài thi chuẩn hóa dự kiến '{step3.StandardizedPlannedTestType}' không có trong danh mục.");

            if (step3.StandardizedPlannedTargetScore is { } stdScore && !string.IsNullOrWhiteSpace(step3.StandardizedPlannedTestType))
            {
                if (certificationTypes.TryGetValue(step3.StandardizedPlannedTestType, out var stdType) && stdType.Min is { } min && stdType.Max is { } max)
                {
                    if (!IsOnScale(stdScore, min, max, stdType.Step))
                        errors.Add($"Bước 3: điểm mục tiêu bài thi chuẩn hóa ({stdType.Name}) phải từ {Fmt(min)} đến {Fmt(max)}{StepText(stdType.Step)}.");
                }
            }
        }

        ValidateTargets(CertificationsWithLegacyPlans(step3, certificationTypes), certificationTypes, errors);

        static bool IsOnScale(decimal value, decimal min, decimal max, decimal? step) =>
            value >= min && value <= max && (step is not > 0 || (value - min) % step.Value == 0);

        static string StepText(decimal? step) => step is > 0 and not 1 ? $", bước {Fmt(step.Value)}" : "";

        static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Dự định thi: mỗi loại (mỗi môn với AP / A-Level) chỉ 1 dòng mục tiêu; đã có điểm thật cùng loại thì điểm mục tiêu
    /// phải cao hơn điểm thật cao nhất (thi lại để nâng điểm)
    /// </summary>
    private static void ValidateTargets(
        IReadOnlyList<FlowCertificationItemDto> items,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        List<string> errors)
    {
        var valid = items.Where(x => !string.IsNullOrWhiteSpace(x.Type)).ToList();
        foreach (var group in valid.Where(x => x.IsTarget).GroupBy(SameTestKey))
        {
            var first = group.First();
            var label = Label(first);
            if (group.Count() > 1)
                errors.Add($"Bước 3: {label} chỉ được có 1 dự định thi.");

            if (first.Overall is not { } target) continue;
            var bestActual = valid
                .Where(x => !x.IsTarget && x.Overall is not null && SameTestKey(x) == group.Key)
                .Max(x => x.Overall);
            if (bestActual is { } best && target <= best)
                errors.Add($"Bước 3: điểm mục tiêu {label} phải cao hơn điểm đã thi ({best.ToString("0.##", CultureInfo.InvariantCulture)}).");
        }

        string Label(FlowCertificationItemDto x)
        {
            var type = x.Type!.Trim();
            var name = string.Equals(type, OtherCertificationType, StringComparison.OrdinalIgnoreCase)
                ? NullIfBlank(x.CustomName) ?? "Khác"
                : certificationTypes.TryGetValue(type, out var t) ? t.Name : type;
            return string.IsNullOrWhiteSpace(x.Subject) ? name : $"{name} {x.Subject.Trim()}";
        }

        static string SameTestKey(FlowCertificationItemDto x)
        {
            var type = x.Type!.Trim().ToLowerInvariant();
            var name = type == OtherCertificationType ? x.CustomName?.Trim().ToLowerInvariant() : null;
            return $"{type}|{name}|{x.Subject?.Trim().ToLowerInvariant()}";
        }
    }

    private const int MaxEcItems = 10;          // advisor nhận tối đa 10 hoạt động và 10 giải thưởng
    private const int MaxEcActivityText = 500;  // giới hạn độ dài text bên EcActivity (Python)
    private const int MaxEcAwardText = 300;     // giới hạn độ dài text bên EcAward (Python)
    private static readonly HashSet<string> EcRoleCodes = ["founder", "leader", "vice_leader", "member"];

    /// <summary>
    /// Bậc ngoại khóa 1–5. Không có hoạt động lẫn giải thưởng: bậc 1, không gọi SLM.
    /// Có thì gửi advisor để SLM xác nhận vai trò, phạm vi; advisor tắt hoặc lỗi thì tính bằng công thức với vai trò, phạm vi học sinh chọn.
    /// </summary>
    private async Task<int> ComputeEcTierAsync(IReadOnlyList<ProfileActivity> items, CancellationToken ct)
    {
        var valid = items.Where(a => a.ImpactLevel is >= 1 and <= 5 && !string.IsNullOrWhiteSpace(a.Title)).ToList();
        var act = valid
            .Where(a => !IsAwardKind(a))
            .Take(MaxEcItems)
            .Select(a => new EcActivityDto(
                Clip(EcText(a), MaxEcActivityText),
                a.ImpactLevel!.Value,
                Math.Clamp(a.DurationMonths ?? 0, 0, 120),
                a.Role is not null && EcRoleCodes.Contains(a.Role) ? a.Role : null))
            .ToList();
        var awards = valid
            .Where(IsAwardKind)
            .Take(MaxEcItems)
            .Select(a => new EcAwardDto(Clip(EcText(a), MaxEcAwardText), a.ImpactLevel!.Value))
            .ToList();

        if (act.Count == 0 && awards.Count == 0)
            return 1;

        var tier = ecTierClient is null
            ? null
            : await ecTierClient.ReadTierAsync(new EcTierRequest(act, awards), ct);
        return tier ?? StudyAbroad.Application.Recommendations.ActivityTier.FromProfile(valid).Tier;
    }

    // Đổ giỏ số đo (do các client OCR, ngoại khóa bỏ vào) xuống ai_calls kèm user_id; chat_message_id trống vì nằm ngoài chat.
    // Đổ giỏ sau khi lấy để lần ghi sau trong cùng yêu cầu không ghi trùng. Lỗi ghi chỉ cảnh báo (AiCallRecorder)
    private async Task RecordAiCallsAsync(Guid userId, CancellationToken ct)
    {
        if (aiCalls is null || usageCollector is null)
            return;
        var usage = usageCollector.Snapshot();
        usageCollector.Clear();
        await new AiCallRecorder(aiCalls, pricing, logger).RecordAsync(userId, null, usage, ct);
    }

    private static bool IsAwardKind(ProfileActivity a) => string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase);

    // SLM đọc tên kèm mô tả để xác nhận vai trò, phạm vi
    private static string EcText(ProfileActivity a) =>
        string.IsNullOrWhiteSpace(a.Description) ? a.Title.Trim() : $"{a.Title.Trim()}: {a.Description.Trim()}";

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static bool IsAward(FlowActivityDraftDto item) =>
        string.Equals(item.Category, "achievement", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Category, "award", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ánh xạ draft (đã validate) vào entity. Không ghi các cột luồng 6 bước không thu thập (financial_notes, max_expected_tuition_usd).</summary>
    private static (List<TranscriptScore> Scores, List<ProfileActivity> Activities, List<ProfileCertification> Certifications) ApplyDraftToProfile(
        StudentProfile profile,
        ProfileFlowDraftDto draft,
        IReadOnlyDictionary<string, BudgetTier> budgetTiers,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        GradeScaleConfigDto? scale,
        DateTime now)
    {
        // Bước 1
        var step1 = draft.Step1!;
        profile.TargetLevel = step1.TargetDegreeLevel!.Trim();
        profile.CurrentEducationLevel = step1.CurrentEducationLevel;
        profile.CurrentGrade = NullIfBlank(step1.CurrentGrade);
        // Hệ chương trình chỉ có nghĩa với học sinh cấp 3
        profile.EducationSystem = step1.CurrentEducationLevel == "high_school" ? NullIfBlank(step1.EducationSystem) : null;
        profile.CurrentSchool = NullIfBlank(step1.CurrentSchool);
        profile.GraduationYear = step1.ExpectedGraduationYear;
        profile.IntendedMajor = NullIfBlank(step1.TargetMajor);
        profile.TargetIntake = NullIfBlank(step1.TargetIntake);
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
        // GPA chỉ tính trên server từ điểm các môn, không nhận số GPA client gửi lên
        profile.OverallGpa = TranscriptGpa.Gpa4(draft.Step2, scale);
        // Giữ cách nhập và số thang 10 tự nhập để mở lại hồ sơ hiện đúng số học sinh đã nhập
        profile.GpaMode = TranscriptGpa.Mode(draft.Step2);
        profile.OverallGpa10 = profile.GpaMode == ProfileConstants.GpaModes.Overall ? draft.Step2?.ManualGpa10Scale : null;

        // Bước 3
        var certifications = BuildCertifications(draft.Step3, certificationTypes);
        ApplyLegacyCertificationColumns(profile, certifications, certificationTypes);
        // Tick nghĩa là "chưa thi và chưa có dự định thi": mục đã có chứng chỉ hoặc dự định thi (dòng mục tiêu) thì không còn tick
        profile.EnglishTestPlanned = (draft.Step3?.EnglishPlanned ?? false)
            && !certifications.Any(c => IsEnglishCategory(c.Category));
        profile.StandardizedTestPlanned = (draft.Step3?.StandardizedPlanned ?? false)
            && !certifications.Any(c => !IsEnglishCategory(c.Category));

        // Bước 4
        var tier = budgetTiers[draft.Step4!.AnnualBudgetTier!];
        profile.BudgetTier = tier.Id;
        profile.BudgetMinUsd = tier.MinUsd;
        profile.BudgetMaxUsd = tier.MaxUsd; // null với mức trên 70k = không giới hạn trần
        profile.ScholarshipNeed = draft.Step4.NeedFinancialAid;
        profile.NeedsScholarship = ProfileConstants.ScholarshipNeeds.NeedsScholarship(profile.ScholarshipNeed);
        profile.FundingSource = NullIfBlank(draft.Step4.FundingSource);

        // Bậc ngoại khóa cũ không còn đúng với hoạt động mới; để SLM xác nhận và tính lại
        profile.ExtracurricularTier = null;

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
                    Description = NullIfBlank(item.Description),
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
                Description = NullIfBlank(item.Description),
            });
        }

        return (scores, activities, certifications);
    }

    private static List<ProfileCertification> BuildCertifications(
        FlowCertificationDraftDto? step3, IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var result = new List<ProfileCertification>();
        if (step3 is null) return result;
        foreach (var item in CertificationsWithLegacyPlans(step3, certificationTypes))
        {
            var typeId = item.Type!.Trim();
            var isOther = string.Equals(typeId, OtherCertificationType, StringComparison.OrdinalIgnoreCase);
            var type = isOther ? null : certificationTypes[typeId];
            var hasNumericScale = type?.Min is not null && type.Max is not null;

            var sections = (item.Sections ?? [])
                .Where(kv => kv.Value is not null && type?.Sections?.Any(x => x.Id == kv.Key) == true)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Value);

            result.Add(new ProfileCertification
            {
                Category = type?.Category ?? item.Category!,
                TestType = isOther ? OtherCertificationType : type!.Id,
                CustomName = isOther ? NullIfBlank(item.CustomName) : null,
                Subject = type?.RequiresSubject == true ? NullIfBlank(item.Subject) : null,
                OverallScore = isOther || hasNumericScale ? item.Overall : null,
                ScaleText = isOther || !hasNumericScale ? NullIfBlank(item.ScaleText) : null,
                SectionScoresJson = sections.Count > 0 ? JsonSerializer.Serialize(sections, JsonOptions) : null,
                TestDate = ParseDateOnly(item.TestDate),
                IsTarget = item.IsTarget,
                SortOrder = result.Count,
            });
        }
        return result;
    }

    /// <summary>english là mục tiếng Anh; standardized và graduate chung mục bài thi chuẩn hóa (như form bước 3)</summary>
    private static bool IsEnglishCategory(string? category) => category == "english";

    /// <summary>
    /// Danh sách chứng chỉ kèm dự định thi kiểu cũ: form và chat cũ gửi 1 dự định mỗi mục (englishPlanned* / standardizedPlanned*)
    /// → đổi thành 1 dòng mục tiêu (IsTarget) để lưu chung bảng. Danh sách đã có dòng mục tiêu cùng loại thì bỏ qua.
    /// Bỏ hàm này khi form và chat gửi thẳng danh sách dự định
    /// </summary>
    private static List<FlowCertificationItemDto> CertificationsWithLegacyPlans(
        FlowCertificationDraftDto step3, IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var items = new List<FlowCertificationItemDto>(step3.Certifications ?? []);
        if (step3.EnglishPlanned)
            AddPlan(step3.EnglishPlannedTestType, step3.EnglishPlannedTargetScore, step3.EnglishPlannedDate, english: true);
        if (step3.StandardizedPlanned)
            AddPlan(step3.StandardizedPlannedTestType, step3.StandardizedPlannedTargetScore, step3.StandardizedPlannedDate, english: false);
        return items;

        void AddPlan(string? typeId, decimal? target, string? date, bool english)
        {
            typeId = typeId?.Trim();
            if (string.IsNullOrEmpty(typeId) || !certificationTypes.TryGetValue(typeId, out var type)) return;
            if (IsEnglishCategory(type.Category) != english) return;
            if (items.Any(x => x.IsTarget && string.Equals(x.Type?.Trim(), type.Id, StringComparison.OrdinalIgnoreCase))) return;
            items.Add(new FlowCertificationItemDto(
                Id: $"plan_{type.Id}",
                Category: type.Category,
                Type: type.Id,
                Overall: type.Min is not null && type.Max is not null ? target : null,
                TestDate: NullIfBlank(date),
                IsTarget: true));
        }
    }

    /// <summary>
    /// Gợi ý trường vẫn đọc các cột cũ (ielts, toefl, sat...). Mỗi loại có profileField trong metadata → ghi điểm cao nhất
    /// của loại đó; tiếng Anh chính là chứng chỉ có cột riêng và ngày thi gần nhất; AP ghi vào other_tests_json {"ap": điểm cao nhất}
    /// </summary>
    private static void ApplyLegacyCertificationColumns(
        StudentProfile profile,
        IReadOnlyList<ProfileCertification> certifications,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var withField = certifications
            .Where(c => c.OverallScore is not null && certificationTypes.TryGetValue(c.TestType, out var t) && t.ProfileField is not null)
            .Select(c => (Cert: c, Field: certificationTypes[c.TestType].ProfileField!))
            .ToList();

        foreach (var field in certificationTypes.Values.Select(t => t.ProfileField).OfType<string>().Distinct())
        {
            var matches = withField.Where(x => x.Field == field).ToList();
            var actual = matches.Where(x => !x.Cert.IsTarget).ToList();
            var candidates = actual.Count > 0 ? actual : matches;
            var best = candidates.Max(x => x.Cert.OverallScore);
            SetProfileField(profile, field, best);
        }

        var englishMatches = withField
            .Where(x => x.Cert.Category == "english")
            .ToList();
        var actualEnglish = englishMatches.Where(x => !x.Cert.IsTarget).ToList();
        var englishCandidates = actualEnglish.Count > 0 ? actualEnglish : englishMatches;

        var mainEnglish = englishCandidates
            .OrderByDescending(x => x.Cert.TestDate ?? DateOnly.MinValue)
            .ThenByDescending(x => x.Cert.OverallScore)
            .Select(x => x.Cert)
            .FirstOrDefault();
        profile.EnglishTestType = mainEnglish is null
            ? ProfileConstants.EnglishTestTypes.None
            : certificationTypes[mainEnglish.TestType].ProfileField;
        profile.EnglishTestDate = mainEnglish?.TestDate;
    }

    /// <summary>Tên profileField trong metadata ↔ cột trên student_profiles</summary>
    private static void SetProfileField(StudentProfile profile, string field, decimal? value)
    {
        switch (field)
        {
            case "ielts": profile.Ielts = value; break;
            case "toefl": profile.Toefl = value; break;
            case "duolingo": profile.Duolingo = value; break;
            case "sat": profile.Sat = value; break;
            case "act": profile.Act = value; break;
            case "gre": profile.Gre = value; break;
            case "gmat": profile.Gmat = value; break;
            case "ap":
                profile.OtherTestsJson = value is null
                    ? null
                    : JsonSerializer.Serialize(new Dictionary<string, decimal> { ["ap"] = value.Value });
                break;
        }
    }

    private static decimal? GetProfileField(StudentProfile profile, string field) => field switch
    {
        "ielts" => profile.Ielts,
        "toefl" => profile.Toefl,
        "duolingo" => profile.Duolingo,
        "sat" => profile.Sat,
        "act" => profile.Act,
        "gre" => profile.Gre,
        "gmat" => profile.Gmat,
        "ap" => ReadApScore(profile.OtherTestsJson),
        _ => null
    };

    /// <summary>Số tháng tính cả tháng đầu và tháng cuối (09/2024 → 05/2025 = 9 tháng)</summary>
    private static int CountMonths(DateOnly start, DateOnly end) =>
        Math.Max(1, (end.Year - start.Year) * 12 + end.Month - start.Month + 1);

    // ───────────────────────────── Dựng lại draft từ DB ─────────────────────────────

    private static ProfileFlowDraftDto BuildDraftFromProfile(
        StudentProfile profile,
        IReadOnlyList<TranscriptScore> scores,
        IReadOnlyList<ProfileActivity> activities,
        IReadOnlyList<ProfileCertification> certifications,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        GradeScaleConfigDto? scale)
    {
        var step1 = new FlowGoalDraftDto(
            CurrentEducationLevel: profile.CurrentEducationLevel ?? NormalizeEducationLevel(profile.EducationSystem) switch
            {
                { } code when EducationLevelCodes.Contains(code) => code,
                _ => null
            },
            CurrentGrade: profile.CurrentGrade,
            EducationSystem: EducationSystems.IsValid(profile.EducationSystem) ? profile.EducationSystem : null,
            CurrentSchool: profile.CurrentSchool,
            ExpectedGraduationYear: profile.GraduationYear,
            TargetDegreeLevel: profile.TargetLevel,
            TargetMajor: profile.IntendedMajor,
            TargetIntake: profile.TargetIntake,
            TargetStates: profile.PreferredStates?.ToList() ?? [],
            TargetSchoolType: profile.TargetSchoolType ?? "all",
            // Hồ sơ đã lưu mà không có ngành nghĩa là học sinh đã chọn "Chưa rõ" (form và chat đều bắt chọn)
            MajorUndecided: profile.FlowSubmittedAt.HasValue && string.IsNullOrWhiteSpace(profile.IntendedMajor) ? true : null);

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
        var step2 = new FlowTranscriptDraftDto(years, profile.OverallGpa, SavedGpa10(profile, scale), profile.GpaMode);
        // Form và chat hiện còn 1 ô dự định mỗi mục: dòng mục tiêu đầu tiên của mục đó điền vào ô dự định, các dòng còn lại hiện thành thẻ.
        // Lưu lại thì ô dự định đổi ngược thành dòng mục tiêu (CertificationsWithLegacyPlans). Bỏ khi form và chat dùng danh sách dự định
        var ordered = certifications.OrderBy(c => c.SortOrder).ToList();
        var englishPlan = ordered.FirstOrDefault(c => c.IsTarget && IsEnglishCategory(c.Category) && c.TestType != OtherCertificationType);
        var standardizedPlan = ordered.FirstOrDefault(c => c.IsTarget && !IsEnglishCategory(c.Category)
            && c.TestType != OtherCertificationType && c.Subject is null);
        var step3 = new FlowCertificationDraftDto(
            Certifications: certifications.Count > 0
                ? ordered.Where(c => c != englishPlan && c != standardizedPlan).Select(ToCertificationDraft).ToList()
                : BuildCertificationsFromLegacyColumns(profile, certificationTypes),
            EnglishPlanned: profile.EnglishTestPlanned || englishPlan is not null,
            EnglishPlannedDate: englishPlan?.TestDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            EnglishPlannedTestType: englishPlan?.TestType,
            EnglishPlannedTargetScore: englishPlan?.OverallScore,
            StandardizedPlanned: profile.StandardizedTestPlanned || standardizedPlan is not null,
            StandardizedPlannedDate: standardizedPlan?.TestDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            StandardizedPlannedTestType: standardizedPlan?.TestType,
            StandardizedPlannedTargetScore: standardizedPlan?.OverallScore);

        // Hồ sơ cũ có thể lưu mã nguồn tài chính khác (family, loan) thì để trống cho học sinh chọn lại
        var funding = profile.FundingSource is { } f && ProfileConstants.FundingSources.All.Contains(f) ? f : null;
        var step4 = new FlowFinancialDraftDto(profile.BudgetTier, profile.ScholarshipNeed, funding);

        var step5 = new FlowActivitiesDraftDto(activities.Select(a => string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase)
            ? new FlowActivityDraftDto(
                Id: a.Id.ToString(),
                Name: a.Title,
                Category: "achievement",
                ImpactLevel: ProfileConstants.ImpactLevelCodes.FromLevel(a.ImpactLevel),
                Year: a.AwardYear ?? a.StartDate?.Year,
                Description: a.Description)
            : new FlowActivityDraftDto(
                Id: a.Id.ToString(),
                Name: a.Title,
                Category: "activity",
                Role: a.Role is not null && ProfileConstants.ActivityRoles.All.Contains(a.Role) ? a.Role : null,
                ImpactLevel: ProfileConstants.ImpactLevelCodes.FromLevel(a.ImpactLevel),
                StartDate: a.StartDate?.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                EndDate: a.EndDate?.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                IsOngoing: a.IsOngoing,
                DurationMonths: a.DurationMonths,
                Description: a.Description)).ToList());

        return new ProfileFlowDraftDto(step1, step2, step3, step4, step5);
    }

    private static FlowCertificationItemDto ToCertificationDraft(ProfileCertification c) => new(
        Id: c.Id.ToString(),
        Category: c.Category,
        Type: c.TestType,
        CustomName: c.CustomName,
        Subject: c.Subject,
        Overall: c.OverallScore,
        ScaleText: c.ScaleText,
        Sections: ReadSectionScores(c.SectionScoresJson),
        TestDate: c.TestDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        IsTarget: c.IsTarget);

    /// <summary>Hồ sơ cũ chưa có dòng profile_certifications: dựng từ các cột cũ theo profileField trong metadata</summary>
    private static List<FlowCertificationItemDto> BuildCertificationsFromLegacyColumns(
        StudentProfile profile, IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var items = new List<FlowCertificationItemDto>();
        foreach (var type in certificationTypes.Values.Where(t => t.ProfileField is not null).OrderBy(t => t.Category).ThenBy(t => t.Order))
        {
            if (GetProfileField(profile, type.ProfileField!) is not { } score) continue;
            // Đang tick "chưa thi, sẽ thi sau" thì điểm trong cột là điểm kỳ vọng (lúc lưu ghi vào để gợi ý trường có điểm mà tính),
            // không phải chứng chỉ đã thi nên không dựng thành thẻ. Form tự bỏ tick khi thêm chứng chỉ thật cùng mục
            if (IsPlannedScore(profile, type.Category)) continue;
            var testDate = string.Equals(profile.EnglishTestType, type.ProfileField, StringComparison.OrdinalIgnoreCase)
                ? profile.EnglishTestDate
                : null;
            items.Add(new FlowCertificationItemDto(
                Id: $"legacy_{type.Id}",
                Category: type.Category,
                Type: type.Id,
                Overall: score,
                TestDate: testDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
        }
        return items;
    }

    /// <summary>Điểm của mục này (english | standardized | graduate) trên cột hồ sơ là điểm kỳ vọng vì học sinh đang tick "chưa thi"</summary>
    private static bool IsPlannedScore(StudentProfile profile, string category) => category == "english"
        ? profile.EnglishTestPlanned
        : profile.StandardizedTestPlanned;

    private static Dictionary<string, decimal?>? ReadSectionScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal?>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
