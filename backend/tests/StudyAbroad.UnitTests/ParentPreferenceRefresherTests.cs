using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// UC-76 (2a'): con chạy gợi ý → danh sách theo ưu tiên cha mẹ đã lưu được tính lại trên lần gợi ý mới; lỗi không làm hỏng lượt của con.
public class ParentPreferenceRefresherTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRunner _runner = new();

    private readonly Guid _child = Guid.NewGuid();
    private readonly Guid _recId = Guid.NewGuid();
    private static readonly int[] Pref = [4, 2, 0];

    private ParentPreferenceRefresher CreateRefresher() => new(_links, _users);

    private ParentStudentLink AddLink(string? prefJson, string status = ParentLinkStatuses.Active, Guid? parent = null)
    {
        var link = new ParentStudentLink
        {
            StudentUserId = _child, ParentUserId = parent ?? Guid.NewGuid(), ParentEmail = "p@test.com",
            Status = status, ParentPreferenceJson = prefJson,
        };
        _links.Links.Add(link);
        return link;
    }

    private static string OldPref(Guid basedOn) => JsonSerializer.Serialize(new ParentPreferenceService.Stored(
        Pref, 1_300_000_000, 26_000m, 50_000m, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        JsonSerializer.Deserialize<JsonElement>("""{"reach":[{"code":"OLD"}]}"""), basedOn, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), false), WebJson);

    private ParentPreferenceService.Stored Row(Guid linkId) =>
        ParentPreferenceService.Read(_links.Links.Single(l => l.Id == linkId).ParentPreferenceJson)!;

    private void AddChild(string status = UserStatuses.Active) =>
        _users.Users.Add(new User { Id = _child, Email = "con@test.com", Status = status });

    // 1
    [Fact]
    public async Task ChildRun_RecomputesResult_KeepsParentPref()
    {
        AddChild();
        var link = AddLink(OldPref(Guid.NewGuid()));

        await CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None);

        var s = Row(link.Id);
        Assert.Equal("NEW", s.Result!.Value.GetProperty("reach")[0].GetProperty("code").GetString());
        Assert.Equal(_recId, s.BasedOnRecommendationId);
        Assert.False(s.Stale);
        Assert.Equal(ParentPreferenceService.CurrentRule, s.Rule);   // tính theo luật lọc hiện tại
        Assert.Equal(Pref, s.Pref);                    // ý cha mẹ giữ nguyên
        Assert.Equal(1_300_000_000, s.BudgetVnd);
        Assert.Equal(26_000m, s.VndPerUsd);            // tỷ giá đã chốt, không đổi
        var call = Assert.Single(_runner.Calls);
        Assert.Equal(_child, call.UserId);
        Assert.Equal(Pref, call.Pref);
        Assert.Equal(50_000m, call.Overrides.BudgetUsd);
        Assert.Equal(0m, call.Overrides.BudgetTolerance);
    }

    // 2
    [Fact]
    public async Task ParentWithoutPref_Revoked_Skipped()
    {
        AddChild();
        var noPref = AddLink(null);
        var revoked = AddLink(OldPref(Guid.NewGuid()), ParentLinkStatuses.Revoked);

        await CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None);

        Assert.Empty(_runner.Calls);
        Assert.Null(_links.Links.Single(l => l.Id == noPref.Id).ParentPreferenceJson);
        Assert.Equal("OLD", Row(revoked.Id).Result!.Value.GetProperty("reach")[0].GetProperty("code").GetString());
    }

    // 3
    [Fact]
    public async Task ManagedChild_Skipped()
    {
        AddChild(UserStatuses.Managed);
        AddLink(OldPref(Guid.NewGuid()));

        await CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None);

        Assert.Empty(_runner.Calls);
    }

    // 4
    [Fact]
    public async Task SimulateFails_NoThrow_StaleKeepsOldResult()
    {
        AddChild();
        var oldRec = Guid.NewGuid();
        var link = AddLink(OldPref(oldRec));
        _runner.Throw = true;

        await CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None);

        var s = Row(link.Id);
        Assert.True(s.Stale);
        Assert.Equal("OLD", s.Result!.Value.GetProperty("reach")[0].GetProperty("code").GetString());
        Assert.Equal(oldRec, s.BasedOnRecommendationId);
    }

    // 5
    [Fact]
    public async Task FatherAndMother_BothRecomputed()
    {
        AddChild();
        var dad = AddLink(OldPref(Guid.NewGuid()));
        var mom = AddLink(OldPref(Guid.NewGuid()));

        await CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None);

        Assert.Equal(2, _runner.Calls.Count);
        Assert.Equal(_recId, Row(dad.Id).BasedOnRecommendationId);
        Assert.Equal(_recId, Row(mom.Id).BasedOnRecommendationId);
    }

    // 6
    [Fact]
    public async Task UpdateConflict_NoThrow()
    {
        AddChild();
        AddLink(OldPref(Guid.NewGuid()));
        _links.ConflictsToThrow = 1;

        var ex = await Record.ExceptionAsync(() => CreateRefresher().OnChildRunAsync(_runner, _child, _recId, CancellationToken.None));

        Assert.Null(ex);
    }

    // 7: RecommendationService gọi refresher đúng con và đúng id lượt vừa lưu; lỗi của refresher không chặn lượt của con
    [Fact]
    public async Task RecommendationService_CallsRefresherAfterSave()
    {
        var store = new MiniRecommendationStore();
        var spy = new SpyRefresher();
        var profile = new StudentProfile
        {
            UserId = _child, TargetLevel = "undergraduate", IntendedMajor = "History", BudgetMaxUsd = 50_000m, TargetSchoolType = "all",
            ScholarshipNeed = ProfileConstants.ScholarshipNeeds.Preferred,
        };
        var service = new RecommendationService(new NoAdvisor(), store, new MiniProfileFlow(), spy);

        await service.RecommendAsync(profile, []);

        var saved = Assert.Single(store.Rows);
        Assert.Equal((_child, saved.Id), Assert.Single(spy.Calls));
        Assert.Same(service, spy.Runner);
    }

    // ---------------------------------------------------------------- fakes

    private sealed class FakeRunner : IRecommendationRunner
    {
        public bool Throw { get; set; }
        public List<(Guid UserId, SimulationOverrides Overrides, IReadOnlyList<int>? Pref)> Calls { get; } = [];

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            if (Throw) throw new InvalidOperationException("Chưa có hồ sơ.");
            Calls.Add((userId, overrides, pref));
            var g = new GroupSummary(1, 1, 0, 0, 0, 0);
            var p = new SimulationProfile(null, null, null, null, null, null, 3);
            return Task.FromResult(new SimulationResult(p, g, g, [], "{}", """{"reach":[{"code":"NEW"}]}"""));
        }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null, string? basis = null) => throw new NotImplementedException();
        public Task<ExplainSchoolDto> ExplainCardAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null) => throw new NotImplementedException();
        public Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct, SimulationOverrides? overrides = null) => throw new NotImplementedException();
        public Task<SchoolInfoDto> SchoolInfoAsync(Guid userId, string code, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SchoolRef>> FindMentionedSchoolsAsync(Guid userId, string text, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolSearchResult> SearchSchoolsAsync(Guid userId, SchoolSearchFilters filters, SchoolSort sort, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class SpyRefresher : IParentPreferenceRefresher
    {
        public List<(Guid, Guid)> Calls { get; } = [];
        public IRecommendationRunner? Runner { get; private set; }

        public Task OnChildRunAsync(IRecommendationRunner runner, Guid childId, Guid recommendationId, CancellationToken ct)
        {
            Runner = runner;
            Calls.Add((childId, recommendationId));
            return Task.CompletedTask;
        }
    }

    private sealed class NoAdvisor : IAdvisorClient
    {
        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class MiniRecommendationStore : IRecommendationRepository
    {
        public List<Recommendation> Rows { get; } = [];
        public Task<IReadOnlyList<SchoolRanking>> ListSchoolRankingsAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchoolRanking>>([]);
        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default) { Rows.Add(recommendation); return Task.CompletedTask; }
        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Rows.LastOrDefault());
        public Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecommendSchoolInput>>([]);
        public Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default) =>
            Task.FromResult<RecommendSchoolInput?>(null);
    }

    private sealed class MiniProfileFlow : IProfileFlowRepository
    {
        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UsStateOption>>([]);
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities, IEnumerable<ProfileCertification> certifications, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProfileActivity>>([]);
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProfileCertification>>([]);
    }
}
