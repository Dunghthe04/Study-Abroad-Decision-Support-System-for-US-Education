using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class RecommendationServiceTests
{
    [Fact]
    public async Task Recommend_FiltersBeforeQwen_ThenScores_ThenSavesCloudNumbers()
    {
        var advisor = new FakeAdvisor();
        var store = new FakeRecommendationRepository();
        var service = new RecommendationService(advisor, store, new UnusedProfileFlow());

        var profile = new StudentProfile
        {
            UserId = Guid.NewGuid(),
            TargetLevel = "undergraduate",
            IntendedMajor = "Computer and information sciences",
            BudgetMaxUsd = 50_000m,
            ScholarshipNeed = ProfileConstants.ScholarshipNeeds.Preferred,
            OverallGpa = 3.6m,
            Sat = 1350m,
            Ielts = 6.5m,
            TargetSchoolType = "all"
        };

        var schools = new List<RecommendSchoolInput>
        {
            School("Colgate", 95_610m, "need_and_merit", "Computer and information sciences"),
            School("B", 38_000m, "none", "Computer and information sciences"),
            School("D", 20_000m, "none", "History")
        };

        await service.RecommendAsync(profile, [], schools);

        Assert.Equal(["ec", "score", "pick"], advisor.Calls);
        Assert.Equal(3, advisor.ScoreRequest!.Student.Tier);
        Assert.DoesNotContain(advisor.ScoreRequest.Schools, s => s.C == "D");
        Assert.Equal("AID", advisor.PickRequest!.Items.Single(i => i.C == "Colgate").Blk);
        Assert.Equal("MAIN", advisor.PickRequest.Items.Single(i => i.C == "B").Blk);

        var saved = Assert.Single(store.Rows);
        Assert.Equal("1.1", saved.AlgorithmVersion);
        using var criteria = JsonDocument.Parse(saved.CriteriaJson);
        using var items = JsonDocument.Parse(saved.ItemsJson);
        Assert.Equal("gemini-test", criteria.RootElement.GetProperty("pickModel").GetString());
        Assert.Equal(12.9, items.RootElement.GetProperty("scored")[0].GetProperty("s").GetDouble());
        Assert.Equal("RF", items.RootElement.GetProperty("scored")[0].GetProperty("g2").GetString());
        Assert.Equal(0.414, items.RootElement.GetProperty("match")[0].GetProperty("x").GetDouble());
    }

    private static RecommendSchoolInput School(string code, decimal tuition, string aidPolicy, string major)
    {
        var university = new University { Code = code, Name = code, State = "NY", Control = "private", IsActive = true };
        var offering = new UniversityOffering
        {
            UniversityId = university.Id,
            StudyLevel = "undergraduate",
            Majors = [major],
            TuitionUsd = tuition,
            IntlAidPolicy = aidPolicy
        };
        return new RecommendSchoolInput(university, offering);
    }

    private sealed class FakeAdvisor : IAdvisorClient
    {
        public List<string> Calls { get; } = [];
        public ScoreRequestDto? ScoreRequest { get; private set; }
        public PickRequestDto? PickRequest { get; private set; }

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<EcResponseDto> ReadEcAsync(EcRequestDto request, CancellationToken ct = default)
        {
            Calls.Add("ec");
            return Task.FromResult(new EcResponseDto([], 1, 0, 3, "2.0", "qwen-test", false));
        }

        public Task<ScoreResponseDto> ScoreAsync(ScoreRequestDto request, CancellationToken ct = default)
        {
            if (!Calls.Contains("ec"))
                throw new InvalidOperationException("Chưa có bậc ngoại khóa.");
            Calls.Add("score");
            ScoreRequest = request;
            var items = request.Schools.Select(school => school.C == "Colgate"
                ? Item("Colgate", "R", "RF", 12.9, 0, 45610)
                : Item(school.C, "M", "M", 50, 0.24, null)).ToList();
            return Task.FromResult(new ScoreResponseDto("1.1", "gemini-test", items, []));
        }

        public Task<PickResponseDto> PickAsync(PickRequestDto request, CancellationToken ct = default)
        {
            if (!Calls.Contains("score"))
                throw new InvalidOperationException("Chưa có kết quả chấm.");
            Calls.Add("pick");
            PickRequest = request;
            var match = request.Items.Where(i => i.Blk == "MAIN")
                .Select(i => new PickEntryDto(i.C, 0.414, ["GPA_MID", "BUDGET_OK"])).ToList();
            var aid = request.Items.Where(i => i.Blk == "AID")
                .Select(i => new PickEntryDto(i.C, null, ["NEED_AID"])).ToList();
            return Task.FromResult(new PickResponseDto("1.1", "gemini-test", "OK", [], match, [], aid, []));
        }

        private static ScoreItemDto Item(string code, string g, string g2, double s, double? f, double? gap) =>
            new(code, [0.5, 0.5], 50, "H", 50, 50, 0, s, g, 1, g2, f, 0.1, 0.8, gap);
    }

    private sealed class FakeRecommendationRepository : IRecommendationRepository
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
            Task.FromResult<IReadOnlyList<RecommendSchoolInput>>([]);
    }

    private sealed class UnusedProfileFlow : IProfileFlowRepository
    {
        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetOfferingMajorsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
