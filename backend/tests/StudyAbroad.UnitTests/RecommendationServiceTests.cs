using StudyAbroad.Application.Grading;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class RecommendationServiceTests
{
    // Repository giả chạy trong bộ nhớ: test service mà không cần database
    private sealed class FakeRepository : IRecommendationRepository
    {
        public StudentProfile? Profile { get; set; }
        public string? SettingsJson { get; set; }
        public string? AnalysisJson { get; set; }
        public List<SchoolCandidate> Candidates { get; } = [];
        public List<Recommendation> Saved { get; } = [];

        public Task<StudentProfile?> GetProfileAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Profile?.UserId == userId ? Profile : null);

        public Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(SettingsJson);

        public Task<IReadOnlyList<SchoolCandidate>> GetSchoolCandidatesAsync(string studyLevel, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchoolCandidate>>(Candidates);

        // Trả kết quả phân tích chỉ khi gọi đúng ID HỒ SƠ (bắt lỗi truyền nhầm userId)
        public Task<string?> GetLatestAnalysisJsonAsync(Guid studentProfileId, CancellationToken ct = default) =>
            Task.FromResult(Profile?.Id == studentProfileId ? AnalysisJson : null);

        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        {
            Saved.Add(recommendation);
            return Task.CompletedTask;
        }

        public Task<Recommendation?> GetLatestRecommendationAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Saved.LastOrDefault(r => r.UserId == userId));
    }

    private static readonly Guid UserId = Guid.NewGuid();

    private static StudentProfile NewProfile(string scale = "4", decimal gpa = 3.5m) => new()
    {
        UserId = UserId,
        TargetLevel = "undergraduate",
        GradeScale = scale,
        OverallGpa = gpa,
        IntendedMajor = "Computer Science",
        Sat = 1300,
        AnnualBudgetUsd = 50000,
    };

    private static FakeRepository NewRepo(StudentProfile? profile = null)
    {
        var repo = new FakeRepository { Profile = profile ?? NewProfile() };
        repo.Candidates.Add(new(Guid.NewGuid(), Guid.NewGuid(), "DEMO_UB", "Demo B", "NY",
            ["Computer Science"], 3.50m, 1250, 1400, 35000, 14000, 1000));   // Match
        repo.Candidates.Add(new(Guid.NewGuid(), Guid.NewGuid(), "DEMO_UC", "Demo C", "TX",
            ["Computer Science"], 3.10m, 1050, 1200, 22000, 12000, 1000));   // Safety
        return repo;
    }

    private static RecommendationService NewService(FakeRepository repo) => new(repo, new Gpa4OnlyConverter());

    [Fact]
    public async Task Create_NoProfile_ReturnsNullAndSavesNothing()
    {
        var repo = NewRepo();
        repo.Profile = null;

        Assert.Null(await NewService(repo).CreateAsync(UserId));
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task Create_ReturnsItemsAndSaves()
    {
        var repo = NewRepo();
        var result = await NewService(repo).CreateAsync(UserId);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal([1, 2], result.Items.Select(i => i.Rank));
        Assert.All(result.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Reason)));   // trường nào cũng có giải thích
        Assert.All(result.Items, i => Assert.False(i.AiExplained));

        var saved = Assert.Single(repo.Saved);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(repo.Profile!.Id, saved.StudentProfileId);
        Assert.Equal(RecommendationService.AlgorithmVersion, saved.AlgorithmVersion);
        Assert.Contains("DEMO_UB", saved.ItemsJson);
        Assert.Equal(result.Id, saved.Id);
    }

    [Fact]
    public async Task Create_CategoriesAsExpected()
    {
        var result = await NewService(NewRepo()).CreateAsync(UserId);
        Assert.Equal("match", result!.Items.Single(i => i.Code == "DEMO_UB").Category);
        Assert.Equal("safety", result.Items.Single(i => i.Code == "DEMO_UC").Category);
    }

    [Fact]
    public async Task Create_Gpa10Scale_WarnsButStillRecommends()
    {
        var result = await NewService(NewRepo(NewProfile(scale: "10", gpa: 8.5m))).CreateAsync(UserId);

        Assert.Contains(result!.Warnings, w => w.Contains("GPA"));
        Assert.NotEmpty(result.Items);                                   // vẫn xếp được nhờ SAT
    }

    [Fact]
    public async Task Create_MissingData_AddsWarnings()
    {
        var result = await NewService(NewRepo()).CreateAsync(UserId);    // không có phân tích, không có điểm tiếng Anh

        Assert.Contains(result!.Warnings, w => w.Contains("ngoại khóa"));
        Assert.Contains(result.Warnings, w => w.Contains("tiếng Anh"));
    }

    [Fact]
    public async Task Create_ReadsExtracurricularFromAnalysis()
    {
        var repo = NewRepo();
        repo.AnalysisJson = """{"extracurricularScore": 8}""";
        var result = await NewService(repo).CreateAsync(UserId);

        Assert.DoesNotContain(result!.Warnings, w => w.Contains("ngoại khóa"));
        Assert.Contains("\"extracurricularScore\":8", repo.Saved.Single().CriteriaJson);
    }

    [Fact]
    public async Task Create_NoMatchingSchool_WarnsWithEmptyList()
    {
        var profile = NewProfile();
        profile.IntendedMajor = "Nursing";
        var result = await NewService(NewRepo(profile)).CreateAsync(UserId);

        Assert.Empty(result!.Items);
        Assert.Contains(result.Warnings, w => w.Contains("Không có trường nào"));
    }

    [Fact]
    public async Task Create_SettingsFromDatabase_AreUsed()            // tiêu chí: đổi ngưỡng → kết quả đổi
    {
        var repo = NewRepo();
        var before = await NewService(repo).CreateAsync(UserId);
        repo.SettingsJson = """{"gpaBand": 0.5}""";                       // nới ngưỡng GPA
        var after = await NewService(repo).CreateAsync(UserId);

        Assert.Equal("safety", before!.Items.Single(i => i.Code == "DEMO_UC").Category);   // GPA hơn 0.4 ≥ 0.3
        Assert.Equal("match", after!.Items.Single(i => i.Code == "DEMO_UC").Category);     // 0.4 < 0.5
    }

    [Fact]
    public async Task Create_SameProfile_SameResult()                  // tiêu chí: chạy lại cho cùng kết quả
    {
        var repo = NewRepo();
        var first = await NewService(repo).CreateAsync(UserId);
        var second = await NewService(repo).CreateAsync(UserId);

        Assert.Equal(first!.Items.Select(i => (i.Code, i.Category)), second!.Items.Select(i => (i.Code, i.Category)));
    }

    [Fact]
    public async Task GetLatest_ReturnsSavedItemsInSameOrder()
    {
        var repo = NewRepo();
        var service = NewService(repo);
        var created = await service.CreateAsync(UserId);
        var latest = await service.GetLatestAsync(UserId);

        Assert.Equal(created!.Items.Select(i => i.Code), latest!.Items.Select(i => i.Code));
        Assert.Equal(created.Items.Select(i => i.Reason), latest.Items.Select(i => i.Reason));
        Assert.Equal(created.Id, latest.Id);
    }

    [Fact]
    public async Task GetLatest_NoRecommendation_ReturnsNull() =>
        Assert.Null(await NewService(NewRepo()).GetLatestAsync(UserId));

    [Fact]
    public void ParseExtracurricular_ReadsScore() =>
        Assert.Equal(7.5m, RecommendationService.ParseExtracurricular("""{"extracurricularScore": 7.5, "summary": "..."}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]                                    // thiếu key
    [InlineData("""{"extracurricularScore": "tám"}""")]   // không phải số
    [InlineData("[1, 2]")]                                // không phải object
    [InlineData("không phải json")]                       // JSON hỏng
    public void ParseExtracurricular_InvalidInput_ReturnsNull(string? json) =>
        Assert.Null(RecommendationService.ParseExtracurricular(json));
}
