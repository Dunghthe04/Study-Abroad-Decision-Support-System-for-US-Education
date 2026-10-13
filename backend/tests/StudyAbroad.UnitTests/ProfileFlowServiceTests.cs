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
      ]
    }
    """;

    private sealed class InMemoryProfileFlowRepository : IProfileFlowRepository
    {
        public readonly Dictionary<Guid, StudentProfile> Profiles = new();
        public readonly Dictionary<Guid, List<TranscriptScore>> Scores = new();
        public readonly Dictionary<Guid, List<ProfileActivity>> Activities = new();

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

        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, CancellationToken ct = default)
        {
            CommitCount++;
            Profiles[profile.Id] = profile;
            Scores[profile.Id] = scores.Select(s => { s.StudentProfileId = profile.Id; return s; }).ToList();
            Activities[profile.Id] = activities.Select(a => { a.StudentProfileId = profile.Id; return a; }).ToList();
            return Task.CompletedTask;
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

    private static ProfileFlowDraftDto FullDraft(
        string? tier = "tier_above_70k",
        string? need = "optional",
        List<FlowActivityDraftDto>? activities = null) => new(
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
        Step3: new FlowCertificationDraftDto(
            EnglishTestType: "IELTS",
            EnglishOverallScore: 7.5m,
            EnglishTestDate: "2026-03",
            OtherTests: [new FlowOtherTestDraftDto("SAT", 1450m), new FlowOtherTestDraftDto("AP", 4m)]),
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
        Assert.Equal("IELTS", d.Step3!.EnglishTestType);
        Assert.Equal(7.5m, d.Step3.EnglishOverallScore);
        Assert.Equal("2026-03", d.Step3.EnglishTestDate);
        Assert.Contains(d.Step3.OtherTests!, t => t.TestType == "AP" && t.Score == 4m);
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
}
