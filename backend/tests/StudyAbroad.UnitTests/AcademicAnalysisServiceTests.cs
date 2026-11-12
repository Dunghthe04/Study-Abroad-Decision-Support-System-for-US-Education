using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-365] Bộ Unit Test kiểm thử toàn diện thuật toán quy đổi GPA thang 4.0,
/// tính Weighted GPA, phân nhóm môn học, phân tích xu hướng học tập và bảo mật Anti-IDOR.
/// </summary>
public class AcademicAnalysisServiceTests
{
    private readonly FakeAcademicAnalysisRepository _repository;
    private readonly AcademicAnalysisService _service;
    private readonly Guid _testUserId = Guid.NewGuid();

    public AcademicAnalysisServiceTests()
    {
        _repository = new FakeAcademicAnalysisRepository();
        _service = new AcademicAnalysisService(_repository);
    }

    [Theory]
    [InlineData(10.0, 4.00)]
    [InlineData(9.5, 3.85)]   // đúng mốc VEF
    [InlineData(9.0, 3.75)]
    [InlineData(8.6, 3.65)]
    [InlineData(8.5, 3.625)]  // giữa 8.4 → 3.60 và 8.6 → 3.65
    [InlineData(8.2, 3.55)]
    [InlineData(8.0, 3.50)]
    [InlineData(7.5, 3.25)]   // giữa 7.4 → 3.20 và 7.6 → 3.30
    [InlineData(7.0, 3.00)]
    [InlineData(6.8, 2.90)]   // giữa 5 → 2.0 và 7 → 3.0
    [InlineData(6.0, 2.50)]
    [InlineData(5.2, 2.10)]
    [InlineData(4.5, 1.80)]   // giữa 0 → 0 và 5 → 2.0
    [InlineData(3.0, 1.20)]
    [InlineData(0.0, 0.0)]
    public void ConvertScoreToGpa4_DefaultVef_InterpolatesBetweenAnchors(decimal rawScore, decimal expectedGpa)
    {
        // [USAS-365] Bảng mặc định: mốc VEF + nội suy tuyến tính (kế hoạch GPA weighted, mục 4)
        var vefConfig = AcademicAnalysisService.GetDefaultScaleConfig();
        var result = AcademicAnalysisService.ConvertScoreToGpa4(rawScore, vefConfig);
        Assert.Equal(expectedGpa, result);
    }

    [Theory]
    [InlineData(8.995, 3.74875)] // sát mốc 9.0 thì sát 3.75, không còn nhảy bậc như bảng tra cũ
    [InlineData(4.995, 1.998)]   // sát mốc 5.0 thì sát 2.0
    public void ConvertScoreToGpa4_NearAnchor_NoStepJump(decimal rawScore, decimal expectedGpa)
    {
        var vefConfig = AcademicAnalysisService.GetDefaultScaleConfig();
        Assert.Equal(expectedGpa, AcademicAnalysisService.ConvertScoreToGpa4(rawScore, vefConfig));
    }

    [Fact]
    public void ConvertScoreToGpa4_SameAsTranscriptGpaTo4()
    {
        // Trang phân tích học bạ và GPA lưu trong hồ sơ dùng cùng một cách đổi
        var vefConfig = AcademicAnalysisService.GetDefaultScaleConfig();
        for (var score = 0m; score <= 10m; score += 0.005m)
            Assert.Equal(TranscriptGpa.To4(score), AcademicAnalysisService.ConvertScoreToGpa4(score, vefConfig));
    }

    [Fact]
    public void ConvertScoreToGpa4_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var vefConfig = AcademicAnalysisService.GetDefaultScaleConfig();

        Assert.Throws<ArgumentOutOfRangeException>(() => AcademicAnalysisService.ConvertScoreToGpa4(-0.5m, vefConfig));
        Assert.Throws<ArgumentOutOfRangeException>(() => AcademicAnalysisService.ConvertScoreToGpa4(10.5m, vefConfig));
    }

    [Theory]
    [InlineData("Toán", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Toán Nâng cao", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Vật lý", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Hóa học", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Sinh học", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Tin học", AcademicConstants.GroupNaturalSciences)]
    [InlineData("Công nghệ", AcademicConstants.GroupNaturalSciences)]
    public void ClassifySubjectGroup_NaturalSciences_IdentifiesCorrectly(string subject, string expectedGroup)
    {
        var group = AcademicAnalysisService.ClassifySubjectGroup(subject);
        Assert.Equal(expectedGroup, group);
    }

    [Theory]
    [InlineData("Ngữ văn", AcademicConstants.GroupSocialSciences)]
    [InlineData("Văn học", AcademicConstants.GroupSocialSciences)]
    [InlineData("Lịch sử", AcademicConstants.GroupSocialSciences)]
    [InlineData("Địa lý", AcademicConstants.GroupSocialSciences)]
    [InlineData("Giáo dục công dân", AcademicConstants.GroupSocialSciences)]
    [InlineData("GDCD", AcademicConstants.GroupSocialSciences)]
    public void ClassifySubjectGroup_SocialSciences_IdentifiesCorrectly(string subject, string expectedGroup)
    {
        var group = AcademicAnalysisService.ClassifySubjectGroup(subject);
        Assert.Equal(expectedGroup, group);
    }

    [Theory]
    [InlineData("Tiếng Anh", AcademicConstants.GroupLanguages)]
    [InlineData("English", AcademicConstants.GroupLanguages)]
    [InlineData("Tiếng Pháp", AcademicConstants.GroupLanguages)]
    [InlineData("Tiếng Trung", AcademicConstants.GroupLanguages)]
    [InlineData("Tiếng Nhật", AcademicConstants.GroupLanguages)]
    public void ClassifySubjectGroup_Languages_IdentifiesCorrectly(string subject, string expectedGroup)
    {
        var group = AcademicAnalysisService.ClassifySubjectGroup(subject);
        Assert.Equal(expectedGroup, group);
    }

    [Fact]
    public void EvaluateTrend_UpwardTrend_DetectsGrowthMindset()
    {
        var terms = new List<TermTrendDto>
        {
            new(1, "Lớp 10 HK1", 7.0m, 3.0m, 5),
            new(2, "Lớp 10 HK2", 7.5m, 3.2m, 5),
            new(3, "Lớp 11 HK1", 8.2m, 3.5m, 5),
            new(4, "Lớp 11 HK2", 9.0m, 4.0m, 5)
        };

        var (trend, desc) = AcademicAnalysisService.EvaluateTrend(terms);
        Assert.Equal(AcademicConstants.TrendUpward, trend);
        Assert.Contains("tiến bộ", desc);
    }

    [Fact]
    public void EvaluateTrend_DownwardTrend_DetectsDrop()
    {
        var terms = new List<TermTrendDto>
        {
            new(1, "Lớp 10 HK1", 9.0m, 4.0m, 5),
            new(2, "Lớp 10 HK2", 8.5m, 3.7m, 5),
            new(3, "Lớp 11 HK1", 7.5m, 3.0m, 5),
            new(4, "Lớp 11 HK2", 7.0m, 2.5m, 5)
        };

        var (trend, desc) = AcademicAnalysisService.EvaluateTrend(terms);
        Assert.Equal(AcademicConstants.TrendDownward, trend);
        Assert.Contains("giảm nhẹ", desc);
    }

    [Fact]
    public void EvaluateTrend_ConsistentTrend_DetectsStability()
    {
        var terms = new List<TermTrendDto>
        {
            new(1, "Lớp 10 HK1", 8.2m, 3.5m, 5),
            new(2, "Lớp 10 HK2", 8.3m, 3.5m, 5),
            new(3, "Lớp 11 HK1", 8.2m, 3.5m, 5),
            new(4, "Lớp 11 HK2", 8.4m, 3.5m, 5)
        };

        var (trend, desc) = AcademicAnalysisService.EvaluateTrend(terms);
        Assert.Equal(AcademicConstants.TrendConsistent, trend);
        Assert.Contains("ổn định", desc);
    }

    [Fact]
    public void EvaluateTrend_SingleTerm_ReturnsConsistentWithNotice()
    {
        var terms = new List<TermTrendDto>
        {
            new(1, "Lớp 10 HK1", 8.0m, 3.5m, 5)
        };

        var (trend, desc) = AcademicAnalysisService.EvaluateTrend(terms);
        Assert.Equal(AcademicConstants.TrendConsistent, trend);
        Assert.Contains("Chưa đủ dữ liệu", desc);
    }

    [Fact]
    public async Task AnalyzeAcademicProfileAsync_WithScores_ComputesGpaAndGroupsCorrectly()
    {
        // 1. Tạo bảng điểm cho học sinh
        var request = new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, "Lớp 10 HK1", 1, "Toán", 9.0m, 2m),       // STEM
            new(null, "Lớp 10 HK1", 1, "Vật lý", 8.5m, 2m),     // STEM
            new(null, "Lớp 10 HK1", 1, "Ngữ văn", 8.0m, 2m),    // Xã hội
            new(null, "Lớp 10 HK1", 1, "Tiếng Anh", 9.5m, 3m),  // Ngoại ngữ
            new(null, "Lớp 10 HK2", 2, "Toán", 9.5m, 2m),       // STEM
            new(null, "Lớp 10 HK2", 2, "Vật lý", 9.0m, 2m),     // STEM
            new(null, "Lớp 10 HK2", 2, "Ngữ văn", 8.5m, 2m),    // Xã hội
            new(null, "Lớp 10 HK2", 2, "Tiếng Anh", 9.0m, 3m),  // Ngoại ngữ
        });

        await _service.SaveScoresAsync(_testUserId, request);

        // 2. Chạy thuật toán phân tích
        var result = await _service.AnalyzeAcademicProfileAsync(_testUserId);

        // 3. Kiểm chứng
        Assert.NotNull(result);
        Assert.Equal(3.72m, result.UnweightedGpa); // TB hệ 10 là 8.875 → 3.71875 → 3.72
        Assert.True(result.WeightedGpa >= result.UnweightedGpa);
        Assert.Equal(8, result.TotalSubjects);
        Assert.Equal(2, result.TotalTerms);
        Assert.Equal(3, result.SubjectGroups.Count); // STEM, Xã hội, Ngoại ngữ
        Assert.NotEmpty(result.Disclaimer);

        // Kiểm tra lưu kết quả vào analysis_results và cập nhật overall_gpa
        var latest = await _service.GetLatestAnalysisAsync(_testUserId);
        Assert.NotNull(latest);
        Assert.Equal(result.UnweightedGpa, latest.UnweightedGpa);
    }

    // Toán 7.0, Tiếng Anh 10: TB hệ 10 là 8.5 → 3.625 → 3.63. Đổi từng môn rồi lấy TB (3.0 và 4.0) sẽ ra 3.50, sai
    [Fact]
    public async Task AnalyzeAcademicProfileAsync_ConvertsAverageOnce_NotEachSubject()
    {
        await _service.SaveScoresAsync(_testUserId, new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, "Lớp 10 HK1", 1, "Toán", 7.0m, null),
            new(null, "Lớp 10 HK1", 1, "Tiếng Anh", 10m, null),
        }));

        var result = await _service.AnalyzeAcademicProfileAsync(_testUserId);

        Assert.Equal(8.5m, result.RawAverage);
        Assert.Equal(3.63m, result.UnweightedGpa);
        Assert.Equal(3.63m, Assert.Single(result.TermAverages).Gpa4);
        var profile = await _repository.GetProfileByUserIdAsync(_testUserId);
        Assert.Equal(3.63m, profile!.OverallGpa); // overall_gpa trong hồ sơ cũng là số đổi một lần
    }

    // Nhóm môn và học kỳ cũng lấy TB hệ 10 của nhóm/kỳ rồi đổi một lần
    [Fact]
    public async Task AnalyzeAcademicProfileAsync_GroupsAndTerms_ConvertAverageOnce()
    {
        await _service.SaveScoresAsync(_testUserId, new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, "Lớp 10 HK1", 1, "Toán", 7.0m, null),     // STEM
            new(null, "Lớp 10 HK1", 1, "Vật lý", 10m, null),    // STEM
            new(null, "Lớp 10 HK2", 2, "Ngữ văn", 6.0m, null),  // Xã hội
            new(null, "Lớp 10 HK2", 2, "Lịch sử", 8.0m, null),  // Xã hội
        }));

        var result = await _service.AnalyzeAcademicProfileAsync(_testUserId);

        var stem = result.SubjectGroups.Single(g => g.GroupKey == AcademicConstants.GroupNaturalSciences);
        var social = result.SubjectGroups.Single(g => g.GroupKey == AcademicConstants.GroupSocialSciences);
        Assert.Equal(3.63m, stem.Gpa4);   // TB 8.5 → 3.625
        Assert.Equal(3.00m, social.Gpa4); // TB 7.0 → 3.00 (đổi từng môn: 2.5 và 3.5 → 3.00, trùng vì đoạn này thẳng)
        Assert.Equal([3.63m, 3.00m], result.TermAverages.OrderBy(t => t.TermOrder).Select(t => t.Gpa4));
        Assert.Equal(3.38m, result.UnweightedGpa); // TB 7.75 → 3.375 → 3.38
    }

    [Fact]
    public async Task AnalyzeAcademicProfileAsync_EmptyScores_ReturnsZeroGpaAndPrompt()
    {
        var result = await _service.AnalyzeAcademicProfileAsync(_testUserId);

        Assert.Equal(0m, result.UnweightedGpa);
        Assert.Equal(0, result.TotalSubjects);
        Assert.Contains("Chưa có dữ liệu", result.TrendDescription);
    }

    [Theory]
    [InlineData(-1.0, "Toán", "Lớp 10 HK1")]
    [InlineData(10.5, "Toán", "Lớp 10 HK1")]
    [InlineData(8.0, "", "Lớp 10 HK1")]
    [InlineData(8.0, "Toán", "")]
    public async Task SaveScoresAsync_InvalidInput_ThrowsArgumentException(decimal score, string subject, string term)
    {
        var request = new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, term, 1, subject, score, null)
        });

        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveScoresAsync(_testUserId, request));
    }

    [Fact]
    public async Task SaveScoresAsync_AntiIdor_OnlyModifiesOwnProfile()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await _service.SaveScoresAsync(user1, new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, "Lớp 10 HK1", 1, "Toán", 9.0m, null)
        }));

        var user2Scores = await _service.GetScoresAsync(user2);
        Assert.Empty(user2Scores);

        var user1Scores = await _service.GetScoresAsync(user1);
        Assert.Single(user1Scores);
    }

    [Fact]
    public async Task DeleteScoreAsync_DeletesSuccessfully()
    {
        var request = new BatchTranscriptScoresRequest(new List<UpsertTranscriptScoreItem>
        {
            new(null, "Lớp 10 HK1", 1, "Toán", 9.0m, null),
            new(null, "Lớp 10 HK1", 1, "Vật lý", 8.0m, null)
        });

        var scores = await _service.SaveScoresAsync(_testUserId, request);
        Assert.Equal(2, scores.Count);

        var scoreToDelete = scores.First();
        await _service.DeleteScoreAsync(_testUserId, scoreToDelete.Id);

        var remaining = await _service.GetScoresAsync(_testUserId);
        Assert.Single(remaining);
        Assert.Equal("Vật lý", remaining[0].Subject);
    }
}

/// <summary>
/// Fake In-Memory Repository phục vụ kiểm thử đơn vị cho IAcademicAnalysisRepository.
/// </summary>
internal class FakeAcademicAnalysisRepository : IAcademicAnalysisRepository
{
    private readonly Dictionary<Guid, StudentProfile> _profiles = new();
    private readonly List<TranscriptScore> _scores = new();
    private readonly List<AnalysisResult> _analysisResults = new();
    private readonly Dictionary<string, AppSetting> _settings = new();

    /// <summary>Đặt một dòng app_settings giả (dùng cho test bảng quy đổi grade_scale.10).</summary>
    public void SetSetting(string key, string valueJson) =>
        _settings[key] = new AppSetting { Key = key, ValueJson = valueJson };

    public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = _profiles.Values.FirstOrDefault(p => p.UserId == userId);
        return Task.FromResult(profile);
    }

    public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = _profiles.Values.FirstOrDefault(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GradeScale = "10"
            };
            _profiles[profile.Id] = profile;
        }
        return Task.FromResult(profile);
    }

    public Task<IReadOnlyList<TranscriptScore>> GetScoresByProfileIdAsync(Guid profileId, CancellationToken ct = default)
    {
        IReadOnlyList<TranscriptScore> list = _scores
            .Where(s => s.StudentProfileId == profileId)
            .OrderBy(s => s.TermOrder)
            .ThenBy(s => s.Subject)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<TranscriptScore>> SaveScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
    {
        foreach (var score in scores)
        {
            var existing = _scores.FirstOrDefault(s => s.Id == score.Id && s.StudentProfileId == profileId);
            if (existing != null)
            {
                existing.TermName = score.TermName;
                existing.TermOrder = score.TermOrder;
                existing.Subject = score.Subject;
                existing.Score = score.Score;
                existing.Credits = score.Credits;
            }
            else
            {
                score.StudentProfileId = profileId;
                _scores.Add(score);
            }
        }
        return GetScoresByProfileIdAsync(profileId, ct);
    }

    public Task DeleteScoresAsync(Guid profileId, IEnumerable<Guid> scoreIds, CancellationToken ct = default)
    {
        _scores.RemoveAll(s => s.StudentProfileId == profileId && scoreIds.Contains(s.Id));
        return Task.CompletedTask;
    }

    public Task<AppSetting?> GetAppSettingByKeyAsync(string key, CancellationToken ct = default)
    {
        _settings.TryGetValue(key, out var val);
        return Task.FromResult(val);
    }

    public Task<AnalysisResult> SaveAnalysisResultAsync(AnalysisResult result, decimal overallGpa, CancellationToken ct = default)
    {
        _analysisResults.Add(result);
        if (_profiles.TryGetValue(result.StudentProfileId, out var profile))
        {
            profile.OverallGpa = overallGpa;
        }
        return Task.FromResult(result);
    }

    public Task<AnalysisResult?> GetLatestAnalysisAsync(Guid profileId, CancellationToken ct = default)
    {
        var latest = _analysisResults
            .Where(a => a.StudentProfileId == profileId && a.Kind == AcademicConstants.KindGpa)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(latest);
    }
}
