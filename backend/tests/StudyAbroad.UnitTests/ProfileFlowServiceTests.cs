using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        public Task<IReadOnlyList<string>> GetOfferingMajorsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Computer Science", "Business Administration"]);

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
            TargetMajor: "Computer Science",
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
        Assert.Equal(3.6m, uploadResult.CalculatedGpa4);

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
        Assert.Equal("Computer Science", p.IntendedMajor);
        Assert.Equal(["CA", "MA"], p.PreferredStates);
        Assert.Equal("private", p.TargetSchoolType);

        // Bước 2: GPA = TB môn (8.5, 7, 9) = 8.17 → 3.27
        Assert.Equal("10", p.GradeScale);
        Assert.Equal(3.27m, p.OverallGpa);
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
        Assert.Null(p.Act);
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
    [InlineData("tier_under_20k", null, "Bước 4: chưa chọn nhu cầu học bổng")]
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
    public async Task CommitFlowAsync_TwoIelts_LegacyColumnKeepsHighestScore_AndPlannedEnglishIsStored()
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
                EnglishPlanned: true,
                EnglishPlannedDate: "2099-12"),
        };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        var p = (await repo.GetProfileByUserIdAsync(userId))!;
        Assert.Equal(7m, p.Ielts);
        Assert.Null(p.Sat);
        Assert.Null(p.OtherTestsJson);
        // PTE không có cột riêng → tiếng Anh chính là IELTS có ngày thi gần nhất
        Assert.Equal("ielts", p.EnglishTestType);
        Assert.Equal(new DateOnly(2026, 5, 1), p.EnglishTestDate);
        Assert.True(p.EnglishTestPlanned);
        Assert.Equal(new DateOnly(2099, 12, 1), p.EnglishPlannedDate);
        Assert.Equal(3, repo.Certifications[p.Id].Count);
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
        var draft = FullDraft(certifications: [new(Category: "english", Type: "ielts", Overall: 7m, TestDate: "2099-01")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitFlowAsync(Guid.NewGuid(), new CommitProfileFlowRequest(draft)));

        Assert.Contains("không được ở tương lai", ex.Message);
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

    [Fact]
    public async Task CommitFlowAsync_IgnoresClientGpa_ComputesOnServerOnly()
    {
        var repo = new InMemoryProfileFlowRepository();
        var service = new ProfileFlowService(repo, new FakeFileStorageService());
        var userId = Guid.NewGuid();
        // Không có điểm môn nào nhưng client gửi GPA 4.0
        var draft = FullDraft() with { Step2 = new FlowTranscriptDraftDto(AcademicYears: [], CalculatedGpa4Scale: 4.0m) };

        await service.CommitFlowAsync(userId, new CommitProfileFlowRequest(draft));

        Assert.Null(repo.Profiles.Values.Single(p => p.UserId == userId).OverallGpa);
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
}
