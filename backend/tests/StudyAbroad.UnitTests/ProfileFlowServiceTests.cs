using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-640 Task 2] Kiểm thử đơn vị cho ProfileFlowService:
/// - Khôi phục trạng thái nháp khi người dùng quay lại
/// - Khởi tạo từ hồ sơ chính thức khi chưa có nháp mới
/// - Lưu nháp từng bước an toàn vào DraftFlowStateJson
/// - Tải tệp bảng điểm nháp
/// - Xác nhận lưu chính thức (Commit) bóc tách toàn bộ 5 bước và dọn dẹp nháp
/// - Xóa nháp
/// </summary>
public class ProfileFlowServiceTests
{
    /// <summary>Trích budgetTiers giống seed metadata (mức trên 70k không có trần)</summary>
    private const string MetadataJson = """
    {
      "budgetTiers": [
        { "id": "tier_under_20k", "minUsd": 0, "maxUsd": 20000 },
        { "id": "tier_20k_35k", "minUsd": 20000, "maxUsd": 35000 },
        { "id": "tier_35k_50k", "minUsd": 35000, "maxUsd": 50000 },
        { "id": "tier_50k_70k", "minUsd": 50000, "maxUsd": 70000 },
        { "id": "tier_above_70k", "minUsd": 70000, "maxUsd": null }
      ],
      "certifications": [
        { "id": "ielts", "name": "IELTS Academic", "category": "english", "group": "popular", "min": 0, "max": 9, "step": 0.5,
          "validityYears": 2, "profileField": "ielts",
          "sections": [ { "id": "listening", "label": "Nghe", "min": 0, "max": 9, "step": 0.5 } ] },
        { "id": "toefl", "name": "TOEFL iBT", "category": "english", "group": "popular", "min": 0, "max": 120, "step": 1, "profileField": "toefl" },
        { "id": "pte", "name": "PTE Academic", "category": "english", "group": "popular", "min": 10, "max": 90, "step": 1 },
        { "id": "sat", "name": "SAT", "category": "standardized", "group": "popular", "min": 400, "max": 1600, "step": 10, "profileField": "sat" },
        { "id": "ap", "name": "AP", "category": "standardized", "group": "popular", "min": 1, "max": 5, "step": 1,
          "requiresSubject": true, "subjectLabel": "Tên môn AP", "profileField": "ap" },
        { "id": "alevel", "name": "A-Level", "category": "standardized", "group": "other", "min": null, "max": null, "step": null,
          "requiresSubject": true, "subjectLabel": "Tên môn A-Level" }
      ]
    }
    """;

    private sealed class InMemoryProfileFlowRepository : IProfileFlowRepository
    {
        public readonly Dictionary<Guid, StudentProfile> Profiles = new();
        public readonly Dictionary<Guid, List<TranscriptScore>> Scores = new();
        public readonly Dictionary<Guid, List<ProfileActivity>> Activities = new();
        public readonly Dictionary<Guid, List<ProfileCertification>> Certifications = new();

        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(key == ProfileConstants.ProfileMetadataAppSettingKey ? MetadataJson : null);

        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MajorOption>>(
            [
                new MajorOption("Computer and information sciences", "Khoa học máy tính và thông tin", [], true),
                new MajorOption("Business/marketing", "Kinh doanh và marketing", [], true)
            ]);

        // Bảng us_states rỗng: metadata giữ bang trong JSON
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UsStateOption>>([]);

        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = Profiles.Values.FirstOrDefault(p => p.UserId == userId);
            return Task.FromResult(profile);
        }

        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = Profiles.Values.FirstOrDefault(p => p.UserId == userId);
            if (profile is null)
            {
                profile = new StudentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                Profiles[profile.Id] = profile;
            }
            return Task.FromResult(profile);
        }

        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
        {
            Profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public int CommitCount { get; private set; }

        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, IEnumerable<ProfileCertification> certifications, CancellationToken ct = default)
        {
            CommitCount++;
            Profiles[profile.Id] = profile;
            Scores[profile.Id] = scores.Select(s => { s.StudentProfileId = profile.Id; return s; }).ToList();
            Activities[profile.Id] = activities.Select(a => { a.StudentProfileId = profile.Id; return a; }).ToList();
            Certifications[profile.Id] = certifications.Select(c => { c.StudentProfileId = profile.Id; return c; }).ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default)
        {
            if (Certifications.TryGetValue(profileId, out var list))
            {
                return Task.FromResult<IReadOnlyList<ProfileCertification>>(list);
            }
            return Task.FromResult<IReadOnlyList<ProfileCertification>>([]);
        }

        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default)
        {
            if (Scores.TryGetValue(profileId, out var list))
            {
                return Task.FromResult<IReadOnlyList<TranscriptScore>>(list);
            }
            return Task.FromResult<IReadOnlyList<TranscriptScore>>([]);
        }

        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default)
        {
            if (Activities.TryGetValue(profileId, out var list))
            {
                return Task.FromResult<IReadOnlyList<ProfileActivity>>(list);
            }
            return Task.FromResult<IReadOnlyList<ProfileActivity>>([]);
        }
    }

    private sealed class FakeFileStorageService : IProfileFileStorageService
    {
        public bool CommitCalled { get; private set; }
        public bool DeleteCalled { get; private set; }

        public Task<DraftTranscriptInfoDto> SaveDraftTranscriptAsync(
            Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default)
        {
            return Task.FromResult(new DraftTranscriptInfoDto(
                OriginalFileName: fileName,
                StoredFileName: "safe_" + fileName,
                RelativePath: $"/uploads/drafts/{userId}/safe_{fileName}",
                FileSizeBytes: length,
                ContentType: contentType,
                UploadedAt: DateTime.UtcNow
            ));
        }

        public Task<string?> CommitDraftTranscriptAsync(Guid userId, string draftRelativePath, CancellationToken ct = default)
        {
            CommitCalled = true;
            var storedName = Path.GetFileName(draftRelativePath);
            return Task.FromResult<string?>($"/uploads/transcripts/{userId}/{storedName}");
        }

        public List<string?> DeletedDraftFiles { get; } = [];
        public List<string?> DeletedTranscriptFiles { get; } = [];

        public Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default)
        {
            DeletedDraftFiles.Add(draftRelativePath);
            return Task.CompletedTask;
        }

        public Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default)
        {
            DeletedTranscriptFiles.Add(transcriptRelativePath);
            return Task.CompletedTask;
        }

        public Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTranscriptOcrClient : StudyAbroad.Application.Advisor.ITranscriptOcrClient
    {
        public bool Called { get; private set; }
        public StudyAbroad.Application.Advisor.TranscriptOcrResponse ResponseToReturn { get; set; } = new(
            Success: true,
            ModelUsed: "minicpm-v",
            Terms: [
                new FlowTranscriptTermDto(
                    1,
                    "Lớp 10 HK1",
                    [new FlowTranscriptSubjectScoreDto("Toán", 9.0m, "9.0", 1m)]
                )
            ],
            AverageScoreScale10: 9.0m,
            CalculatedGpa4: 3.6m,
            Message: "Đã OCR thành công"
        );

        public Task<StudyAbroad.Application.Advisor.TranscriptOcrResponse> OcrTranscriptAsync(
            StudyAbroad.Application.Advisor.TranscriptOcrRequest request, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(ResponseToReturn);
        }
    }

    private static List<FlowCertificationItemDto> DefaultCertifications() =>
    [
        new(Id: "c1", Category: "english", Type: "ielts", Overall: 7.5m,
            Sections: new Dictionary<string, decimal?> { ["listening"] = 8m }, TestDate: "2026-03"),
        new(Id: "c2", Category: "standardized", Type: "sat", Overall: 1450m),
        new(Id: "c3", Category: "standardized", Type: "ap", Subject: "Calculus BC", Overall: 4m),
    ];

    private static ProfileFlowDraftDto FullDraft(
        string? tier = "tier_above_70k",
        string? need = "optional",
        List<FlowActivityDraftDto>? activities = null,
        List<FlowCertificationItemDto>? certifications = null) => new(
        Step1: new FlowGoalDraftDto(
            CurrentEducationLevel: "high_school",
            CurrentGrade: "Lớp 12",
            CurrentSchool: "THPT Chuyên Hà Nội - Amsterdam",
            ExpectedGraduationYear: 2027,
            TargetDegreeLevel: StudyLevels.Undergraduate,
            TargetMajor: "Computer and information sciences",
            TargetStates: ["CA", "MA"],
            TargetSchoolType: "private"),
        Step2: new FlowTranscriptDraftDto(
            AcademicYears:
            [
                new FlowAcademicYearDraftDto("Lớp 11", [new FlowSubjectDraftDto("Toán", 8m, 9m), new FlowSubjectDraftDto("Văn", 7m, null)]),
                new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Toán", 9m, null)]),
            ],
            CalculatedGpa4Scale: 3.3m),
        Step3: new FlowCertificationDraftDto(Certifications: certifications ?? DefaultCertifications()),
        Step4: new FlowFinancialDraftDto(tier, need),
        Step5: new FlowActivitiesDraftDto(activities ??
        [
            new FlowActivityDraftDto(Name: "CLB Robotics", Category: "activity", Role: "founder", ImpactLevel: "district",
                StartDate: "09/2024", EndDate: "05/2026", IsOngoing: false),
            new FlowActivityDraftDto(Name: "HSG Tin học Quốc gia", Category: "achievement", ImpactLevel: "national", Year: 2025),
        ]));

    [Fact]
    public async Task GetFlowStateAsync_WhenNewUser_ReturnsEmptyDraftWithoutFinancialDefaults()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());

        var state = await service.GetFlowStateAsync(Guid.NewGuid());

        Assert.Equal(1, state.CurrentStep);
        Assert.Empty(state.CompletedSteps);
        Assert.False(state.HasCommittedProfile);
        Assert.Null(state.Draft.Step4!.AnnualBudgetTier);
        Assert.Null(state.Draft.Step4.NeedFinancialAid);
    }

    [Fact]
    public async Task SaveDraftAsync_ThenGetFlowState_RoundTripsDraftExactly()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft();

        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(4, [3, 1, 2, 2], draft));
        var state = await service.GetFlowStateAsync(userId);

        Assert.Equal(4, state.CurrentStep);
        Assert.Equal([1, 2, 3], state.CompletedSteps);
        Assert.Equal(3, state.LastCompletedStep);
        Assert.Equal(JsonSerializer.Serialize(draft), JsonSerializer.Serialize(state.Draft));
    }

    [Fact]
    public async Task SaveDraftAsync_NormalizesLegacyDraftValues()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft(tier: "under_20k", need: "not_needed") with
        {
            Step1 = new FlowGoalDraftDto(CurrentEducationLevel: "Trung học phổ thông"),
        };

        var state = await service.SaveDraftAsync(Guid.NewGuid(), new SaveProfileDraftRequest(1, [], draft));

        Assert.Equal("high_school", state.Draft.Step1!.CurrentEducationLevel);
        Assert.Equal("tier_under_20k", state.Draft.Step4!.AnnualBudgetTier);
        Assert.Equal("optional", state.Draft.Step4.NeedFinancialAid);
    }

    [Fact]
    public async Task SaveDraftAsync_WhenStepOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveDraftAsync(Guid.NewGuid(), new SaveProfileDraftRequest(7)));
    }

    [Fact]
    public async Task UploadDraftTranscriptAsync_StoresFileInfoOnly_AndKeepsDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var ocrClient = new FakeTranscriptOcrClient();
        var service = new ProfileFlowService(repo, new FakeFileStorageService(), ocrClient);
        var userId = Guid.NewGuid();
        var draft = FullDraft();
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(2, [1], draft));

        using var memoryStream = new MemoryStream([1, 2, 3, 4]);
        var uploadResult = await service.UploadDraftTranscriptAsync(userId, memoryStream, "hoc_ba.png", "image/png", 4);

        Assert.True(ocrClient.Called);
        Assert.Single(uploadResult.ExtractedTerms);
        Assert.Equal(3.83m, Math.Round((decimal)uploadResult.CalculatedGpa4!, 2)); // TB 9.0 nội suy ra 3.833, không lấy 3.6 advisor trả

        var state = await service.GetFlowStateAsync(userId);
        Assert.Equal("hoc_ba.png", state.DraftTranscript!.OriginalFileName);
        Assert.Equal(2, state.CurrentStep);
        // OCR không ghi đè draft — FE tự ghép điểm rồi lưu nháp
        Assert.Equal(JsonSerializer.Serialize(draft), JsonSerializer.Serialize(state.Draft));
    }

    [Fact]
    public async Task UploadDraftTranscriptAsync_SecondUpload_DeletesPreviousDraftFile()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        using (var first = new MemoryStream([1, 2, 3]))
            await service.UploadDraftTranscriptAsync(userId, first, "lan1.pdf", "application/pdf", 3);
        Assert.Empty(storage.DeletedDraftFiles);

        using (var second = new MemoryStream([1, 2, 3]))
            await service.UploadDraftTranscriptAsync(userId, second, "lan2.pdf", "application/pdf", 3);

        Assert.Equal([$"/uploads/drafts/{userId}/safe_lan1.pdf"], storage.DeletedDraftFiles);
        var state = await service.GetFlowStateAsync(userId);
        Assert.Equal("lan2.pdf", state.DraftTranscript!.OriginalFileName);
    }

    [Fact]
    public async Task CommitFlowAsync_NewTranscript_DeletesPreviousOfficialFile()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        using (var first = new MemoryStream([1, 2, 3]))
            await service.UploadDraftTranscriptAsync(userId, first, "cu.pdf", "application/pdf", 3);
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(6, [1, 2, 3, 4, 5], FullDraft()));
        await service.CommitFlowAsync(userId, null);
        Assert.Empty(storage.DeletedTranscriptFiles);

        using (var second = new MemoryStream([1, 2, 3]))
            await service.UploadDraftTranscriptAsync(userId, second, "moi.pdf", "application/pdf", 3);
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(6, [1, 2, 3, 4, 5], FullDraft()));
        await service.CommitFlowAsync(userId, null);

        Assert.Equal([$"/uploads/transcripts/{userId}/safe_cu.pdf"], storage.DeletedTranscriptFiles);
        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal($"/uploads/transcripts/{userId}/safe_moi.pdf", p.TranscriptFileUrl);
    }

    [Fact]
    public async Task CommitFlowAsync_StoresEveryFieldInProfileTables()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        using var stream = new MemoryStream([1, 2, 3]);
        await service.UploadDraftTranscriptAsync(userId, stream, "transcript.pdf", "application/pdf", 3);
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(6, [1, 2, 3, 4, 5], FullDraft()));

        var response = await service.CommitFlowAsync(userId, null);

        Assert.True(response.Succeeded);
        Assert.Equal(1, repo.CommitCount);
        Assert.True(storage.CommitCalled);

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Null(p.DraftFlowStateJson);
        Assert.NotNull(p.FlowSubmittedAt);
        Assert.Contains("/uploads/transcripts/", p.TranscriptFileUrl);

        // Bước 1
        Assert.Equal(StudyLevels.Undergraduate, p.TargetLevel);
        Assert.Equal("high_school", p.CurrentEducationLevel);
        Assert.Equal("Lớp 12", p.CurrentGrade);
        Assert.Equal(2027, p.GraduationYear);
        Assert.Equal("Computer and information sciences", p.IntendedMajor);
        Assert.Equal(["CA", "MA"], p.PreferredStates);
        Assert.Equal("private", p.TargetSchoolType);

        // Bước 2: GPA = TB môn, mỗi môn (HK1 + 2×HK2)/3: (8.67, 7, 9) = 8.22 → 3.573 (nội suy mốc 8 → 3.5, 9.5 → 4.0)
        Assert.Equal("10", p.GradeScale);
        Assert.Equal(3.57m, Math.Round(p.OverallGpa!.Value, 2));
        var scores = await repo.GetTranscriptScoresAsync(p.Id);
        Assert.Equal(4, scores.Count);
        var grade12 = Assert.Single(scores, s => s.GradeName == "Lớp 12");
        Assert.Equal(1, grade12.Semester);
        Assert.Equal("Lớp 12 HK1", grade12.TermName);
        Assert.Equal(3, grade12.TermOrder);
        Assert.Equal(2, scores.Single(s => s.GradeName == "Lớp 11" && s.Semester == 2).TermOrder);

        // Bước 3
        Assert.Equal("ielts", p.EnglishTestType);
        Assert.Equal(7.5m, p.Ielts);
        Assert.Null(p.Toefl);
        Assert.Equal(new DateOnly(2026, 3, 1), p.EnglishTestDate);
        Assert.Equal(1450m, p.Sat);
        Assert.Equal("{\"ap\":4}", p.OtherTestsJson);
        var certs = repo.Certifications[p.Id];
        Assert.Equal(["ielts", "sat", "ap"], certs.OrderBy(c => c.SortOrder).Select(c => c.TestType));
        var ielts = certs.Single(c => c.TestType == "ielts");
        Assert.Equal(("english", new DateOnly(2026, 3, 1)), (ielts.Category, ielts.TestDate!.Value));
        Assert.Equal("{\"listening\":8}", ielts.SectionScoresJson);
        Assert.Equal("Calculus BC", certs.Single(c => c.TestType == "ap").Subject);

        // Bước 4: trên 70k → không có trần; không cần học bổng
        Assert.Equal("tier_above_70k", p.BudgetTier);
        Assert.Equal(70000m, p.BudgetMinUsd);
        Assert.Null(p.BudgetMaxUsd);
        Assert.Equal("optional", p.ScholarshipNeed);
        Assert.False(p.NeedsScholarship);

        // Bước 5
        var activities = await repo.GetProfileActivitiesAsync(p.Id);
        var club = activities.Single(a => a.Kind == "extracurricular");
        Assert.Equal("founder", club.Role);
        Assert.Equal(2, club.ImpactLevel);
        Assert.Equal(new DateOnly(2024, 9, 1), club.StartDate);
        Assert.Equal(21, club.DurationMonths);
        var award = activities.Single(a => a.Kind == "award");
        Assert.Equal(2025, award.AwardYear);
        Assert.Equal(4, award.ImpactLevel);
        Assert.Null(award.Role);
    }

    [Fact]
    public async Task CommitFlowAsync_ThenGetFlowState_RebuildsEquivalentDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft(tier: "tier_20k_35k", need: "mandatory");

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));
        var state = await service.GetFlowStateAsync(userId);

        Assert.True(state.HasCommittedProfile);
        var d = state.Draft;
        Assert.Equal(draft.Step1 with { TargetStates = d.Step1!.TargetStates }, d.Step1);
        Assert.Equal(["CA", "MA"], d.Step1.TargetStates!);
        Assert.Equal(["Lớp 11", "Lớp 12"], d.Step2!.AcademicYears!.Select(y => y.GradeName));
        Assert.Equal(new FlowSubjectDraftDto("Toán", 8m, 9m), d.Step2.AcademicYears![0].Subjects!.Single(x => x.SubjectName == "Toán"));
        Assert.Equal(new FlowSubjectDraftDto("Toán", 9m, null), d.Step2.AcademicYears![1].Subjects!.Single());
        var rebuilt = d.Step3!.Certifications!;
        Assert.Equal(["ielts", "sat", "ap"], rebuilt.Select(c => c.Type));
        Assert.Equal((7.5m, "2026-03", 8m), (rebuilt[0].Overall!.Value, rebuilt[0].TestDate, rebuilt[0].Sections!["listening"]!.Value));
        Assert.Equal(("Calculus BC", 4m), (rebuilt[2].Subject, rebuilt[2].Overall!.Value));
        Assert.Equal(new FlowFinancialDraftDto("tier_20k_35k", "mandatory"), d.Step4);
        var club = d.Step5!.Activities!.Single(a => a.Category == "activity");
        Assert.Equal(("founder", "district", "09/2024", "05/2026", (int?)21), (club.Role, club.ImpactLevel, club.StartDate, club.EndDate, club.DurationMonths));
        var award = d.Step5.Activities!.Single(a => a.Category == "achievement");
        Assert.Equal(("national", (int?)2025), (award.ImpactLevel, award.Year));
    }

    [Fact]
    public async Task GetFlowState_ForLegacyTranscriptRows_ParsesGradeAndSemesterFromTermName()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var profile = await repo.EnsureProfileExistsAsync(userId);
        profile.OverallGpa = 3.5m;
        repo.Scores[profile.Id] =
        [
            new TranscriptScore { TermName = "Lớp 12 - HK1", TermOrder = 5, Subject = "Toán", Score = 9m },
            new TranscriptScore { TermName = "Lớp 12 HK2", TermOrder = 6, Subject = "Toán", Score = 8m },
        ];

        var state = await service.GetFlowStateAsync(userId);

        var year = Assert.Single(state.Draft.Step2!.AcademicYears!);
        Assert.Equal("Lớp 12", year.GradeName);
        Assert.Equal(new FlowSubjectDraftDto("Toán", 9m, 8m), Assert.Single(year.Subjects!));
    }

    [Theory]
    [InlineData(null, "optional", "Bước 4: chưa chọn mức ngân sách")]
    [InlineData("tier_unknown", "optional", "không tồn tại")]
    [InlineData("tier_under_20k", "required", "Bước 4: nhu cầu học bổng 'required' không hợp lệ")]
    public async Task CommitFlowAsync_WhenFinancialStepInvalid_Throws(string? tier, string? need, string expected)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var draft = FullDraft() with { Step4 = new FlowFinancialDraftDto(tier, need) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, repo.CommitCount);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenActivityIncomplete_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft(activities:
        [
            new FlowActivityDraftDto(Name: "Tình nguyện", Category: "activity", Role: "Thành viên", ImpactLevel: "school", StartDate: "09/2024", IsOngoing: true),
            new FlowActivityDraftDto(Name: "Giải Toán", Category: "achievement", ImpactLevel: "province"),
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("'Tình nguyện' chưa chọn vai trò", ex.Message);
        Assert.Contains("'Giải Toán' chưa có năm đạt giải", ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenMajorNotInList_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft();
        draft = draft with { Step1 = draft.Step1! with { TargetMajor = "Computer Science" } };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("ngành 'Computer Science' không có trong danh sách", ex.Message);
    }

    [Theory]
    [InlineData("business/MARKETING")]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task CommitFlowAsync_MajorInListOrNotChosen_Commits(string? major)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft();
        draft = draft with { Step1 = draft.Step1! with { TargetMajor = major } };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var profile = await repo.GetProfileByUserIdAsync(userId);
        Assert.Equal(string.IsNullOrWhiteSpace(major) ? null : major, profile!.IntendedMajor);
    }

    [Fact]
    public async Task CommitFlowAsync_TwoIelts_LegacyColumnKeepsHighestScore_AndTickWithCertificatesIsDropped()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "english", Type: "ielts", Overall: 6.5m, TestDate: "2026-05"),
                    new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2025-01"),
                    new(Category: "english", Type: "pte", Overall: 70m, TestDate: "2026-06"),
                ],
                EnglishPlanned: true),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal(7m, p.Ielts);
        Assert.Null(p.Sat);
        Assert.Null(p.OtherTestsJson);
        // PTE không có cột riêng → tiếng Anh chính là IELTS có ngày thi gần nhất
        Assert.Equal("ielts", p.EnglishTestType);
        Assert.Equal(new DateOnly(2026, 5, 1), p.EnglishTestDate);
        // Tick là "chưa thi và chưa có dự định thi" nên mục đã có chứng chỉ thì không giữ tick
        Assert.False(p.EnglishTestPlanned);
        Assert.Equal(3, repo.Certifications[p.Id].Count);
    }

    [Fact]
    public async Task CommitFlowAsync_StandardizedPlanCard_IsStored_AndRestoredWhenReopened()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2026-05"),
                    new(Category: "standardized", Type: "sat", Overall: 1450m, TestDate: "2099-10", IsTarget: true),
                ]),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        // Dự định là dòng mục tiêu, không phải tick
        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.False(p.StandardizedTestPlanned);
        var plan = Assert.Single(repo.Certifications[p.Id], c => c.IsTarget);
        Assert.Equal("sat", plan.TestType);
        Assert.Equal("standardized", plan.Category);
        Assert.Equal(1450m, plan.OverallScore);
        Assert.Equal(new DateOnly(2099, 10, 1), plan.TestDate);
        Assert.Equal(1450m, p.Sat);

        // Mở lại "Sửa hồ sơ": dự định hiện lại thành thẻ mục tiêu, không có tick
        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.False(step3.StandardizedPlanned);
        var card = Assert.Single(step3.Certifications!, c => c.IsTarget);
        Assert.Equal("sat", card.Type);
        Assert.Equal("2099-10", card.TestDate);
        Assert.Equal(1450m, card.Overall);
    }

    [Fact]
    public async Task CommitFlowAsync_OnlyPlansWithTargets_ReopenKeepsTargetsAsPlans_NotAsTakenCertificates()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2099-08", IsTarget: true),
                    new(Category: "standardized", Type: "sat", Overall: 1450m, TestDate: "2099-10", IsTarget: true),
                ]),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        // Gợi ý trường vẫn tính theo điểm kỳ vọng
        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal(7m, p.Ielts);
        Assert.Equal(1450m, p.Sat);

        // Mở lại form: điểm kỳ vọng vẫn là thẻ dự định, không thành thẻ chứng chỉ "đã thi"
        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.Equal(2, step3.Certifications!.Count);
        Assert.All(step3.Certifications!, c => Assert.True(c.IsTarget));
        Assert.Contains(step3.Certifications!, c => c.Type == "ielts" && c.Overall == 7m && c.TestDate == "2099-08");
        Assert.Contains(step3.Certifications!, c => c.Type == "sat" && c.Overall == 1450m);
        Assert.False(step3.EnglishPlanned);
        Assert.False(step3.StandardizedPlanned);
    }

    [Fact]
    public async Task CommitFlowAsync_EnglishPlannedPte_WithoutProfileColumn_IsRestoredWhenReopened()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "standardized", Type: "sat", Overall: 1400m, TestDate: "2026-05"),
                    new(Category: "english", Type: "pte", Overall: 65m, TestDate: "2099-08", IsTarget: true),
                ]),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        var plan = Assert.Single(repo.Certifications[p.Id], c => c.IsTarget);
        Assert.Equal("pte", plan.TestType);
        Assert.Equal(65m, plan.OverallScore);

        // PTE không có cột điểm riêng trên hồ sơ nhưng mở lại form vẫn còn loại bài và điểm mục tiêu
        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.Contains(step3.Certifications!, c => c.IsTarget && c.Type == "pte" && c.Overall == 65m && c.TestDate == "2099-08");
    }

    [Fact]
    public async Task GetFlowStateAsync_LegacyTickWithScoreInColumn_IsNotRebuiltAsTakenCertificate()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));
        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        // Hồ sơ rất cũ chưa có dòng chứng chỉ: tick "chưa thi" và điểm kỳ vọng nằm ở cột Ielts
        repo.Certifications[p.Id].Clear();
        p.EnglishTestPlanned = true;
        p.EnglishTestType = "ielts";
        p.Ielts = 7m;

        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.True(step3.EnglishPlanned);
        Assert.DoesNotContain(step3.Certifications!, c => c.Type == "ielts");
    }

    [Fact]
    public async Task GetFlowStateAsync_LegacyProfileWithoutPlans_StillRebuildsTakenCertificatesFromColumns()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));
        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        // Hồ sơ kiểu cũ: chưa có dòng chứng chỉ, điểm chỉ nằm ở cột, không tick "chưa thi" → là điểm thật
        repo.Certifications[p.Id].Clear();
        p.Ielts = 6.5m;
        p.EnglishTestType = "ielts";
        p.Sat = 1300m;

        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        var types = step3.Certifications!.Select(c => c.Type).ToList();
        Assert.Contains("ielts", types);
        Assert.Contains("sat", types);
        Assert.All(step3.Certifications!, c => Assert.False(c.IsTarget));
        Assert.False(step3.EnglishPlanned);
    }

    [Fact]
    public async Task CommitFlowAsync_TakenCertificatesAndSeveralPlans_AreStoredTogether()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "english", Type: "ielts", Overall: 6m, TestDate: "2026-05"),
                    // Thi lại IELTS để nâng điểm, dự định thêm SAT và AP Physics C (AP chưa đặt điểm mục tiêu)
                    new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2099-08", IsTarget: true),
                    new(Category: "standardized", Type: "sat", Overall: 1450m, TestDate: "2099-10", IsTarget: true),
                    new(Category: "standardized", Type: "ap", Subject: "Physics C", TestDate: "2099-12", IsTarget: true),
                ]),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        var rows = repo.Certifications[p.Id];
        Assert.Equal(4, rows.Count);
        Assert.Equal(3, rows.Count(c => c.IsTarget));
        Assert.Null(rows.Single(c => c.TestType == "ap").OverallScore);
        // Cột gợi ý trường: có điểm thật thì lấy điểm thật, chưa có thì lấy điểm mục tiêu
        Assert.Equal(6m, p.Ielts);
        Assert.Equal(1450m, p.Sat);
        Assert.False(p.EnglishTestPlanned);
        Assert.False(p.StandardizedTestPlanned);

        // Mở lại form: đủ 4 thẻ theo thứ tự đã lưu, 3 thẻ dự định
        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.Equal(["ielts", "ielts", "sat", "ap"], step3.Certifications!.Select(c => c.Type));
        Assert.Contains(step3.Certifications!, c => c.Type == "ielts" && !c.IsTarget && c.Overall == 6m);
        Assert.Contains(step3.Certifications!, c => c.Type == "ielts" && c.IsTarget && c.Overall == 7m);
        Assert.Contains(step3.Certifications!, c => c.Type == "ap" && c.IsTarget && c.Subject == "Physics C");
        Assert.Equal(3, step3.Certifications!.Count(c => c.IsTarget));

        // Lưu lại bản mở ra vẫn đủ 4 dòng, không nhân đôi dự định
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft with { Step3 = step3 }));
        Assert.Equal(4, repo.Certifications[p.Id].Count);
        Assert.Equal(3, repo.Certifications[p.Id].Count(c => c.IsTarget));
    }

    [Theory]
    [InlineData(6.5)]
    [InlineData(6)]
    public async Task CommitFlowAsync_RetakeTargetNotAboveTakenScore_IsRejected(double target)
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "english", Type: "ielts", Overall: 6m, TestDate: "2025-05"),
                    new(Category: "english", Type: "ielts", Overall: 6.5m, TestDate: "2026-05"),
                    new(Category: "english", Type: "ielts", Overall: (decimal)target, TestDate: "2099-08", IsTarget: true),
                ]),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("phải cao hơn điểm đã thi", ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_TwoPlansOfSameTest_IsRejected()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "standardized", Type: "sat", Overall: 1400m, TestDate: "2099-08", IsTarget: true),
                    new(Category: "standardized", Type: "sat", Overall: 1500m, TestDate: "2099-12", IsTarget: true),
                ]),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("chỉ được có 1 dự định thi", ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_PlansForTwoApSubjects_AreAllowed()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications:
                [
                    new(Category: "standardized", Type: "ap", Subject: "Calculus BC", Overall: 4m, TestDate: "2026-05"),
                    new(Category: "standardized", Type: "ap", Subject: "Calculus BC", Overall: 5m, TestDate: "2099-05", IsTarget: true),
                    new(Category: "standardized", Type: "ap", Subject: "Physics C", Overall: 5m, TestDate: "2099-05", IsTarget: true),
                ]),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal(2, repo.Certifications[p.Id].Count(c => c.IsTarget));
    }

    [Fact]
    public async Task CommitFlowAsync_StandardizedNoPlanTick_IsRestoredWhenReopened()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                Certifications: [new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2026-05")],
                StandardizedPlanned: true),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var step3 = (await service.GetFlowStateAsync(userId)).Draft.Step3!;
        Assert.True(step3.StandardizedPlanned);
        Assert.DoesNotContain(step3.Certifications!, c => c.IsTarget);
    }

    [Fact]
    public async Task CommitFlowAsync_NoCertifications_IsAllowed_AndClearsLegacyColumns()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft(certifications: [])));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Empty(repo.Certifications[p.Id]);
        Assert.Null(p.Ielts);
        Assert.Null(p.Sat);
        Assert.Null(p.OtherTestsJson);
        Assert.Equal("none", p.EnglishTestType);
    }

    [Theory]
    [InlineData("gmat", 700.0, null, null, null, "không có trong danh mục")]
    [InlineData("ielts", 9.5, null, null, null, "điểm IELTS Academic phải từ 0 đến 9")]
    [InlineData("ielts", 6.3, null, null, null, "bước 0.5")]
    [InlineData("ielts", 7.0, "listening", 10.0, null, "điểm Nghe của IELTS Academic")]
    [InlineData("ielts", 7.0, "speaking", 7.0, null, "không có phần thi 'speaking'")]
    [InlineData("ap", 4.0, null, null, null, "AP chưa nhập tên môn ap")]
    [InlineData("other", 80.0, null, null, null, "chưa nhập tên")]
    [InlineData("sat", null, null, null, null, "SAT chưa nhập điểm")]
    [InlineData("alevel", null, null, null, "Toán", "A-Level chưa nhập điểm")]
    public async Task CommitFlowAsync_WhenCertificationInvalid_Throws(
        string type, double? overall, string? sectionId, double? sectionScore, string? subject, string expected)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var sections = sectionId is null ? null : new Dictionary<string, decimal?> { [sectionId] = (decimal?)sectionScore };
        var draft = FullDraft(certifications:
        [
            new(Category: "english", Type: type, Overall: (decimal?)overall, Sections: sections, Subject: subject),
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, repo.CommitCount);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenTestDateInFuture_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft(certifications: [new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2099-01", IsTarget: false)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("không được ở tương lai", ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenTargetDateInFuture_SucceedsAndStoresIsTarget()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft(certifications:
        [
            new(Category: "english", Type: "ielts", Overall: 7.5m, TestDate: "2029-06", IsTarget: true),
        ]);

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        var cert = Assert.Single(repo.Certifications[p.Id]);
        Assert.True(cert.IsTarget);
        Assert.Equal(7.5m, cert.OverallScore);
        Assert.Equal(new DateOnly(2029, 6, 1), cert.TestDate);
        Assert.Equal(7.5m, p.Ielts);
        // Chỉ có dự định: cột điểm vẫn có điểm mục tiêu để gợi ý trường, nhưng không ghi là đã thi IELTS vào tháng dự kiến
        Assert.Equal("none", p.EnglishTestType);
        Assert.Null(p.EnglishTestDate);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenTargetDateInPast_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft(certifications: [new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2020-01", IsTarget: true)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("dự định thi IELTS Academic tháng 01/2020 đã qua. Nếu đã thi, hãy nhập điểm thật; nếu chưa, đổi tháng dự kiến hoặc xóa dự định.", ex.Message);
    }

    [Theory]
    [InlineData("english", "toefl", 100.0, "dự định thi TOEFL iBT tháng 03/2021 đã qua")]
    [InlineData("standardized", "sat", 1450.0, "dự định thi SAT tháng 03/2021 đã qua")]
    public async Task CommitFlowAsync_PlanCardInPast_SaysHowToFix(string category, string type, double target, string expected)
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft(certifications:
        [
            new(Category: category, Type: type, Overall: (decimal)target, TestDate: "2021-03", IsTarget: true),
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenBothActualAndTargetExist_ActualScoreTakesPrecedenceInLegacyColumns()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft(certifications:
        [
            new(Category: "english", Type: "ielts", Overall: 6.5m, TestDate: "2025-05", IsTarget: false),
            new(Category: "english", Type: "ielts", Overall: 8.0m, TestDate: "2028-12", IsTarget: true),
        ]);

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal(6.5m, p.Ielts); // Thực tế 6.5 thắng mục tiêu 8.0 cho profile field chính thức
    }

    [Fact]
    public async Task CommitFlowAsync_OtherCertificationAndLetterGrade_AreStored()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft(certifications:
        [
            new(Category: "english", Type: "other", CustomName: "Aptis", Overall: 160m, ScaleText: "0–200", TestDate: "2026-01"),
            new(Category: "standardized", Type: "alevel", Subject: "Mathematics", ScaleText: "A*", Overall: 99m),
        ]);

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        var certs = repo.Certifications[p.Id];
        var other = certs.Single(c => c.TestType == "other");
        Assert.Equal(("english", "Aptis", 160m, "0–200"), (other.Category, other.CustomName, other.OverallScore!.Value, other.ScaleText));
        var alevel = certs.Single(c => c.TestType == "alevel");
        // A-Level không có thang số → bỏ điểm số, giữ điểm chữ
        Assert.Equal(("Mathematics", "A*", (decimal?)null), (alevel.Subject, alevel.ScaleText, alevel.OverallScore));
        Assert.Equal("none", p.EnglishTestType);
    }

    [Fact]
    public async Task SaveDraftAsync_LegacyStep3_IsConvertedToCertificationList()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var draft = FullDraft() with
        {
            Step3 = new FlowCertificationDraftDto(
                EnglishTestType: "IELTS",
                EnglishOverallScore: 7.5m,
                EnglishTestDate: "2026-03",
                OtherTests: [new FlowOtherTestDraftDto("SAT", 1450m), new FlowOtherTestDraftDto("AP", null)]),
        };

        var state = await service.SaveDraftAsync(Guid.NewGuid(), new SaveProfileDraftRequest(3, [1, 2], draft));

        var items = state.Draft.Step3!.Certifications!;
        Assert.Equal(2, items.Count);
        Assert.Equal(("english", "ielts", 7.5m, "2026-03"), (items[0].Category, items[0].Type, items[0].Overall!.Value, items[0].TestDate));
        Assert.Equal(("standardized", "sat", 1450m), (items[1].Category, items[1].Type, items[1].Overall!.Value));
        Assert.Null(state.Draft.Step3.EnglishTestType);
        Assert.Null(state.Draft.Step3.OtherTests);
    }

    [Fact]
    public async Task GetFlowState_ForProfileWithOnlyLegacyColumns_RebuildsCertificationsFromMetadata()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var profile = await repo.EnsureProfileExistsAsync(userId);
        profile.FlowSubmittedAt = DateTime.UtcNow;
        profile.Ielts = 6.5m;
        profile.EnglishTestType = "ielts";
        profile.EnglishTestDate = new DateOnly(2025, 11, 1);
        profile.Sat = 1380m;
        profile.OtherTestsJson = "{\"ap\":5}";

        var state = await service.GetFlowStateAsync(userId);

        var items = state.Draft.Step3!.Certifications!;
        var ielts = Assert.Single(items, c => c.Type == "ielts");
        Assert.Equal((6.5m, "2025-11"), (ielts.Overall!.Value, ielts.TestDate));
        Assert.Equal(1380m, Assert.Single(items, c => c.Type == "sat").Overall);
        Assert.Equal(5m, Assert.Single(items, c => c.Type == "ap").Overall);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenNoDraft_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitFlowAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task ClearDraftAsync_SetsDraftToNull_AndDeletesDraftFiles()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        var profile = await repo.EnsureProfileExistsAsync(userId);
        profile.DraftFlowStateJson = "{\"currentStep\":2}";
        await repo.UpdateProfileAsync(profile);

        await service.ClearDraftAsync(userId);

        var updatedProfile = await repo.GetProfileByUserIdAsync(userId);
        Assert.NotNull(updatedProfile);
        Assert.Null(updatedProfile!.DraftFlowStateJson);
        Assert.True(storage.DeleteCalled);
    }

    // C3: chat ghi các khóa vừa đổi vào nháp
    private static JsonObject ChatProfile(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public async Task PatchDraftAsync_ChangesOnlyRequestedKeys_KeepsStepsAndTranscriptFile()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft();
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(3, [1, 2], draft));
        using (var stream = new MemoryStream([1, 2, 3]))
            await service.UploadDraftTranscriptAsync(userId, stream, "hoc_ba.pdf", "application/pdf", 3);

        // profile có cả major nhưng chỉ budget nằm trong danh sách đổi
        var chat = ChatProfile("""
        {
          "budget": { "value": "tier_20k_35k", "status": "confirmed" },
          "major": { "value": "Biology", "status": "confirmed" }
        }
        """);
        await service.PatchDraftAsync(userId, chat, ["budget"]);
        var state = await service.GetFlowStateAsync(userId);

        Assert.Equal(3, state.CurrentStep);
        Assert.Equal([1, 2], state.CompletedSteps);
        Assert.Equal("hoc_ba.pdf", state.DraftTranscript!.OriginalFileName);
        var expected = draft with { Step4 = draft.Step4! with { AnnualBudgetTier = "tier_20k_35k" } };
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(state.Draft));
    }

    [Fact]
    public async Task PatchDraftAsync_WhenNoDraftButCommittedProfile_PatchesDraftBuiltFromProfile()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var profile = await repo.EnsureProfileExistsAsync(userId);
        profile.FlowSubmittedAt = DateTime.UtcNow;
        profile.IntendedMajor = "Computer Science";
        profile.Ielts = 6.5m;
        profile.EnglishTestType = "ielts";

        var state = await service.PatchDraftAsync(
            userId, ChatProfile("""{ "budget": { "value": "tier_35k_50k", "status": "confirmed" } }"""), ["budget"]);

        Assert.Equal(6, state.CurrentStep);
        Assert.Equal([1, 2, 3, 4, 5], state.CompletedSteps);
        Assert.Equal("tier_35k_50k", state.Draft.Step4!.AnnualBudgetTier);
        Assert.Equal("Computer Science", state.Draft.Step1!.TargetMajor);
        Assert.Equal(6.5m, Assert.Single(state.Draft.Step3!.Certifications!, c => c.Type == "ielts").Overall);
        Assert.NotNull(profile.DraftFlowStateJson);
    }

    [Fact]
    public async Task PatchDraftAsync_ForNewUser_StartsFromEmptyDraftAtStep1()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var userId = Guid.NewGuid();

        await service.PatchDraftAsync(userId, ChatProfile("""
        {
          "gpa": { "value": { "value": 8.5, "scale": 10, "source": "overall" }, "status": "said_in_B0" },
          "target_level": { "value": "undergraduate", "status": "confirmed" }
        }
        """), ["gpa", "target_level"]);
        var state = await service.GetFlowStateAsync(userId);

        Assert.Equal(1, state.CurrentStep);
        Assert.Empty(state.CompletedSteps);
        Assert.Equal(StudyLevels.Undergraduate, state.Draft.Step1!.TargetDegreeLevel);
        Assert.Equal("all", state.Draft.Step1.TargetSchoolType);
        Assert.Equal(8.5m, state.Draft.Step2!.ManualGpa10Scale);
        Assert.Null(state.Draft.Step4!.AnnualBudgetTier);
    }

    [Fact]
    public async Task PatchDraftAsync_WhenNothingChanged_DoesNotWriteDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var profile = await repo.EnsureProfileExistsAsync(userId);
        await service.PatchDraftAsync(userId, ChatProfile("""{ "budget": { "value": "tier_35k_50k", "status": "confirmed" } }"""), []);

        Assert.Null(profile.DraftFlowStateJson);
    }

    [Fact]
    public async Task SaveDraftAsync_PreservesPlanCardsAndNoPlanTick()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var step3 = new FlowCertificationDraftDto(
            Certifications:
            [
                new(Id: "p1", Category: "english", Type: "ielts", Overall: 7.5m, TestDate: "2027-10", IsTarget: true),
                new(Id: "p2", Category: "english", Type: "toefl", Overall: 100m, TestDate: "2027-11", IsTarget: true),
            ],
            StandardizedPlanned: true);
        var draft = new ProfileFlowDraftDto { Step3 = step3 };

        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(3, [1, 2], draft));
        var state = await service.GetFlowStateAsync(userId);

        Assert.NotNull(state.Draft?.Step3);
        Assert.True(state.Draft.Step3.StandardizedPlanned);
        Assert.False(state.Draft.Step3.EnglishPlanned);
        Assert.Equal(2, state.Draft.Step3.Certifications!.Count(c => c.IsTarget));
        Assert.Contains(state.Draft.Step3.Certifications!, c => c.Type == "ielts" && c.Overall == 7.5m && c.TestDate == "2027-10");
        Assert.Contains(state.Draft.Step3.Certifications!, c => c.Type == "toefl" && c.Overall == 100m && c.TestDate == "2027-11");
    }

    [Fact]
    public async Task PatchDraftAsync_StandardizedNoPlanFromChat_TicksCheckboxInDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();

        // Chat bấm "Em chưa có dự định thi" ở câu E3
        var chat = ChatProfile("""
        {
          "standardized_plan": {
            "value": { "planned": false, "date": null },
            "status": "confirmed"
          }
        }
        """);
        await service.PatchDraftAsync(userId, chat, ["standardized_plan"]);
        var state = await service.GetFlowStateAsync(userId);

        Assert.NotNull(state.Draft?.Step3);
        Assert.True(state.Draft.Step3.StandardizedPlanned);
        Assert.DoesNotContain(state.Draft.Step3.Certifications ?? [], c => c.IsTarget);
    }

    [Fact]
    public async Task PatchDraftAsync_StandardizedPlanFromChat_AddsPlanCardInDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();

        var chat = ChatProfile("""
        {
          "standardized_plan": {
            "value": { "planned": true, "date": "2099-10", "test_type": "sat", "target_score": 1450 },
            "status": "confirmed"
          }
        }
        """);
        await service.PatchDraftAsync(userId, chat, ["standardized_plan"]);
        var state = await service.GetFlowStateAsync(userId);

        Assert.NotNull(state.Draft?.Step3);
        Assert.False(state.Draft.Step3.StandardizedPlanned);
        var card = Assert.Single(state.Draft.Step3.Certifications!);
        Assert.True(card.IsTarget);
        Assert.Equal("sat", card.Type);
        Assert.Equal("2099-10", card.TestDate);
        Assert.Equal(1450m, card.Overall);
    }

    // C6: chat xác nhận hồ sơ khi chưa hỏi học bổng; chọn trường mới bắt buộc (C7)
    [Fact]
    public async Task CommitFlowAsync_WithoutScholarshipNeed_Commits()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft(need: null)));

        var profile = repo.Profiles.Values.Single(p => p.UserId == userId);
        Assert.Null(profile.ScholarshipNeed);
        Assert.False(profile.NeedsScholarship);
    }

    // gpaMode = transcript: tính từ điểm môn, bỏ qua GPA client gửi lên
    [Fact]
    public async Task CommitFlowAsync_TranscriptMode_IgnoresClientGpa()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft();
        draft = draft with { Step2 = draft.Step2! with { CalculatedGpa4Scale = 4.0m, ManualGpa10Scale = 10m, GpaMode = "transcript" } };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        Assert.Equal(3.57m, Math.Round(repo.Profiles.Values.Single(p => p.UserId == userId).OverallGpa!.Value, 2));
    }

    // gpaMode = overall: thang 10 quy đổi lại sang thang 4, không tin số thang 4 client gửi
    [Theory]
    [InlineData(8.6, 1.0, 3.7)]
    [InlineData(null, 3.6, 3.6)]
    public async Task CommitFlowAsync_OverallMode_UsesEnteredGpa(double? gpa10, double gpa4, double expected)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with
        {
            Step2 = new FlowTranscriptDraftDto([], (decimal)gpa4, (decimal?)gpa10, GpaMode: "overall"),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        Assert.Equal((decimal)expected, repo.Profiles.Values.Single(p => p.UserId == userId).OverallGpa);
    }

    // Nháp cũ không có gpaMode: không có điểm môn thì coi là GPA tổng
    [Fact]
    public async Task CommitFlowAsync_OldDraftWithoutSubjects_UsesOverallGpa()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with { Step2 = new FlowTranscriptDraftDto(AcademicYears: [], CalculatedGpa4Scale: 3.5m) };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        Assert.Equal(3.5m,repo.Profiles.Values.Single(p => p.UserId == userId).OverallGpa);
    }

    // Lưu GPA tổng 8.5 thang 10 rồi mở lại: nháp dựng từ hồ sơ trả lại đúng 8.5, không đổi ngược 3.67 ra 9.18
    [Fact]
    public async Task GetFlowState_AfterCommitOverallGpa10_ReturnsEnteredGpa()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var draft = FullDraft() with { Step2 = new FlowTranscriptDraftDto([], 3.67m, 8.5m, GpaMode: "overall") };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));
        var step2 = (await service.GetFlowStateAsync(userId)).Draft.Step2!;

        Assert.Equal(8.5m, step2.ManualGpa10Scale);
        Assert.Equal("overall", step2.GpaMode);
    }

    // overall_gpa bị sửa sau khi lưu (OCR, mod đổi bảng) hoặc hồ sơ cũ không có cách nhập: chỉ trả số thang 4
    [Theory]
    [InlineData("overall", 8.5, 3.9)]
    [InlineData(null, null, 3.67)]
    public async Task GetFlowState_WhenGpa10NotMatching_ReturnsGpa4Only(string? mode, double? gpa10, double gpa4)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var profile = await repo.EnsureProfileExistsAsync(userId);
        profile.GpaMode = mode;
        profile.OverallGpa10 = (decimal?)gpa10;
        profile.OverallGpa = (decimal)gpa4;

        var step2 = (await service.GetFlowStateAsync(userId)).Draft.Step2!;

        Assert.Null(step2.ManualGpa10Scale);
        Assert.Equal((decimal)gpa4, step2.CalculatedGpa4Scale);
    }

    [Theory]
    [InlineData(null, null, null, "Bước 2: chưa nhập GPA")]
    [InlineData("overall", 10.5, null, "Bước 2: GPA thang 10 phải từ 0 đến 10")]
    [InlineData("overall", null, 4.5, "Bước 2: GPA thang 4 phải từ 0 đến 4")]
    [InlineData("transcript", 8.0, 3.2, "Bước 2: chưa nhập điểm môn nào")]
    public async Task CommitFlowAsync_WhenGpaMissingOrOutOfRange_Throws(string? mode, double? gpa10, double? gpa4, string expected)
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var draft = FullDraft() with { Step2 = new FlowTranscriptDraftDto([], (decimal?)gpa4, (decimal?)gpa10, mode) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, repo.CommitCount);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenFieldsTooLongOrDuplicateSubject_Throws()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var baseDraft = FullDraft();
        var draft = baseDraft with
        {
            Step1 = baseDraft.Step1! with { CurrentSchool = new string('a', 257), ExpectedGraduationYear = 1800 },
            Step2 = new FlowTranscriptDraftDto(AcademicYears:
            [
                new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Toán", 8m, 9m), new FlowSubjectDraftDto(" toán ", 7m, null)]),
                new FlowAcademicYearDraftDto("Lớp 11", [new FlowSubjectDraftDto(new string('b', 129), 8m, null)]),
            ]),
            Step5 = new FlowActivitiesDraftDto(
            [
                new FlowActivityDraftDto(Name: new string('c', 257), Category: "achievement", ImpactLevel: "national", Year: 2025),
            ]),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("tên trường tối đa 256", ex.Message);
        Assert.Contains("năm tốt nghiệp phải từ", ex.Message);
        Assert.Contains("môn toán bị trùng trong Lớp 12", ex.Message);
        Assert.Contains("tên môn trong Lớp 11 tối đa 128", ex.Message);
        Assert.Contains("tên hoạt động / giải thưởng tối đa 256", ex.Message);
    }

    [Fact]
    public async Task CommitFlowAsync_WhenTranscriptCellsStillFlagged_Throws_ThenSucceedsAfterConfirm()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var userId = Guid.NewGuid();
        var flagged = FullDraft() with
        {
            Step2 = new FlowTranscriptDraftDto(AcademicYears:
            [
                new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Toán", 8m, 9m, Semester2Review: "source_image")]),
            ]),
        };

        // Cờ được giữ khi lưu nháp để người dùng quay lại vẫn thấy ô cam
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(2, [1], flagged));
        var state = await service.GetFlowStateAsync(userId);
        Assert.Equal("source_image", state.Draft!.Step2!.AcademicYears![0].Subjects![0].Semester2Review);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(userId, new CommitProfileFlowRequest(flagged)));
        Assert.Contains("chưa xác nhận đã kiểm tra bảng điểm", ex.Message);

        var confirmed = flagged with
        {
            Step2 = new FlowTranscriptDraftDto(AcademicYears:
            [
                new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Toán", 8m, 9m)]),
            ]),
        };
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(confirmed));
    }

    [Fact]
    public async Task SaveDraftAsync_WhenTooManyItems_ThrowsArgumentException_AndKeepsOldDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(1, [], FullDraft()));
        var before = repo.Profiles.Values.Single(p => p.UserId == userId).DraftFlowStateJson;

        var subjects = Enumerable.Range(1, 41).Select(i => new FlowSubjectDraftDto($"Môn {i}", 8m, null)).ToList();
        var draft = FullDraft() with { Step2 = new FlowTranscriptDraftDto(AcademicYears: [new FlowAcademicYearDraftDto("Lớp 12", subjects)]) };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveDraftAsync(userId, new SaveProfileDraftRequest(2, [1], draft)));

        Assert.Contains("tối đa 40 môn", ex.Message);
        Assert.Equal(before, repo.Profiles.Values.Single(p => p.UserId == userId).DraftFlowStateJson);
    }

    [Fact]
    public async Task SaveDraftAsync_WhenDraftTooLarge_ThrowsArgumentException()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var activities = Enumerable.Range(1, 50)
            .Select(i => new FlowActivityDraftDto(Name: $"Hoạt động {i} " + new string('x', 6000), Category: "activity"))
            .ToList();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveDraftAsync(Guid.NewGuid(), new SaveProfileDraftRequest(5, [1, 2, 3, 4], FullDraft(activities: activities))));

        Assert.Contains("quá lớn", ex.Message);
    }

    [Fact]
    public async Task UploadAndGetState_DoNotExposeStoragePath()
    {
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService());
        var userId = Guid.NewGuid();

        using var stream = new MemoryStream([1, 2, 3, 4]);
        var upload = await service.UploadDraftTranscriptAsync(userId, stream, "hoc_ba.png", "image/png", 4);
        var state = await service.GetFlowStateAsync(userId);

        foreach (var json in new[] { JsonSerializer.Serialize(upload), JsonSerializer.Serialize(state) })
        {
            Assert.Contains("hoc_ba.png", json);
            Assert.DoesNotContain("relativePath", json);
            Assert.DoesNotContain("storedFileName", json);
            Assert.DoesNotContain("/uploads/", json);
        }
    }

    // Nhật ký token của các lần gọi advisor ngoài chat (ngoại khóa, OCR)

    private sealed class FakeAiCalls : StudyAbroad.Application.Advisor.IAiCallRepository
    {
        public List<AiCall> Rows { get; } = [];
        public int Writes { get; private set; }
        public bool Fail { get; init; }
        public Action? OnWrite { get; init; }

        public Task AddRangeAsync(IReadOnlyList<AiCall> calls, CancellationToken ct = default)
        {
            Writes++;
            OnWrite?.Invoke();
            if (Fail)
                throw new InvalidOperationException("Database không ghi được");
            Rows.AddRange(calls);
            return Task.CompletedTask;
        }
    }

    // Giống EcTierClient thật: bỏ số đo vào giỏ chung của yêu cầu rồi trả bậc
    private sealed class UsageEcTierClient(StudyAbroad.Application.Advisor.IAiUsageCollector basket, int tier) : StudyAbroad.Application.Advisor.IEcTierClient
    {
        public Task<int?> ReadTierAsync(StudyAbroad.Application.Advisor.EcTierRequest request, CancellationToken ct = default)
        {
            basket.Add([new StudyAbroad.Application.Advisor.AiUsageDto("claude", "claude-haiku-5-5", "skill_ec", "1.0", 900, 120, 0, true, null, 2500)]);
            return Task.FromResult<int?>(tier);
        }
    }

    // Giống TranscriptOcrClient thật: bỏ số đo của hai bước OCR vào giỏ chung
    private sealed class UsageOcrClient(StudyAbroad.Application.Advisor.IAiUsageCollector basket) : StudyAbroad.Application.Advisor.ITranscriptOcrClient
    {
        public Task<StudyAbroad.Application.Advisor.TranscriptOcrResponse> OcrTranscriptAsync(
            StudyAbroad.Application.Advisor.TranscriptOcrRequest request, CancellationToken ct = default)
        {
            basket.Add(
            [
                new StudyAbroad.Application.Advisor.AiUsageDto("ollama", "qwen3-vl", "transcript_ocr_read", "", 700, 300, 0, true, null, 18000),
                new StudyAbroad.Application.Advisor.AiUsageDto("claude", "claude-haiku-5-5", "transcript_ocr_parse", "", 2100, 400, 0, true, null, 2200),
            ]);
            return Task.FromResult(new StudyAbroad.Application.Advisor.TranscriptOcrResponse(
                true, "qwen3-vl",
                [new FlowTranscriptTermDto(1, "Lớp 10 HK1", [new FlowTranscriptSubjectScoreDto("Toán", 9.0m, "9.0", 1m)])],
                9.0m, 4.0m, null));
        }
    }

    private static ProfileFlowService WithUsage(
        InMemoryProfileFlowRepository repo, StudyAbroad.Application.Advisor.IAiUsageCollector basket, FakeAiCalls calls) =>
        new(repo, new FakeFileStorageService(), new UsageOcrClient(basket), new UsageEcTierClient(basket, 4), aiCalls: calls, usageCollector: basket);

    [Fact]
    public async Task CommitFlowAsync_LogsTheEcCallWithTheUser_AndNoMessage_AfterTheCommit()
    {
        var repo = new InMemoryProfileFlowRepository();
        var committedWhenLogged = -1;
        var calls = new FakeAiCalls { OnWrite = () => committedWhenLogged = repo.CommitCount };
        var userId = Guid.NewGuid();

        await WithUsage(repo, new StudyAbroad.Application.Advisor.AiUsageCollector(), calls)
            .CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));

        var row = Assert.Single(calls.Rows);
        Assert.Equal(("claude", "skill_ec", userId), (row.Kind, row.Purpose, row.UserId));
        Assert.Null(row.ChatMessageId);                        // nằm ngoài chat nên không có tin nhắn để nối
        Assert.Equal((900, 120), (row.PromptTokens, row.CompletionTokens));
        Assert.Equal(1, committedWhenLogged);                  // ghi SAU commit, không để SaveChanges của ai_calls lưu sớm profile dở
    }

    [Fact]
    public async Task UploadDraftTranscriptAsync_LogsBothOcrSteps_WithTheUser()
    {
        var repo = new InMemoryProfileFlowRepository();
        var calls = new FakeAiCalls();
        var service = WithUsage(repo, new StudyAbroad.Application.Advisor.AiUsageCollector(), calls);
        var userId = Guid.NewGuid();
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(2, [1], FullDraft()));

        using var file = new MemoryStream([1, 2, 3, 4]);
        await service.UploadDraftTranscriptAsync(userId, file, "hoc_ba.png", "image/png", 4);

        Assert.Equal(["transcript_ocr_read", "transcript_ocr_parse"], calls.Rows.Select(r => r.Purpose));
        Assert.Equal(["ollama", "claude"], calls.Rows.Select(r => r.Kind));
        Assert.All(calls.Rows, r => Assert.Equal(userId, r.UserId));
        Assert.All(calls.Rows, r => Assert.Null(r.ChatMessageId));
    }

    [Fact]
    public async Task TheBasketIsEmptiedAfterEachWrite_SoNothingIsLoggedTwice()
    {
        var repo = new InMemoryProfileFlowRepository();
        var basket = new StudyAbroad.Application.Advisor.AiUsageCollector();
        var calls = new FakeAiCalls();
        var service = WithUsage(repo, basket, calls);
        var userId = Guid.NewGuid();
        await service.SaveDraftAsync(userId, new SaveProfileDraftRequest(2, [1], FullDraft()));

        using var file = new MemoryStream([1, 2, 3, 4]);
        await service.UploadDraftTranscriptAsync(userId, file, "hoc_ba.png", "image/png", 4);   // 2 dòng OCR
        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));       // thêm 1 dòng ngoại khóa

        Assert.Equal(3, calls.Rows.Count);
        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public async Task FailureToLogTokens_NeverBreaksSavingTheProfile()
    {
        var repo = new InMemoryProfileFlowRepository();
        var userId = Guid.NewGuid();

        var response = await WithUsage(repo, new StudyAbroad.Application.Advisor.AiUsageCollector(), new FakeAiCalls { Fail = true })
            .CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));

        Assert.True(response.Succeeded);
        Assert.Equal(4, (await repo.GetProfileByUserIdAsync(userId))!.ExtracurricularTier);
    }

    [Fact]
    public async Task WhenNothingWasMeasured_NothingIsWritten()
    {
        var calls = new FakeAiCalls();
        var basket = new StudyAbroad.Application.Advisor.AiUsageCollector();
        var service = new ProfileFlowService(new InMemoryProfileFlowRepository(), new FakeFileStorageService(), aiCalls: calls, usageCollector: basket);

        await service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(FullDraft()));

        Assert.Equal(0, calls.Writes);   // không có client nào gọi advisor nên giỏ rỗng
    }

    // Bậc ngoại khóa lúc lưu hồ sơ

    private sealed class FakeEcTierClient(int? tier) : StudyAbroad.Application.Advisor.IEcTierClient
    {
        public StudyAbroad.Application.Advisor.EcTierRequest? Sent { get; private set; }

        public Task<int?> ReadTierAsync(StudyAbroad.Application.Advisor.EcTierRequest request, CancellationToken ct = default)
        {
            Sent = request;
            return Task.FromResult(tier);
        }
    }

    [Fact]
    public async Task CommitFlowAsync_StoresTierFromAdvisor_AndSendsActivitiesAndAwards()
    {
        var repo = new InMemoryProfileFlowRepository();
        var ec = new FakeEcTierClient(4);
        var service = new ProfileFlowService(repo, new FakeFileStorageService(), ecTierClient: ec);
        var userId = Guid.NewGuid();

        var response = await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));

        Assert.Equal(4, (await repo.GetProfileByUserIdAsync(userId))!.ExtracurricularTier);
        Assert.Equal(4, response.ExtracurricularTier);
        var act = Assert.Single(ec.Sent!.Act);
        Assert.Equal("CLB Robotics", act.Text);
        Assert.Equal(2, act.Scope);       // district
        Assert.Equal(21, act.Months);     // 09/2024 → 05/2026, CountMonths tính cả hai đầu
        Assert.Equal("founder", act.Role);
        var award = Assert.Single(ec.Sent.Awards);
        Assert.Equal("HSG Tin học Quốc gia", award.Text);
        Assert.Equal(4, award.Level);     // national
    }

    [Fact]
    public async Task CommitFlowAsync_WhenAdvisorFails_FallsBackToFormulaTier()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService(), ecTierClient: new FakeEcTierClient(null));
        var userId = Guid.NewGuid();

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft()));

        // q = 0.4×2/5 + 0.35×1 + 0.25×21/24 = 0.73; bonus = 0.05×4 = 0.2; m = 0.93/4 = 0.23 → bậc 2
        Assert.Equal(2, (await repo.GetProfileByUserIdAsync(userId))!.ExtracurricularTier);
    }

    [Fact]
    public async Task CommitFlowAsync_WithoutActivities_StoresTier1_WithoutCallingAdvisor()
    {
        var repo = new InMemoryProfileFlowRepository();
        var ec = new FakeEcTierClient(5);
        var service = new ProfileFlowService(repo, new FakeFileStorageService(), ecTierClient: ec);
        var userId = Guid.NewGuid();

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(FullDraft(activities: [])));

        Assert.Equal(1, (await repo.GetProfileByUserIdAsync(userId))!.ExtracurricularTier);
        Assert.Null(ec.Sent);
    }

    [Fact]
    public async Task CommitFlowAsync_SendsDescriptionWithName_ForSlmToRead()
    {
        var repo = new InMemoryProfileFlowRepository();
        var ec = new FakeEcTierClient(3);
        var service = new ProfileFlowService(repo, new FakeFileStorageService(), ecTierClient: ec);
        var draft = FullDraft(activities:
        [
            new FlowActivityDraftDto(Name: "CLB Tiếng Anh", Category: "activity", Role: "member", ImpactLevel: "school",
                StartDate: "09/2025", EndDate: "05/2026", IsOngoing: false, Description: "Tổ chức 3 buổi debate"),
        ]);

        await service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft));

        var act = Assert.Single(ec.Sent!.Act);
        Assert.Equal("CLB Tiếng Anh: Tổ chức 3 buổi debate", act.Text);
        Assert.Equal(1, act.Scope);
        Assert.Equal("member", act.Role);
        Assert.Empty(ec.Sent.Awards);
    }
}
