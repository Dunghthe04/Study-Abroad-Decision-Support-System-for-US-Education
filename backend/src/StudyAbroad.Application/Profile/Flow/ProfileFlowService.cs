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
/// - Commit: validate rồi ánh xạ draft vào student_profiles / transcript_scores / profile_activities / profile_certifications
///   trong 1 transaction. Danh mục loại chứng chỉ lấy từ app_settings profile.metadata, không viết cứng trong code
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
            return new ProfileFlowStateDto(
                CurrentStep: 6,
                CompletedSteps: [1, 2, 3, 4, 5],
                Draft: BuildDraftFromProfile(profile, scores, activities, certifications, certificationTypes),
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

        var limitErrors = new List<string>();
        ValidateDraftLimits(stored.Draft!, limitErrors);
        if (limitErrors.Count > 0)
            throw new ArgumentException(string.Join(" ", limitErrors));

        var json = JsonSerializer.Serialize(stored, JsonOptions);
        if (json.Length > MaxDraftJsonChars)
            throw new ArgumentException("Dữ liệu nháp quá lớn, vui lòng rút gọn nội dung.");

        profile.DraftFlowStateJson = json;
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
            CalculatedGpa4: ocrResponse?.CalculatedGpa4,
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
        var now = DateTime.UtcNow;
        ValidateForCommit(draft, budgetTiers, certificationTypes, now);

        var (scores, activities, certifications) = ApplyDraftToProfile(profile, draft, budgetTiers, certificationTypes, now);

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

        await repository.CommitProfileFlowAsync(profile, scores, activities, certifications, ct);

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
            EnglishPlannedDate: step3.EnglishPlanned ? NullIfBlank(step3.EnglishPlannedDate) : null);
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
        if (step1?.CurrentSchool?.Trim().Length > 256)
            errors.Add("Bước 1: tên trường tối đa 256 ký tự.");
        if (step1?.CurrentGrade?.Trim().Length > 64)
            errors.Add("Bước 1: lớp / năm học hiện tại tối đa 64 ký tự.");
        if (step1?.TargetMajor?.Trim().Length > 128)
            errors.Add("Bước 1: ngành dự định tối đa 128 ký tự.");
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
                    if (item.Overall is not { } overall)
                        errors.Add($"Bước 3: {label} chưa nhập điểm.");
                    else if (!IsOnScale(overall, min, max, type.Step))
                        errors.Add($"Bước 3: điểm {label} phải từ {Fmt(min)} đến {Fmt(max)}{StepText(type.Step)}.");
                }
                else if (string.IsNullOrWhiteSpace(item.ScaleText))
                {
                    errors.Add($"Bước 3: {label} chưa nhập điểm.");
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

                if (type.RequiresSubject && string.IsNullOrWhiteSpace(item.Subject))
                    errors.Add($"Bước 3: {label} chưa nhập {(type.SubjectLabel ?? "tên môn").ToLowerInvariant()}.");
                else if (item.Subject?.Trim().Length > 128)
                    errors.Add($"Bước 3: tên môn {label} tối đa 128 ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(item.TestDate))
            {
                var date = ParseDateOnly(item.TestDate);
                if (date is null)
                    errors.Add($"Bước 3: ngày thi {label} không hợp lệ.");
                else if (date > thisMonth)
                    errors.Add($"Bước 3: ngày thi {label} không được ở tương lai.");
            }
        }

        if (step3.EnglishPlanned && !string.IsNullOrWhiteSpace(step3.EnglishPlannedDate))
        {
            var planned = ParseDateOnly(step3.EnglishPlannedDate);
            if (planned is null)
                errors.Add("Bước 3: tháng dự kiến thi tiếng Anh không hợp lệ.");
            else if (planned < thisMonth)
                errors.Add("Bước 3: tháng dự kiến thi tiếng Anh không được ở quá khứ.");
        }

        static bool IsOnScale(decimal value, decimal min, decimal max, decimal? step) =>
            value >= min && value <= max && (step is not > 0 || (value - min) % step.Value == 0);

        static string StepText(decimal? step) => step is > 0 and not 1 ? $", bước {Fmt(step.Value)}" : "";

        static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool IsAward(FlowActivityDraftDto item) =>
        string.Equals(item.Category, "achievement", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Category, "award", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ánh xạ draft (đã validate) vào entity. Không ghi các cột luồng 6 bước không thu thập (funding_source, financial_notes, max_expected_tuition_usd).</summary>
    private static (List<TranscriptScore> Scores, List<ProfileActivity> Activities, List<ProfileCertification> Certifications) ApplyDraftToProfile(
        StudentProfile profile,
        ProfileFlowDraftDto draft,
        IReadOnlyDictionary<string, BudgetTier> budgetTiers,
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes,
        DateTime now)
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
        // GPA chỉ tính trên server từ điểm các môn, không nhận số GPA client gửi lên
        profile.OverallGpa = TranscriptGpa.Gpa4(draft.Step2);

        // Bước 3
        var certifications = BuildCertifications(draft.Step3, certificationTypes);
        ApplyLegacyCertificationColumns(profile, certifications, certificationTypes);
        profile.EnglishTestPlanned = draft.Step3?.EnglishPlanned ?? false;
        profile.EnglishPlannedDate = profile.EnglishTestPlanned ? ParseDateOnly(draft.Step3?.EnglishPlannedDate) : null;

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

        return (scores, activities, certifications);
    }

    private static List<ProfileCertification> BuildCertifications(
        FlowCertificationDraftDto? step3, IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var result = new List<ProfileCertification>();
        foreach (var item in step3?.Certifications ?? [])
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
                SortOrder = result.Count,
            });
        }
        return result;
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
            var best = withField.Where(x => x.Field == field).Max(x => x.Cert.OverallScore);
            SetProfileField(profile, field, best);
        }

        var mainEnglish = withField
            .Where(x => x.Cert.Category == "english")
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
        IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
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
        var step3 = new FlowCertificationDraftDto(
            Certifications: certifications.Count > 0
                ? certifications.OrderBy(c => c.SortOrder).Select(ToCertificationDraft).ToList()
                : BuildCertificationsFromLegacyColumns(profile, certificationTypes),
            EnglishPlanned: profile.EnglishTestPlanned,
            EnglishPlannedDate: profile.EnglishPlannedDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture));

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

    private static FlowCertificationItemDto ToCertificationDraft(ProfileCertification c) => new(
        Id: c.Id.ToString(),
        Category: c.Category,
        Type: c.TestType,
        CustomName: c.CustomName,
        Subject: c.Subject,
        Overall: c.OverallScore,
        ScaleText: c.ScaleText,
        Sections: ReadSectionScores(c.SectionScoresJson),
        TestDate: c.TestDate?.ToString("yyyy-MM", CultureInfo.InvariantCulture));

    /// <summary>Hồ sơ cũ chưa có dòng profile_certifications: dựng từ các cột cũ theo profileField trong metadata</summary>
    private static List<FlowCertificationItemDto> BuildCertificationsFromLegacyColumns(
        StudentProfile profile, IReadOnlyDictionary<string, CertificationMetadataItem> certificationTypes)
    {
        var items = new List<FlowCertificationItemDto>();
        foreach (var type in certificationTypes.Values.Where(t => t.ProfileField is not null).OrderBy(t => t.Category).ThenBy(t => t.Order))
        {
            if (GetProfileField(profile, type.ProfileField!) is not { } score) continue;
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
