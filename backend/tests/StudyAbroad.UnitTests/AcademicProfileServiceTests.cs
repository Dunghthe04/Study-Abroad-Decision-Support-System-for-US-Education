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

        public Task AddProfileAsync(StudentProfile profile, CancellationToken ct = default)
        {
            Profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
        {
            Profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default)
        {
            if (Scores.TryGetValue(profileId, out var list))
            {
                return Task.FromResult(list.ToList());
            }

            return Task.FromResult(new List<TranscriptScore>());
        }

        public Task ReplaceTranscriptScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
        {
            Scores[profileId] = scores.ToList();
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_ValidData_SavesSuccessfullyAndCalculatesGpa()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "THPT Chuyên Hà Nội - Amsterdam",
            EducationSystem: EducationSystems.Specialized,
            GraduationYear: 2027,
            CurrentGrade: "Lớp 11",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Khoa học Máy tính",
            Ielts: 7.5m,
            Toefl: null,
            Duolingo: null,
            Sat: 1480,
            Act: null,
            Terms:
            [
                new TranscriptTermDto(
                    "Lớp 10 - Học kỳ 1",
                    1,
                    [
                        new TranscriptScoreItemDto("Toán học", null, 9.0m, 2),
                        new TranscriptScoreItemDto("Vật lý", null, 8.0m, 2)
                    ]
                )
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(8.5m, result.Data.OverallGpa);
        Assert.Equal(7.5m, result.Data.Ielts);
        Assert.Equal(1480, result.Data.Sat);
        Assert.Single(result.Data.Terms);
        Assert.Equal(2, result.Data.Terms[0].Scores.Count);

        // Verify repository data
        var savedProfile = repo.Profiles.Values.First(p => p.UserId == userId);
        Assert.Equal("THPT Chuyên Hà Nội - Amsterdam", savedProfile.CurrentSchool);
        Assert.Equal(8.5m, savedProfile.OverallGpa);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    public async Task SaveAcademicProfileAsync_Scale10_RejectsOutOfRangeScores(decimal invalidScore)
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường A",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 12",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Kinh tế",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto("HK1", 1, [new TranscriptScoreItemDto("Toán", null, invalidScore, 1)])
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("không hợp lệ với Thang 10", result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SaveAcademicProfileAsync_Scale100_RejectsOutOfRangeScores(decimal invalidScore)
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường Quốc tế",
            EducationSystem: EducationSystems.International,
            GraduationYear: 2026,
            CurrentGrade: "Grade 12",
            GradeScale: GradeScales.Scale100,
            IntendedMajor: "Quản trị Kinh doanh",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto("Term 1", 1, [new TranscriptScoreItemDto("Math", null, invalidScore, 1)])
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("không hợp lệ với Thang 100", result.Message);
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_LetterGrade_MapsValidLettersCorrectly()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "American International School",
            EducationSystem: EducationSystems.International,
            GraduationYear: 2026,
            CurrentGrade: "Grade 12",
            GradeScale: GradeScales.ScaleLetter,
            IntendedMajor: "Computer Science",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto(
                    "Fall 2025",
                    1,
                    [
                        new TranscriptScoreItemDto("Math", "A", 0, 3), // quy đổi sang 4.0
                        new TranscriptScoreItemDto("Physics", "B", 0, 3) // quy đổi sang 3.0
                    ]
                )
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        // (4.0*3 + 3.0*3) / 6 = 3.5
        Assert.Equal(3.5m, result.Data.OverallGpa);
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_LetterGrade_RejectsInvalidLetter()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "School",
            EducationSystem: EducationSystems.International,
            GraduationYear: 2026,
            CurrentGrade: "Grade 12",
            GradeScale: GradeScales.ScaleLetter,
            IntendedMajor: "Art",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto(
                    "Fall 2025",
                    1,
                    [
                        new TranscriptScoreItemDto("Math", "Z", 0, 3)
                    ]
                )
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("không hợp lệ", result.Message);
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_DuplicateSubjectInSameTerm_ReturnsValidationError()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường THPT",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 12",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Y khoa",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto(
                    "Học kỳ 1",
                    1,
                    [
                        new TranscriptScoreItemDto("Sinh học", null, 9.0m, 1),
                        new TranscriptScoreItemDto("Sinh học", null, 8.5m, 1)
                    ]
                )
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("bị nhập trùng lặp", result.Message);
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_SameSubjectInDifferentTerms_IsAllowed()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường THPT",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 12",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Toán tin",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms:
            [
                new TranscriptTermDto("Học kỳ 1", 1, [new TranscriptScoreItemDto("Toán học", null, 9.0m, 1)]),
                new TranscriptTermDto("Học kỳ 2", 2, [new TranscriptScoreItemDto("Toán học", null, 9.5m, 1)])
            ]
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Terms.Count);
    }

    [Fact]
    public async Task SaveAcademicProfileAsync_UnattemptedCertificates_CanBeNull()
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        // Học sinh chưa thi bất kỳ chứng chỉ nào (chưa có IELTS/SAT/ACT)
        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường THPT",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 11",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Tài chính",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms: []
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.True(result.Succeeded);
        Assert.Null(result.Data!.Ielts);
        Assert.Null(result.Data!.Sat);
        Assert.Null(result.Data!.Toefl);
        Assert.Null(result.Data!.Duolingo);
        Assert.Null(result.Data!.Act);
    }

    [Theory]
    [InlineData(9.2)] // bước nhảy IELTS phải là 0.5
    [InlineData(9.5)] // quá 9.0
    [InlineData(-0.5)]
    public async Task SaveAcademicProfileAsync_InvalidIeltsScore_Rejects(decimal invalidIelts)
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường THPT",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 12",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Tài chính",
            Ielts: invalidIelts,
            Toefl: null,
            Duolingo: null,
            Sat: null,
            Act: null,
            Terms: []
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("IELTS", result.Message);
    }

    [Theory]
    [InlineData(350)]  // Dưới 400
    [InlineData(1650)] // Trên 1600
    public async Task SaveAcademicProfileAsync_InvalidSatScore_Rejects(decimal invalidSat)
    {
        var repo = new FakeAcademicProfileRepository();
        var service = new AcademicProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveAcademicProfileRequest(
            TargetLevel: StudyLevels.Undergraduate,
            CurrentSchool: "Trường THPT",
            EducationSystem: EducationSystems.Standard,
            GraduationYear: 2026,
            CurrentGrade: "Lớp 12",
            GradeScale: GradeScales.Scale10,
            IntendedMajor: "Tài chính",
            Ielts: null,
            Toefl: null,
            Duolingo: null,
            Sat: invalidSat,
            Act: null,
            Terms: []
        );

        var result = await service.SaveAcademicProfileAsync(userId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(AcademicProfileError.Validation, result.Error);
        Assert.Contains("SAT", result.Message);
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
            GraduationYear = 2025,
            CurrentGrade = "Năm 4",
            GradeScale = GradeScales.Scale4,
            OverallGpa = 3.65m,
            IntendedMajor = "Trí tuệ nhân tạo",
            Ielts = 8.0m,
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddProfileAsync(profile);

        var scores = new List<TranscriptScore>
        {
            new() { Id = Guid.NewGuid(), StudentProfileId = profileId, TermName = "Năm 3 - Kỳ 1", TermOrder = 1, Subject = "Học máy", Score = 3.7m, Credits = 3 },
            new() { Id = Guid.NewGuid(), StudentProfileId = profileId, TermName = "Năm 3 - Kỳ 1", TermOrder = 1, Subject = "Thị giác máy tính", Score = 4.0m, Credits = 3 }
        };
        await repo.ReplaceTranscriptScoresAsync(profileId, scores);

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
