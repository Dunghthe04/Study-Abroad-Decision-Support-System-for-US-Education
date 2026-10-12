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
    private sealed class InMemoryProfileFlowRepository : IProfileFlowRepository
    {
        public readonly Dictionary<Guid, StudentProfile> Profiles = new();
        public readonly Dictionary<Guid, List<TranscriptScore>> Scores = new();
        public readonly Dictionary<Guid, List<ProfileActivity>> Activities = new();

        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

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

        public Task ReplaceTranscriptScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
        {
            Scores[profileId] = scores.ToList();
            return Task.CompletedTask;
        }

        public Task ReplaceProfileActivitiesAsync(Guid profileId, IEnumerable<ProfileActivity> activities, CancellationToken ct = default)
        {
            Activities[profileId] = activities.ToList();
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

    [Fact]
    public async Task GetFlowStateAsync_WhenNewUser_ReturnsDefaultStep1State()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        var state = await service.GetFlowStateAsync(userId);

        Assert.NotNull(state);
        Assert.Equal(1, state.CurrentStep);
        Assert.Empty(state.CompletedSteps);
        Assert.False(state.HasCommittedProfile);
        Assert.NotNull(state.StepData.Step1);
        Assert.Equal(StudyLevels.Undergraduate, state.StepData.Step1!.TargetLevel);
        Assert.Equal("Lớp 11", state.StepData.Step1!.CurrentGrade);
    }

    [Fact]
    public async Task GetFlowStateAsync_WhenDraftExists_RestoresDraftState()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        var profile = await repo.EnsureProfileExistsAsync(userId);
        var draftState = new ProfileFlowStateDto(
            CurrentStep: 3,
            CompletedSteps: [1, 2],
            StepData: new ProfileFlowStepDataDto(
                Step1: new Step1GeneralDto(
                    TargetLevel: StudyLevels.Undergraduate,
                    CurrentEducationLevel: "high_school",
                    CurrentSchool: "THPT Chuyên Hà Nội - Amsterdam",
                    CurrentGrade: "Lớp 12",
                    GraduationYear: 2026,
                    IntendedMajor: "Computer Science",
                    PreferredStates: ["CA", "MA"],
                    InstitutionType: "university"
                )
            ),
            DraftTranscript: null,
            LastUpdatedAt: DateTime.UtcNow,
            HasCommittedProfile: false
        );
        profile.DraftFlowStateJson = JsonSerializer.Serialize(draftState);
        await repo.UpdateProfileAsync(profile);

        var restoredState = await service.GetFlowStateAsync(userId);

        Assert.Equal(3, restoredState.CurrentStep);
        Assert.Equal([1, 2], restoredState.CompletedSteps);
        Assert.Equal("THPT Chuyên Hà Nội - Amsterdam", restoredState.StepData.Step1!.CurrentSchool);
        Assert.Equal("Computer Science", restoredState.StepData.Step1!.IntendedMajor);
        Assert.Contains("CA", restoredState.StepData.Step1!.PreferredStates);
        Assert.Contains("MA", restoredState.StepData.Step1!.PreferredStates);
    }

    [Fact]
    public async Task SaveDraftAsync_UpdatesDraftFlowStateJson()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        var request = new SaveProfileDraftRequest(
            CurrentStep: 2,
            CompletedSteps: [1],
            StepData: new ProfileFlowStepDataDto(
                Step1: new Step1GeneralDto(
                    TargetLevel: StudyLevels.Undergraduate,
                    CurrentEducationLevel: "high_school",
                    CurrentSchool: "THPT Lê Hồng Phong",
                    CurrentGrade: "Lớp 11",
                    GraduationYear: 2027,
                    IntendedMajor: "Business Administration",
                    PreferredStates: ["NY"],
                    InstitutionType: "any"
                )
            )
        );

        var savedState = await service.SaveDraftAsync(userId, request);

        Assert.Equal(2, savedState.CurrentStep);
        Assert.Equal([1], savedState.CompletedSteps);
        Assert.Equal("THPT Lê Hồng Phong", savedState.StepData.Step1!.CurrentSchool);

        var profile = await repo.GetProfileByUserIdAsync(userId);
        Assert.NotNull(profile);
        Assert.False(string.IsNullOrWhiteSpace(profile!.DraftFlowStateJson));
        Assert.Contains("THPT Lê Hồng Phong", profile.DraftFlowStateJson);
    }

    [Fact]
    public async Task SaveDraftAsync_WhenStepOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        var invalidRequest = new SaveProfileDraftRequest(
            CurrentStep: 7,
            CompletedSteps: [],
            StepData: new ProfileFlowStepDataDto()
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveDraftAsync(userId, invalidRequest));
    }

    [Fact]
    public async Task UploadDraftTranscriptAsync_StoresDraftFileInfoInState()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        using var memoryStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var draftInfo = await service.UploadDraftTranscriptAsync(
            userId, memoryStream, "bang_diem.pdf", "application/pdf", 4);

        Assert.Equal("bang_diem.pdf", draftInfo.OriginalFileName);
        Assert.Contains("/uploads/drafts/", draftInfo.RelativePath);

        var profile = await repo.GetProfileByUserIdAsync(userId);
        Assert.NotNull(profile);
        Assert.Contains("bang_diem.pdf", profile!.DraftFlowStateJson);
    }

    [Fact]
    public async Task CommitFlowAsync_CommitsAll5Steps_CalculatesGpaAndActivities_ClearsDraft()
    {
        var repo = new InMemoryProfileFlowRepository();
        var storage = new FakeFileStorageService();
        var service = new ProfileFlowService(repo, storage);
        var userId = Guid.NewGuid();

        // 1. Tải bảng điểm nháp
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await service.UploadDraftTranscriptAsync(userId, stream, "transcript.pdf", "application/pdf", 3);

        // 2. Chuẩn bị request commit hoàn thiện cả 5 bước
        var request = new CommitProfileFlowRequest(
            StepData: new ProfileFlowStepDataDto(
                Step1: new Step1GeneralDto(
                    TargetLevel: StudyLevels.Undergraduate,
                    CurrentEducationLevel: "high_school",
                    CurrentSchool: "THPT Chuyên KHTN",
                    CurrentGrade: "Lớp 12",
                    GraduationYear: 2026,
                    IntendedMajor: "Computer Science",
                    PreferredStates: ["CA", "WA"],
                    InstitutionType: "university"
                ),
                Step2: new Step2TranscriptDto(
                    EducationSystem: "standard",
                    GradeScale: "10",
                    CalculatedGpa4: 3.85m,
                    Terms: [
                        new FlowTranscriptTermDto(
                            TermOrder: 1,
                            TermName: "Lớp 10 HK1",
                            Scores: [
                                new FlowTranscriptSubjectScoreDto("Toán", 9.0m, "9.0", 1m),
                                new FlowTranscriptSubjectScoreDto("Tin học", 9.5m, "9.5", 1m)
                            ]
                        )
                    ]
                ),
                Step3: new Step3CertificationsDto(
                    EnglishTestType: "ielts",
                    Ielts: 7.5m,
                    Toefl: null,
                    Duolingo: null,
                    EnglishExamDate: "2025-05-15",
                    SelectedStandardizedTests: ["sat"],
                    Sat: 1450,
                    Act: null,
                    Ap: null,
                    Gre: null,
                    Gmat: null,
                    OtherTestsJson: null
                ),
                Step4: new Step4FinancialDto(
                    BudgetTierId: "tier_35k_50k",
                    AnnualBudgetUsd: 45000m,
                    ScholarshipNeedId: "preferred",
                    FundingSourceId: "family_support",
                    FinancialNotes: "Gia đình có sổ tiết kiệm chứng minh tài chính"
                ),
                Step5: new Step5ActivitiesDto(
                    Items: [
                        new FlowActivityItemDto(
                            Id: null,
                            Type: "award",
                            Title: "Giải Nhì HSG Quốc Gia Tin Học",
                            Role: "Thí sinh",
                            Organization: "Bộ GD&ĐT",
                            ImpactLevel: 3, // Quốc gia
                            DurationMonths: 12,
                            Year: "2025",
                            Description: "Thi lập trình thuật toán C++"
                        ),
                        new FlowActivityItemDto(
                            Id: null,
                            Type: "activity",
                            Title: "Chủ nhiệm CLB Robotics",
                            Role: "Trưởng ban tổ chức",
                            Organization: "THPT KHTN",
                            ImpactLevel: 1, // Trường
                            DurationMonths: 18,
                            Year: "2024",
                            Description: "Dạy lập trình cơ bản cho đàn em"
                        )
                    ]
                )
            )
        );

        // 3. Thực hiện commit
        var response = await service.CommitFlowAsync(userId, request);

        Assert.True(response.Succeeded);
        Assert.Equal(StudyLevels.Undergraduate, response.TargetLevel);
        Assert.Equal("Computer Science", response.IntendedMajor);
        Assert.Equal(3.85m, response.OverallGpa);
        Assert.True(response.ExtracurricularScore > 0);
        Assert.Equal(45000m, response.AnnualBudgetUsd);
        Assert.Contains("/uploads/transcripts/", response.TranscriptFileUrl);

        // 4. Kiểm tra dữ liệu được ghi vào StudentProfile
        var profile = await repo.GetProfileByUserIdAsync(userId);
        Assert.NotNull(profile);
        Assert.Null(profile!.DraftFlowStateJson); // Nháp phải bị xóa sạch sau khi commit
        Assert.Equal(7.5m, profile.Ielts);
        Assert.Equal(1450, profile.Sat);
        Assert.True(profile.NeedsScholarship);
        Assert.Contains("/uploads/transcripts/", profile.TranscriptFileUrl);

        // Kiểm tra transcript scores & activities được lưu vào DB
        var savedScores = await repo.GetTranscriptScoresAsync(profile.Id);
        Assert.Equal(2, savedScores.Count);

        var savedActivities = await repo.GetProfileActivitiesAsync(profile.Id);
        Assert.Equal(2, savedActivities.Count);

        Assert.True(storage.CommitCalled);
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
