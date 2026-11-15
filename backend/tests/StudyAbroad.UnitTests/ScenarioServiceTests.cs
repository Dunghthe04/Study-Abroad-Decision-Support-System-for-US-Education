using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// UC-86: lưu, xem, mở lại, đổi tên, xóa kịch bản what-if. Học sinh chỉ thấy của mình; phụ huynh có liên kết đang hiệu lực chỉ được xem.
public class ScenarioServiceTests
{
    private readonly FakeScenarioRepository _scenarios = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeSimulator _simulator = new();

    private readonly Guid _student = Guid.NewGuid();
    private readonly Guid _otherStudent = Guid.NewGuid();
    private readonly Guid _parent = Guid.NewGuid();

    private static readonly SimulationOverrides SatOnly = new(null, 1450m, null, null, null, null, null);

    private ScenarioService CreateService() => new(_scenarios, _simulator, _links);

    private void LinkParent(string status) =>
        _links.Links.Add(new ParentStudentLink { StudentUserId = _student, ParentUserId = _parent, ParentEmail = "me@test.com", Status = status });

    private async Task<SavedScenarioDto> SaveAsync(string title = "Tập trung SAT") =>
        await CreateService().SaveAsync(_student, title, SatOnly, null, CancellationToken.None);

    // 1
    [Fact]
    public async Task Save_Valid_StoresValuesAndSnapshot()
    {
        var saved = await CreateService().SaveAsync(_student, "  Tập trung SAT  ", SatOnly, [2, 4, 2], CancellationToken.None);

        var row = Assert.Single(_scenarios.Rows);
        Assert.Equal(_student, row.UserId);
        Assert.Equal("Tập trung SAT", row.Title);               // đã cắt khoảng trắng hai đầu
        Assert.Equal(_student, _simulator.CalledFor);           // bản chụp do backend tự tính, không nhận từ frontend
        Assert.Equal(row.Id, saved.Id);
        Assert.Equal("what_if", saved.Kind);
        Assert.Equal(1450m, saved.Overrides.Sat);
        Assert.Equal([2, 4, 2], saved.Pref);
        Assert.Equal(FakeSimulator.Baseline, saved.Baseline);
        Assert.Equal(FakeSimulator.Simulated, saved.Simulated);
        Assert.Equal("188429", Assert.Single(saved.Changes).Code);
    }

    // 2
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_BlankTitle_Rejected(string? title)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().SaveAsync(_student, title, SatOnly, null, CancellationToken.None));
        Assert.Empty(_scenarios.Rows);
    }

    // 3
    [Fact]
    public async Task Save_TitleTooLong_Rejected()
    {
        var title = new string('a', ScenarioService.MaxTitleLength + 1);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().SaveAsync(_student, title, SatOnly, null, CancellationToken.None));
        Assert.Empty(_scenarios.Rows);
    }

    // 4
    [Fact]
    public async Task Save_NoSimulatedValue_Rejected()
    {
        var empty = new SimulationOverrides(null, null, null, null, null, null, null);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().SaveAsync(_student, "Trống", empty, null, CancellationToken.None));
        Assert.Null(_simulator.CalledFor);
        Assert.Empty(_scenarios.Rows);
    }

    // 4b: chỉ đổi mức ưu tiên cũng là một kịch bản
    [Fact]
    public async Task Save_OnlyPref_Allowed()
    {
        var empty = new SimulationOverrides(null, null, null, null, null, null, null);
        await CreateService().SaveAsync(_student, "Ưu tiên học bổng", empty, [1, 4, 1], CancellationToken.None);
        Assert.Single(_scenarios.Rows);
    }

    // 5
    [Fact]
    public async Task Save_AtLimit_Rejected()
    {
        for (var i = 0; i < ScenarioService.MaxPerUser; i++)
            _scenarios.Rows.Add(new WhatIfSimulation { UserId = _student, Title = $"KB {i}" });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync("Cái thứ 21"));
        Assert.Contains("xóa bớt", ex.Message);
        Assert.Equal(ScenarioService.MaxPerUser, _scenarios.Rows.Count);
    }

    // 6
    [Fact]
    public async Task List_OnlyMine_NewestFirst()
    {
        var now = DateTime.UtcNow;
        _scenarios.Rows.Add(new WhatIfSimulation { UserId = _student, Title = "Cũ", CreatedAt = now.AddDays(-2) });
        _scenarios.Rows.Add(new WhatIfSimulation { UserId = _student, Title = "Mới", CreatedAt = now });
        _scenarios.Rows.Add(new WhatIfSimulation { UserId = _otherStudent, Title = "Của bạn khác", CreatedAt = now });

        var list = await CreateService().ListAsync(_student, null, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Equal(["Mới", "Cũ"], list.Select(s => s.Title));
    }

    // 7
    [Fact]
    public async Task OtherStudent_CannotOpenRenameOrDelete()
    {
        var saved = await SaveAsync();
        var service = CreateService();

        Assert.Null(await service.GetAsync(_otherStudent, saved.Id, CancellationToken.None));
        Assert.Null(await service.RenameAsync(_otherStudent, saved.Id, "Đổi trộm", CancellationToken.None));
        Assert.False(await service.DeleteAsync(_otherStudent, saved.Id, CancellationToken.None));
        Assert.Null(await service.ListAsync(_otherStudent, _student, CancellationToken.None));

        var row = Assert.Single(_scenarios.Rows);
        Assert.Equal("Tập trung SAT", row.Title);
    }

    // 8
    [Fact]
    public async Task Rename_ChangesTitleAndSetsUpdatedAt()
    {
        var saved = await SaveAsync();

        var renamed = await CreateService().RenameAsync(_student, saved.Id, " SAT 1.450 ", CancellationToken.None);

        Assert.NotNull(renamed);
        Assert.Equal("SAT 1.450", renamed.Title);
        Assert.NotNull(renamed.UpdatedAt);
        Assert.Equal("SAT 1.450", Assert.Single(_scenarios.Rows).Title);
    }

    // 8b
    [Fact]
    public async Task Rename_BlankTitle_Rejected()
    {
        var saved = await SaveAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().RenameAsync(_student, saved.Id, "  ", CancellationToken.None));
    }

    // 9
    [Fact]
    public async Task Delete_RemovesRow()
    {
        var saved = await SaveAsync();

        Assert.True(await CreateService().DeleteAsync(_student, saved.Id, CancellationToken.None));
        Assert.Empty(_scenarios.Rows);
    }

    // 10
    [Fact]
    public async Task LinkedParent_CanListAndOpen()
    {
        var saved = await SaveAsync();
        LinkParent(ParentLinkStatuses.Active);
        var service = CreateService();

        var list = await service.ListAsync(_parent, _student, CancellationToken.None);
        var opened = await service.GetAsync(_parent, saved.Id, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Equal(saved.Id, Assert.Single(list).Id);
        Assert.NotNull(opened);
    }

    // 11
    [Theory]
    [InlineData(ParentLinkStatuses.Revoked)]
    [InlineData(ParentLinkStatuses.Pending)]
    [InlineData(null)]   // không có liên kết nào
    public async Task ParentWithoutActiveLink_CannotSee(string? status)
    {
        var saved = await SaveAsync();
        if (status is not null)
            LinkParent(status);
        var service = CreateService();

        Assert.Null(await service.ListAsync(_parent, _student, CancellationToken.None));
        Assert.Null(await service.GetAsync(_parent, saved.Id, CancellationToken.None));
    }

    // 12
    [Fact]
    public async Task LinkedParent_CannotRenameOrDelete()
    {
        var saved = await SaveAsync();
        LinkParent(ParentLinkStatuses.Active);
        var service = CreateService();

        Assert.Null(await service.RenameAsync(_parent, saved.Id, "Bố đổi", CancellationToken.None));
        Assert.False(await service.DeleteAsync(_parent, saved.Id, CancellationToken.None));
        Assert.Equal("Tập trung SAT", Assert.Single(_scenarios.Rows).Title);
    }

    // Chỉ SimulateAsync được dùng; các hàm khác không liên quan đến UC-86
    internal sealed class FakeSimulator : IRecommendationRunner
    {
        public static readonly GroupSummary Baseline = new(30, 10, 12, 8, 0, 0);
        public static readonly GroupSummary Simulated = new(30, 7, 14, 9, 0, 0);

        public Guid? CalledFor { get; private set; }

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            CalledFor = userId;
            var profile = new SimulationProfile(3.4m, null, null, 6.5m, 35_000m, "optional", 3);
            return Task.FromResult(new SimulationResult(profile, Baseline, Simulated,
                [new SchoolChange("188429", "Boston University", "R", "M")], "{}", "{}"));
        }

        public Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct) => throw new NotImplementedException();
        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
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
