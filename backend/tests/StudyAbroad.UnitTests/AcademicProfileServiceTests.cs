using StudyAbroad.Application.AcademicProfiles;
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
            Ielts = 8.0m,
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
    }
}
