using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// UC-76 (2a): phụ huynh lưu ưu tiên + ngân sách VND cho con có liên kết thật; chốt tỷ giá và tính sẵn danh sách theo cha mẹ.
public class ParentPreferenceServiceTests
{
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRunner _runner = new();
    private readonly FakeRates _rates = new();

    private readonly Guid _parent = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();

    private static readonly int[] Pref = [4, 2, 0];

    private ParentPreferenceService CreateService() => new(_links, _users, _runner, _rates);

    private void Setup(string linkStatus = ParentLinkStatuses.Active, string childStatus = UserStatuses.Active)
    {
        _users.Users.Add(new User { Id = _child, Email = "con@test.com", Status = childStatus });
        _links.Links.Add(new ParentStudentLink { StudentUserId = _child, ParentUserId = _parent, ParentEmail = "p@test.com", Status = linkStatus });
    }

    private ParentPreferenceService.Stored StoredRow() =>
        ParentPreferenceService.Read(Assert.Single(_links.Links).ParentPreferenceJson)!;

    // 1
    [Fact]
    public async Task ActiveLink_SavesPrefAndBudget()
    {
        Setup();
        var r = await CreateService().SaveAsync(_parent, _child, Pref, 1_300_000_000, CancellationToken.None);

        Assert.Equal(ParentLinkError.None, r.Error);
        Assert.Equal(Pref, StoredRow().Pref);
        Assert.Equal(1_300_000_000, StoredRow().BudgetVnd);
        var got = await CreateService().GetAsync(_parent, _child, CancellationToken.None);
        Assert.Equal(Pref, got.Value!.Pref);
    }

    // 2
    [Fact]
    public async Task ManagedChild_NotFound()
    {
        Setup(childStatus: UserStatuses.Managed);
        var r = await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        Assert.Equal(ParentLinkError.NotFound, r.Error);
        Assert.Null(Assert.Single(_links.Links).ParentPreferenceJson);
    }

    // 3
    [Fact]
    public async Task RevokedLink_NotFound()
    {
        Setup(linkStatus: ParentLinkStatuses.Revoked);
        Assert.Equal(ParentLinkError.NotFound, (await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None)).Error);
        Assert.Equal(ParentLinkError.NotFound, (await CreateService().GetAsync(_parent, _child, CancellationToken.None)).Error);
    }

    // 4
    [Fact]
    public async Task OtherParent_NotFound()
    {
        Setup();
        var r = await CreateService().SaveAsync(Guid.NewGuid(), _child, Pref, null, CancellationToken.None);

        Assert.Equal(ParentLinkError.NotFound, r.Error);
    }

    // 5
    [Theory]
    [InlineData(new[] { 5, 2, 0 })]
    [InlineData(new[] { -1, 2, 0 })]
    [InlineData(new[] { 2, 2 })]
    public async Task PrefOutOfRange_Validation(int[] pref)
    {
        Setup();
        var r = await CreateService().SaveAsync(_parent, _child, pref, null, CancellationToken.None);

        Assert.Equal(ParentLinkError.Validation, r.Error);
        Assert.Null(Assert.Single(_links.Links).ParentPreferenceJson);
    }

    // 6
    [Theory]
    [InlineData(99_999_999L)]
    [InlineData(10_000_000_001L)]
    public async Task BudgetOutOfRange_Validation(long vnd)
    {
        Setup();
        var r = await CreateService().SaveAsync(_parent, _child, Pref, vnd, CancellationToken.None);

        Assert.Equal(ParentLinkError.Validation, r.Error);
    }

    // 7
    [Fact]
    public async Task EmptyBudget_SavesNull_AndSimulatesWithChildBudget()
    {
        Setup();
        _runner.Latest = new Recommendation { UserId = _child };
        await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        var s = StoredRow();
        Assert.Null(s.BudgetVnd);
        Assert.Null(s.VndPerUsd);
        Assert.Null(s.BudgetUsd);
        Assert.Null(_runner.LastOverrides!.BudgetUsd);   // dùng ngân sách trong hồ sơ của con
    }

    // 8
    [Fact]
    public async Task Save_LocksRate_AndUpdatedAt()
    {
        Setup();
        var before = DateTime.UtcNow;
        await CreateService().SaveAsync(_parent, _child, Pref, 1_300_000_000, CancellationToken.None);
        _rates.Rate = 30_000m;   // tỷ giá đổi sau khi lưu

        var got = (await CreateService().GetAsync(_parent, _child, CancellationToken.None)).Value!;
        Assert.Equal(26_000m, got.VndPerUsd);
        Assert.Equal(50_000m, got.BudgetUsd);   // 1,3 tỷ / 26.000, không tính lại theo 30.000
        Assert.True(got.UpdatedAt >= before);
    }

    // 9
    [Fact]
    public async Task ChildHasRecommendation_SavesResultAndBasedOn()
    {
        Setup();
        var latest = new Recommendation { UserId = _child };
        _runner.Latest = latest;
        await CreateService().SaveAsync(_parent, _child, Pref, 1_300_000_000, CancellationToken.None);

        var s = StoredRow();
        Assert.Equal(latest.Id, s.BasedOnRecommendationId);
        Assert.Equal("A", s.Result!.Value.GetProperty("reach")[0].GetProperty("code").GetString());
        Assert.False(s.Stale);
        Assert.Equal(_child, _runner.LastUserId);   // tính trên hồ sơ của con
        Assert.Equal(Pref, _runner.LastPref);
        Assert.Equal(50_000m, _runner.LastOverrides!.BudgetUsd);
    }

    // 10
    [Fact]
    public async Task NoRecommendationYet_NoResult_NoSimulate()
    {
        Setup();
        var r = await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        Assert.False(r.Value!.HasResult);
        Assert.Null(_runner.LastUserId);
        Assert.False(StoredRow().Stale);
    }

    // 11
    [Fact]
    public async Task SimulateFails_StillSaves_Stale()
    {
        Setup();
        _runner.Latest = new Recommendation { UserId = _child };
        _runner.Throw = true;
        var r = await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        Assert.Equal(ParentLinkError.None, r.Error);
        Assert.True(StoredRow().Stale);
        Assert.Null(StoredRow().Result);
    }

    // 12
    [Fact]
    public async Task ConcurrentChange_Conflict()
    {
        Setup();
        _links.ConflictsToThrow = 1;
        var r = await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        Assert.Equal(ParentLinkError.Conflict, r.Error);
    }

    private sealed class FakeRates : IExchangeRateProvider
    {
        public decimal Rate { get; set; } = 26_000m;
        public Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default) => Task.FromResult(Rate);
    }

    // Chỉ GetLatestAsync và SimulateAsync được dùng
    private sealed class FakeRunner : IRecommendationRunner
    {
        public Recommendation? Latest { get; set; }
        public bool Throw { get; set; }
        public Guid? LastUserId { get; private set; }
        public SimulationOverrides? LastOverrides { get; private set; }
        public IReadOnlyList<int>? LastPref { get; private set; }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) => Task.FromResult(Latest);

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            if (Throw) throw new InvalidOperationException("Chưa có hồ sơ.");
            (LastUserId, LastOverrides, LastPref) = (userId, overrides, pref);
            var g = new GroupSummary(1, 1, 0, 0, 0, 0);
            var profile = new SimulationProfile(null, null, null, null, null, null, 3);
            return Task.FromResult(new SimulationResult(profile, g, g, [], "{}", """{"reach":[{"code":"A"}]}"""));
        }

        public Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null, string? basis = null) => throw new NotImplementedException();
        public Task<ExplainSchoolDto> ExplainCardAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null) => throw new NotImplementedException();
        public Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolInfoDto> SchoolInfoAsync(Guid userId, string code, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SchoolRef>> FindMentionedSchoolsAsync(Guid userId, string text, CancellationToken ct) => throw new NotImplementedException();
        public Task<SchoolSearchResult> SearchSchoolsAsync(Guid userId, SchoolSearchFilters filters, SchoolSort sort, CancellationToken ct) => throw new NotImplementedException();
    }
}
