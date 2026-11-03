using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Luồng gợi ý trường: lọc cứng → advisor đọc ngoại khóa → code chấm nhóm và fit score → lưu theo khuôn JSON cũ.
public class RecommendationServiceTests
{
    private const string Major = "Computer and information sciences";

    private static StudentProfile Profile(string? need = ProfileConstants.ScholarshipNeeds.Preferred, string? major = Major) => new()
    {
        UserId = Guid.NewGuid(),
        TargetLevel = "undergraduate",
        IntendedMajor = major,
        BudgetMaxUsd = 50_000m,
        ScholarshipNeed = need,
        OverallGpa = 3.6m,
        Sat = 1350m,
        Ielts = 6.5m,
        TargetSchoolType = "all"
    };

    private static (RecommendationService Service, FakeAdvisor Advisor, FakeRecommendationRepository Store) Setup(
        StudentProfile? profile = null, IReadOnlyList<RecommendSchoolInput>? schools = null)
    {
        var advisor = new FakeAdvisor();
        var store = new FakeRecommendationRepository(schools ?? []);
        return (new RecommendationService(advisor, store, new FakeProfileFlow(profile)), advisor, store);
    }

    // ---------------------------------------------------------------- luồng chính

    [Fact]
    public async Task Recommend_FiltersFirst_GradesByCode_AndOnlyTheEcReadGoesToTheAdvisor()
    {
        var (service, advisor, store) = Setup();
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("Colgate", 95_610m, "need_and_merit", Major, sat: (1450, 1550), accept: 0.08m),   // vượt ngân sách, rất chọn lọc
            School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m),     // vừa sức
            School("D", 20_000m, "none", "History", sat: (1200, 1380), accept: 0.60m)                 // sai ngành
        };

        await service.RecommendAsync(profile, [], schools);

        Assert.Equal(["ec"], advisor.Calls);   // không còn chấm điểm và chọn trường trên cloud

        var saved = Assert.Single(store.Rows);
        Assert.Equal(RecommendationService.AlgorithmVersion, saved.AlgorithmVersion);
        using var criteria = JsonDocument.Parse(saved.CriteriaJson);
        using var items = JsonDocument.Parse(saved.ItemsJson);
        var root = items.RootElement;

        var scored = root.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.False(scored.ContainsKey("D"));
        Assert.Equal("AidBlock", scored["Colgate"].GetProperty("outcome").GetString());
        Assert.Equal("R", scored["Colgate"].GetProperty("g2").GetString());     // tỉ lệ nhận 8% nên luôn Khó vào
        Assert.Equal("M", scored["B"].GetProperty("g2").GetString());

        Assert.Equal("B", root.GetProperty("match")[0].GetProperty("code").GetString());
        Assert.Equal(0, root.GetProperty("reach").GetArrayLength());           // Colgate cần học bổng lớn nên ở khối riêng
        Assert.Equal("Colgate", root.GetProperty("aid")[0].GetProperty("code").GetString());
        var why = root.GetProperty("match")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString()).ToList();
        Assert.Contains("GPA_MID", why);
        Assert.Contains("BUDGET_OK", why);
        Assert.Contains("NEED_AID", root.GetProperty("aid")[0].GetProperty("why").EnumerateArray().Select(w => w.GetString()));

        Assert.Equal(3, criteria.RootElement.GetProperty("tier").GetInt32());
        Assert.Equal("OK", criteria.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Recommend_SavedJsonKeepsTheShapeTheFrontendReads()
    {
        var (service, _, store) = Setup();
        await service.RecommendAsync(Profile(), [], [School("B", 38_000m, "none", Major, sat: (1200, 1380), accept: 0.60m, avgGpa: 3.5m)]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];

        foreach (var key in new[] { "universityId", "code", "name", "state", "control", "cost", "outcome", "flags",
                     "g2", "s", "f", "h", "q", "gap", "pg", "ps", "adj", "r" })
            Assert.True(row.TryGetProperty(key, out _), $"thiếu khóa {key}");
        foreach (var key in new[] { "reach", "match", "safety", "aid", "other" })
            Assert.Equal(JsonValueKind.Array, items.RootElement.GetProperty(key).ValueKind);
        var x = items.RootElement.GetProperty("match")[0].GetProperty("x").GetDouble();
        Assert.InRange(x, 0.0, 1.0);
        Assert.InRange(row.GetProperty("pg").GetDouble(), 0.0, 100.0);   // thang 0–100 như bản cũ
    }

    [Fact]
    public async Task Recommend_SameInput_GivesTheSameJson()
    {
        var schools = new List<RecommendSchoolInput>
        {
            School("A", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
            School("B", 40_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
        };
        var (service1, _, store1) = Setup();
        var (service2, _, store2) = Setup();

        await service1.RecommendAsync(Profile(), [], schools);
        await service2.RecommendAsync(Profile(), [], schools.AsEnumerable().Reverse().ToList());

        Assert.Equal(store1.Rows.Single().ItemsJson, store2.Rows.Single().ItemsJson);
    }

    [Fact]
    public async Task Recommend_AtMostFourSchoolsPerBlock()
    {
        var (service, _, store) = Setup();
        var schools = Enumerable.Range(1, 9)
            .Select(i => School($"S{i}", 30_000m + i * 100, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m))
            .ToList();

        await service.RecommendAsync(Profile(), [], schools);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(4, items.RootElement.GetProperty("match").GetArrayLength());
        Assert.Equal(9, items.RootElement.GetProperty("scored").GetArrayLength());   // danh sách đầy đủ vẫn được lưu
    }

    // ---------------------------------------------------------------- công thức đi qua service

    [Fact]
    public async Task Recommend_SchoolWithoutAcademicData_GoesToTheOtherBlock()
    {
        var (service, _, store) = Setup();

        await service.RecommendAsync(Profile(), [], [School("Bare", 30_000m, "none", Major)]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal("NA", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal("Bare", items.RootElement.GetProperty("other")[0].GetProperty("code").GetString());
        Assert.Equal(0, items.RootElement.GetProperty("match").GetArrayLength());
    }

    [Fact]
    public async Task Recommend_AcceptanceRateIsTakenFromAdmittedOverApplicants_WhenTheColumnIsMissing()
    {
        var (service, _, store) = Setup();
        // 100 / 1.000 = 10% dưới 15%: luôn Khó vào dù điểm cao
        var school = School("Picky", 30_000m, "none", Major, sat: (1000, 1100), avgGpa: 3.0m, admitted: 100, applicants: 1_000);

        await service.RecommendAsync(Profile(), [], [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal("R", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal("Picky", items.RootElement.GetProperty("reach")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Recommend_English_BelowTheMinimum_IsFlaggedNotExcluded()
    {
        var (service, _, store) = Setup();
        var profile = Profile();
        profile.Ielts = 5.5m;
        var school = School("Strict", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, minIelts: 7.0m);

        await service.RecommendAsync(profile, [], [school]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var row = items.RootElement.GetProperty("scored")[0];
        Assert.Contains("ENGLISH_BELOW_MIN", row.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(0.0, row.GetProperty("e").GetDouble());   // tiếng Anh chỉ làm giảm fit score
    }

    [Fact]
    public async Task Recommend_QualityComesFromTheRankingPosition()
    {
        var (service, _, store) = Setup();
        var ranked = School("Top", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (10, 100));
        var unranked = School("None", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m);

        await service.RecommendAsync(Profile(), [], [ranked, unranked]);

        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        var rows = items.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!);
        Assert.Equal(0.91, rows["Top"].GetProperty("q").GetDouble());               // 1 − (10 − 1) / 100
        Assert.Equal(JsonValueKind.Null, rows["None"].GetProperty("q").ValueKind);   // chưa có hạng: bỏ thành phần Q
    }

    [Fact]
    public async Task Recommend_WithNothingLeftAfterTheFilter_SavesAnEmptyRunWithoutCallingTheAdvisor()
    {
        var (service, advisor, store) = Setup();

        await service.RecommendAsync(Profile(), [], [School("D", 20_000m, "none", "History", sat: (1200, 1380))]);

        Assert.Empty(advisor.Calls);   // không còn trường thì không tốn lượt đọc ngoại khóa
        using var criteria = JsonDocument.Parse(store.Rows.Single().CriteriaJson);
        using var items = JsonDocument.Parse(store.Rows.Single().ItemsJson);
        Assert.Equal(JsonValueKind.Null, criteria.RootElement.GetProperty("status").ValueKind);
        Assert.Equal(0, items.RootElement.GetProperty("scored").GetArrayLength());
    }

    // ---------------------------------------------------------------- điều kiện hồ sơ

    // C7: học bổng trống thì không gợi ý trường, không gọi advisor, không lưu lượt nào
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Recommend_WithoutScholarshipNeed_IsBlockedBeforeAnyCall(string? need)
    {
        var (service, advisor, store) = Setup();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecommendAsync(Profile(need), [], [School("B", 38_000m, "none", Major)]));

        Assert.Equal(RecommendationService.MissingScholarshipMessage, ex.Message);
        Assert.StartsWith("Hồ sơ", ex.Message); // controller trả 400, không phải 502
        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Recommend_WithoutMajor_IsBlockedBeforeAnyCall(string? major)
    {
        var (service, advisor, store) = Setup();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecommendAsync(Profile(major: major), [], [School("B", 38_000m, "none", Major)]));

        Assert.Equal(RecommendationService.MissingMajorMessage, ex.Message);
        Assert.StartsWith("Hồ sơ", ex.Message);
        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Recommend_WithOptionalScholarship_StillRuns()
    {
        var (service, advisor, _) = Setup();

        await service.RecommendAsync(Profile(ProfileConstants.ScholarshipNeeds.Optional), [], [School("B", 38_000m, "none", Major)]);

        Assert.Equal(["ec"], advisor.Calls); // "không cần" là lựa chọn thật, vẫn chạy
    }

    // ---------------------------------------------------------------- đổi nút ưu tiên

    [Fact]
    public async Task Repick_RecomputesWithNewPriorities_WithoutCallingTheAdvisorAgain()
    {
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("Cheap", 20_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (166, 332)),
            School("Famous", 49_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m, rank: (17, 332)),
        };
        var (service, advisor, store) = Setup(profile, schools);

        await service.RecommendAsync(profile, [], schools, [4, 2, 0]);
        var costFirst = MatchOrder(store.Rows.Last());
        var again = await service.RepickAsync(profile.UserId, [0, 2, 4], default);
        var qualityFirst = MatchOrder(again);

        Assert.Equal(["ec"], advisor.Calls);   // đổi ưu tiên không gọi lại advisor
        Assert.Equal(2, store.Rows.Count);
        Assert.Equal(["Cheap", "Famous"], costFirst);
        Assert.Equal(["Famous", "Cheap"], qualityFirst);
        using var criteria = JsonDocument.Parse(again.CriteriaJson);
        Assert.Equal(0, criteria.RootElement.GetProperty("pref")[0].GetInt32());
        Assert.Equal(3, criteria.RootElement.GetProperty("tier").GetInt32());   // giữ bậc ngoại khóa cũ
    }

    [Fact]
    public async Task Repick_DoesNotMoveASchoolBetweenGroups()
    {
        var profile = Profile();
        var schools = new List<RecommendSchoolInput>
        {
            School("A", 30_000m, "none", Major, sat: (1200, 1380), accept: 0.6m, avgGpa: 3.5m),
            School("B", 40_000m, "none", Major, sat: (1450, 1550), accept: 0.08m),
        };
        var (service, _, store) = Setup(profile, schools);

        await service.RecommendAsync(profile, [], schools);
        var again = await service.RepickAsync(profile.UserId, [4, 0, 0], default);

        using var a = JsonDocument.Parse(store.Rows[0].ItemsJson);
        using var b = JsonDocument.Parse(again.ItemsJson);
        var before = a.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("g2").GetString());
        var after = b.RootElement.GetProperty("scored").EnumerateArray().ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("g2").GetString());
        Assert.Equal(before, after);   // ưu tiên chỉ đổi thứ tự, không đổi nhóm
    }

    [Fact]
    public async Task Repick_WithoutAPreviousRun_ThrowsBeforeDoingAnything()
    {
        var (service, advisor, store) = Setup(Profile());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RepickAsync(Guid.NewGuid(), [2, 2, 2], default));

        Assert.Empty(advisor.Calls);
        Assert.Empty(store.Rows);
    }

    private static List<string> MatchOrder(Recommendation recommendation)
    {
        using var items = JsonDocument.Parse(recommendation.ItemsJson);
        return items.RootElement.GetProperty("match").EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToList();
    }

    // ---------------------------------------------------------------- dữ liệu giả

    private static RecommendSchoolInput School(
        string code, decimal tuition, string aidPolicy, string major,
        (int Low, int High)? sat = null, decimal? accept = null, decimal? avgGpa = null,
        decimal? minIelts = null, (int Rank, int Total)? rank = null, int? admitted = null, int? applicants = null)
    {
        var university = new University
        {
            Code = code, Name = code, State = "NY", Control = "private", IsActive = true, AcceptanceRate = accept
        };
        var offering = new UniversityOffering
        {
            UniversityId = university.Id,
            StudyLevel = "undergraduate",
            TuitionUsd = tuition,
            IntlAidPolicy = aidPolicy,
            Sat25 = sat?.Low,
            Sat75 = sat?.High,
            AvgGpa4 = avgGpa,
            MinIelts = minIelts,
            AdmittedTotal = admitted,
            ApplicantsTotal = applicants
        };
        return new RecommendSchoolInput(university, offering, [major], rank is { } r ? [new RankPosition(r.Rank, r.Total)] : null);
    }

    private sealed class FakeAdvisor : IAdvisorClient
    {
        public List<string> Calls { get; } = [];

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<EcResponseDto> ReadEcAsync(EcRequestDto request, CancellationToken ct = default)
        {
            Calls.Add("ec");
            return Task.FromResult(new EcResponseDto([], 1, 0, 3, "2.0", "qwen-test", false));
        }

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRecommendationRepository(IReadOnlyList<RecommendSchoolInput> schools) : IRecommendationRepository
    {
        public List<Recommendation> Rows { get; } = [];

        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        {
            Rows.Add(recommendation);
            return Task.CompletedTask;
        }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.LastOrDefault(r => r.UserId == userId));

        public Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default) =>
            Task.FromResult(schools);

        public Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default) =>
            Task.FromResult<RecommendSchoolInput?>(schools.FirstOrDefault(s => s.University.Code == code));
    }

    private sealed class FakeProfileFlow(StudentProfile? profile) : IProfileFlowRepository
    {
        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            profile is not null ? Task.FromResult<StudentProfile?>(profile) : throw new NotSupportedException();
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, IEnumerable<ProfileCertification> certifications, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
