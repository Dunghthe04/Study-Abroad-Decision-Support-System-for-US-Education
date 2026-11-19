using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AcademicProfileServiceTests
{
    private sealed class FakeAcademicProfileRepository : IAcademicProfileRepository
    {
        public readonly Dictionary<Guid, StudentProfile> Profiles = new();
        public readonly Dictionary<Guid, List<TranscriptScore>> Scores = new();
        public readonly Dictionary<Guid, List<ProfileCertification>> Certifications = new();

        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = Profiles.Values.FirstOrDefault(p => p.UserId == userId);
            return Task.FromResult(profile);
        }

        public Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default)
        {
            if (Scores.TryGetValue(profileId, out var list))
            {
                return Task.FromResult(list.ToList());
            }

            return Task.FromResult(new List<TranscriptScore>());
        }

        public Task<List<ProfileCertification>> GetCertificationsAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult(Certifications.TryGetValue(profileId, out var list) ? list.ToList() : new List<ProfileCertification>());
    }

    [Fact]
    public async Task GetAcademicProfileAsync_ProfileExists_ReturnsCompleteDto()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var profile = new StudentProfile
        {
            Id = profileId,
            UserId = userId,
            TargetLevel = StudyLevels.Master,
            CurrentSchool = "Đại học Bách Khoa",
            EducationSystem = EducationSystems.Standard,
            GraduationYear = 2026,
            CurrentGrade = "Năm 4",
            GradeScale = GradeScales.Scale4,
            OverallGpa = 3.65m,
            IntendedMajor = "Trí tuệ nhân tạo",
            CreatedAt = DateTime.UtcNow
        };
        repo.Profiles[profile.Id] = profile;

        var scores = new List<TranscriptScore>
        {
            new() { Id = Guid.NewGuid(), StudentProfileId = profileId, TermName = "Năm 3 - Kỳ 1", TermOrder = 1, Subject = "Học máy", Score = 3.7m, Credits = 3 },
            new() { Id = Guid.NewGuid(), StudentProfileId = profileId, TermName = "Năm 3 - Kỳ 1", TermOrder = 1, Subject = "Thị giác máy tính", Score = 4.0m, Credits = 3 }
        };
        repo.Scores[profileId] = scores;

        var result = await service.GetAcademicProfileAsync(userId);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Đại học Bách Khoa", result.Data.CurrentSchool);
        Assert.Equal(3.65m, result.Data.OverallGpa);
        Assert.Single(result.Data.Terms);
        Assert.Equal(2, result.Data.Terms[0].Scores.Count);
        Assert.True(result.Data.IsComplete);
    }

    // Luồng 6 bước chế độ GPA tổng: không có trường, không có điểm theo kỳ vẫn là đã hoàn thành
    [Fact]
    public async Task GetAcademicProfileAsync_FlowSubmittedWithOverallGpaOnly_IsComplete()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        repo.Profiles[profileId] = new StudentProfile
        {
            Id = profileId,
            UserId = userId,
            OverallGpa = 8.2m,
            FlowSubmittedAt = DateTimeOffset.UtcNow,
        };

        var result = await service.GetAcademicProfileAsync(userId);

        Assert.True(result.Data!.IsComplete);
        Assert.Null(result.Data.CurrentSchool);
        Assert.Empty(result.Data.Terms);
    }

    [Fact]
    public async Task GetAcademicProfileAsync_TestScores_ReadFromCertificationsTakenOnly()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        repo.Profiles[profileId] = new StudentProfile { Id = profileId, UserId = userId };
        repo.Certifications[profileId] =
        [
            new() { StudentProfileId = profileId, Category = "english", TestType = "ielts", OverallScore = 6.5m },
            new() { StudentProfileId = profileId, Category = "english", TestType = "ielts", OverallScore = 7.0m },
            // dòng mục tiêu (chưa thi) không tính là điểm
            new() { StudentProfileId = profileId, Category = "english", TestType = "ielts", OverallScore = 8.0m, IsTarget = true },
            new() { StudentProfileId = profileId, Category = "standardized", TestType = "sat", OverallScore = 1400m, IsTarget = true },
            new() { StudentProfileId = profileId, Category = "standardized", TestType = "ap", Subject = "Calculus BC", OverallScore = 5m },
        ];

        var result = await service.GetAcademicProfileAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Equal(7.0m, result.Data!.Ielts);
        Assert.Null(result.Data.Sat);
        Assert.Null(result.Data.Toefl);
        Assert.Equal("{\"ap\":5}", result.Data.OtherTestsJson);
        Assert.False(result.Data.IsComplete);
    }

    [Fact]
    public async Task GetAcademicProfileAsync_ProfileDoesNotExist_ReturnsDefaultEmptyProfile()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var result = await service.GetAcademicProfileAsync(userId);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserId);
        Assert.Equal(StudyLevels.Undergraduate, result.Data.TargetLevel);
        Assert.Equal(GradeScales.Scale10, result.Data.GradeScale);
        Assert.Empty(result.Data.Terms);
        Assert.False(result.Data.IsComplete);
    }

    // Chỉ GetCommittedDetailsAsync được gọi; các hàm khác không dùng trong test này
    private sealed class FakeProfileFlowService(ProfileFlowDraftDto? details) : IProfileFlowService
    {
        public Guid? AskedUserId { get; private set; }

        public Task<ProfileFlowDraftDto?> GetCommittedDetailsAsync(Guid userId, CancellationToken ct = default)
        {
            AskedUserId = userId;
            return Task.FromResult(details);
        }

        public Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ProfileFlowStateDto> PatchDraftAsync(Guid userId, System.Text.Json.Nodes.JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(Guid userId, System.IO.Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearDraftAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task GetAcademicProfileAsync_WithProfileFlow_FillsDetailsOfSameUser()
    {
        var repo = new FakeAcademicProfileRepository();
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        repo.Profiles[profileId] = new StudentProfile { Id = profileId, UserId = userId, TargetLevel = StudyLevels.Undergraduate };
        var details = new ProfileFlowDraftDto(Step4: new FlowFinancialDraftDto("tier_20k_35k", "preferred", "parents"));
        var flow = new FakeProfileFlowService(details);
        var service = new AcademicProfileService(repo, flow);

        var result = await service.GetAcademicProfileAsync(userId);

        Assert.Equal(userId, flow.AskedUserId);
        Assert.Same(details, result.Data!.Details);
    }
}
