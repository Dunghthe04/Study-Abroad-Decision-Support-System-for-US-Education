using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
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

    private readonly FakeProfiles _profiles = new();
    private readonly FakeNotifications _notifications = new();

    private ParentPreferenceService CreateService() => new(_links, _users, _runner, _rates, _profiles, _notifications);

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
        Assert.Null(_runner.LastOverrides!.BudgetTolerance);   // ngân sách của con: mức dư như con (10%)
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
        Assert.Equal(0m, _runner.LastOverrides!.BudgetTolerance);   // ngân sách cha mẹ là con số chốt, không có mức dư
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

    // ===== 2c: so sánh =====

    private const string ChildItems = """{"match":[{"code":"A"},{"code":"B"}],"scored":[{"code":"A"},{"code":"B"}]}""";
    private const string ParentItems = """{"match":[{"code":"B"},{"code":"C"}],"scored":[{"code":"B"},{"code":"C"}]}""";

    private Recommendation ChildRun(DateTime? at = null)
    {
        var rec = new Recommendation { UserId = _child, ItemsJson = ChildItems, CriteriaJson = """{"pref":[1,3,2]}""" };
        if (at is { } a) rec.CreatedAt = a;
        _runner.Latest = rec;
        _runner.ItemsJson = ParentItems;
        return rec;
    }

    // 13
    [Fact]
    public async Task Compare_Preview_DoesNotWriteDb()
    {
        Setup();
        ChildRun();
        var r = await CreateService().CompareForParentAsync(_parent, _child, [4, 0, 2], 1_300_000_000, CancellationToken.None);

        var d = r.Value!;
        Assert.True(d.IsPreview);
        Assert.Equal(50_000m, d.ParentBudgetUsd);   // tỷ giá hôm nay
        Assert.Equal(new CompareCounts(1, 1, 1), d.Comparison!.Counts);
        Assert.Null(Assert.Single(_links.Links).ParentPreferenceJson);
        Assert.Empty(_notifications.Sent);
    }

    // 14
    [Fact]
    public async Task Compare_NotSavedYet_PreviewsBalanced()
    {
        Setup();
        ChildRun();
        var d = (await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None)).Value!;

        Assert.False(d.ParentHasPref);
        Assert.True(d.IsPreview);
        Assert.Equal([2, 2, 2], d.ParentPref);
        Assert.Equal([2, 2, 2], _runner.LastPref);
    }

    // 15
    [Fact]
    public async Task Compare_ResultUpToDate_NoSimulate()
    {
        Setup();
        ChildRun();
        await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);
        var calls = _runner.SimulateCalls;

        var d = (await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None)).Value!;

        Assert.Equal(calls, _runner.SimulateCalls);
        Assert.False(d.IsPreview);
        Assert.True(d.ParentHasPref);
        Assert.Equal([1, 3, 2], d.StudentPref);
        Assert.Equal(Pref, d.ParentPref);
    }

    // 16
    [Fact]
    public async Task Compare_StaleOrOldRun_RecomputesAndSaves()
    {
        Setup();
        ChildRun();
        await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);
        var newer = ChildRun();   // con chạy gợi ý lần mới, refresher chưa kịp tính
        var calls = _runner.SimulateCalls;

        await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None);

        Assert.Equal(calls + 1, _runner.SimulateCalls);
        Assert.Equal(newer.Id, StoredRow().BasedOnRecommendationId);
        Assert.False(StoredRow().Stale);
    }

    // 17
    [Fact]
    public async Task Compare_ProfileChangedAfterRun_Flagged()
    {
        Setup();
        ChildRun(DateTime.UtcNow.AddHours(-2));
        _profiles.Profile = new StudentProfile { UserId = _child, UpdatedAt = DateTime.UtcNow };

        var d = (await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None)).Value!;
        Assert.True(d.ProfileChangedAfterRun);
    }

    // 18
    [Fact]
    public async Task Compare_ChildNotRunYet_Message()
    {
        Setup();
        var d = (await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None)).Value!;

        Assert.False(d.StudentHasRun);
        Assert.Null(d.Comparison);
        Assert.Contains("chưa nhận gợi ý", d.Message);
    }

    // 19
    [Fact]
    public async Task Compare_BadPref_Validation_NoLink_NotFound()
    {
        Setup();
        var svc = CreateService();
        Assert.Equal(ParentLinkError.Validation, (await svc.CompareForParentAsync(_parent, _child, [5, 0, 0], null, CancellationToken.None)).Error);
        Assert.Equal(ParentLinkError.Validation, (await svc.CompareForParentAsync(_parent, _child, null, 5, CancellationToken.None)).Error);
        Assert.Equal(ParentLinkError.NotFound, (await svc.CompareForParentAsync(Guid.NewGuid(), _child, null, null, CancellationToken.None)).Error);
    }

    // 20
    [Fact]
    public async Task Compare_ProfileNotReady_Conflict()
    {
        Setup();
        ChildRun();
        _runner.Throw = true;
        var r = await CreateService().CompareForParentAsync(_parent, _child, [4, 0, 2], null, CancellationToken.None);
        Assert.Equal(ParentLinkError.Conflict, r.Error);
    }

    // 21
    [Fact]
    public async Task Save_NotifiesChild_OnlyOnSave()
    {
        Setup();
        await CreateService().SaveAsync(_parent, _child, Pref, null, CancellationToken.None);

        var n = Assert.Single(_notifications.Sent);
        Assert.Equal(_child, n.UserId);
        Assert.Equal(ParentPreferenceService.NotificationType, n.Type);
        Assert.Contains(_parent.ToString(), n.LinkUrl);
    }

    // 22: có cả bố và mẹ, con chọn theo parentId; mặc định là người đã lưu
    [Fact]
    public async Task Child_PicksParent_DefaultsToOneWithPref()
    {
        Setup();
        var mom = Guid.NewGuid();
        _users.Users.Add(new User { Id = mom, Email = "me@test.com", FullName = "Mẹ Lan" });
        _links.Links.Add(new ParentStudentLink { StudentUserId = _child, ParentUserId = mom, ParentEmail = "me@test.com", Status = ParentLinkStatuses.Active });
        ChildRun();
        await CreateService().SaveAsync(mom, _child, Pref, null, CancellationToken.None);

        var v = (await CreateService().GetForChildAsync(_child, null, CancellationToken.None)).Value!;
        Assert.Equal(2, v.Parents.Count);
        Assert.Equal(mom, v.SelectedParentId);
        Assert.Contains(v.Parents, p => p.Id == mom && p.Name == "Mẹ Lan" && p.HasPref);
        Assert.Contains(v.Parents, p => p.Id == _parent && p.Name == "p@test.com" && !p.HasPref);
        Assert.NotNull(v.Compare!.Comparison);

        var dad = (await CreateService().GetForChildAsync(_child, _parent, CancellationToken.None)).Value!;
        Assert.Equal(_parent, dad.SelectedParentId);
        Assert.False(dad.Compare!.ParentHasPref);
        Assert.Null(dad.Compare.Comparison);
        Assert.Contains("chưa chọn ưu tiên", dad.Compare.Message);

        Assert.Equal(ParentLinkError.NotFound, (await CreateService().GetForChildAsync(_child, Guid.NewGuid(), CancellationToken.None)).Error);
    }

    // 23
    [Fact]
    public async Task Child_Managed_NotFound_NoLinks_Empty()
    {
        _users.Users.Add(new User { Id = _child, Email = "con@test.com", Status = UserStatuses.Active });
        var empty = (await CreateService().GetForChildAsync(_child, null, CancellationToken.None)).Value!;
        Assert.Empty(empty.Parents);
        Assert.Null(empty.Compare);

        var managed = Guid.NewGuid();
        _users.Users.Add(new User { Id = managed, Email = "m@test.com", Status = UserStatuses.Managed });
        Assert.Equal(ParentLinkError.NotFound, (await CreateService().GetForChildAsync(managed, null, CancellationToken.None)).Error);
    }

    // 24: chỉ giữ số liệu các trường có trong khối, khối thiếu thành mảng rỗng
    [Fact]
    public void Picked_KeepsOnlyScoredOfPickedSchools()
    {
        var items = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            """{"reach":[{"code":"A","x":1}],"aid":[{"code":"b"}],"scored":[{"code":"A","g2":"R"},{"code":"B"},{"code":"Z"}],"failed":["Q"]}""");
        var p = ParentPreferenceService.Picked(items);

        var codes = p.GetProperty("scored").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Equal(["A", "B"], codes);
        Assert.Equal(1, p.GetProperty("reach")[0].GetProperty("x").GetInt32());
        Assert.Equal(0, p.GetProperty("match").GetArrayLength());
        Assert.False(p.TryGetProperty("failed", out _));
    }

    // 25: màn so sánh trả kèm hai danh sách để vẽ thẻ trường
    [Fact]
    public async Task Compare_ReturnsBothItemLists()
    {
        Setup();
        ChildRun();
        var d = (await CreateService().CompareForParentAsync(_parent, _child, [4, 0, 2], null, CancellationToken.None)).Value!;

        Assert.Equal(2, d.StudentItems!.Value.GetProperty("scored").GetArrayLength());
        Assert.Equal("C", d.ParentItems!.Value.GetProperty("match")[1].GetProperty("code").GetString());
    }

    // 26: kết quả lưu theo luật lọc cũ (trước khi bỏ mức dư 10% cho ngân sách cha mẹ, chưa có Rule) thì tính lại và ghi Rule mới
    [Fact]
    public async Task Compare_OldRule_RecomputesAndSaves()
    {
        Setup();
        ChildRun();
        await CreateService().SaveAsync(_parent, _child, Pref, 1_300_000_000, CancellationToken.None);
        Assert.Equal(ParentPreferenceService.CurrentRule, StoredRow().Rule);

        var link = Assert.Single(_links.Links);
        var old = System.Text.Json.Nodes.JsonNode.Parse(link.ParentPreferenceJson!)!.AsObject();
        old.Remove("rule");   // bản lưu trước khi có Rule
        link.ParentPreferenceJson = old.ToJsonString();
        Assert.Equal(0, StoredRow().Rule);
        var calls = _runner.SimulateCalls;

        await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None);

        Assert.Equal(calls + 1, _runner.SimulateCalls);
        Assert.Equal(0m, _runner.LastOverrides!.BudgetTolerance);
        Assert.Equal(ParentPreferenceService.CurrentRule, StoredRow().Rule);

        await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None);
        Assert.Equal(calls + 1, _runner.SimulateCalls);   // đã đúng luật: không tính lại nữa
    }

    // 27: UC-94 popup — chỉ lưu tiền: chưa có ưu tiên, không báo con, UC-76 tự hiện số tiền
    [Fact]
    public async Task SaveBudget_NoPref_SavesBudgetOnly_NoNotify()
    {
        Setup();
        var r = await CreateService().SaveBudgetAsync(_parent, _child, 1_300_000_000, CancellationToken.None);

        Assert.Equal(ParentLinkError.None, r.Error);
        var s = StoredRow();
        Assert.Null(s.Pref);
        Assert.Equal(1_300_000_000, s.BudgetVnd);
        Assert.Equal(50_000m, s.BudgetUsd);
        Assert.Null(s.Result);
        Assert.Empty(_notifications.Sent);

        var got = (await CreateService().GetAsync(_parent, _child, CancellationToken.None)).Value!;
        Assert.False(got.HasPref);
        Assert.Equal([2, 2, 2], got.Pref);
        Assert.Equal(1_300_000_000, got.BudgetVnd);
    }

    // 28: đã có ưu tiên → giữ ưu tiên, đổi tiền, đánh dấu cần tính lại
    [Fact]
    public async Task SaveBudget_KeepsExistingPref_MarksStale()
    {
        Setup();
        ChildRun();
        await CreateService().SaveAsync(_parent, _child, Pref, 1_300_000_000, CancellationToken.None);
        _notifications.Sent.Clear();

        await CreateService().SaveBudgetAsync(_parent, _child, 2_600_000_000, CancellationToken.None);

        var s = StoredRow();
        Assert.Equal(Pref, s.Pref);
        Assert.Equal(2_600_000_000, s.BudgetVnd);
        Assert.Equal(100_000m, s.BudgetUsd);
        Assert.True(s.Stale);
        Assert.Empty(_notifications.Sent);
    }

    // 29
    [Theory]
    [InlineData(null)]
    [InlineData(99_999_999L)]
    [InlineData(10_000_000_001L)]
    public async Task SaveBudget_NullOrOutOfRange_Validation(long? vnd)
    {
        Setup();
        var r = await CreateService().SaveBudgetAsync(_parent, _child, vnd, CancellationToken.None);

        Assert.Equal(ParentLinkError.Validation, r.Error);
        Assert.Null(Assert.Single(_links.Links).ParentPreferenceJson);
    }

    // 30
    [Fact]
    public async Task SaveBudget_Managed_NotFound()
    {
        Setup(childStatus: UserStatuses.Managed);
        Assert.Equal(ParentLinkError.NotFound, (await CreateService().SaveBudgetAsync(_parent, _child, 1_300_000_000, CancellationToken.None)).Error);
    }

    // 31: chỉ có tiền → so sánh xem trước với ưu tiên cân bằng và số tiền đã lưu; con thấy "chưa chọn ưu tiên"
    [Fact]
    public async Task OnlyBudget_ComparePreviewsWithSavedBudget_ChildSeesNoPref()
    {
        Setup();
        ChildRun();
        await CreateService().SaveBudgetAsync(_parent, _child, 1_300_000_000, CancellationToken.None);

        var d = (await CreateService().CompareForParentAsync(_parent, _child, null, null, CancellationToken.None)).Value!;
        Assert.True(d.IsPreview);
        Assert.False(d.ParentHasPref);
        Assert.Equal(50_000m, d.ParentBudgetUsd);
        Assert.Equal([2, 2, 2], _runner.LastPref);

        var v = (await CreateService().GetForChildAsync(_child, null, CancellationToken.None)).Value!;
        Assert.Contains(v.Parents, p => p.Id == _parent && !p.HasPref);
        Assert.False(v.Compare!.ParentHasPref);
        Assert.Null(v.Compare.Comparison);
    }

    internal sealed class FakeProfiles : IAcademicProfileRepository
    {
        public StudentProfile? Profile { get; set; }
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Profile?.UserId == userId ? Profile : null);
        public Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<TranscriptScore>());
        public Task<List<ProfileCertification>> GetCertificationsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<ProfileCertification>());
    }

    internal sealed class FakeNotifications : INotificationService
    {
        public readonly List<(Guid UserId, string Type, string? LinkUrl)> Sent = [];
        public Task NotifyAsync(Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default)
        {
            Sent.Add((userId, type, linkUrl));
            return Task.CompletedTask;
        }
        public Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    internal sealed class FakeRates : IExchangeRateProvider
    {
        public decimal Rate { get; set; } = 26_000m;
        public Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default) => Task.FromResult(Rate);
    }

    // Chỉ GetLatestAsync và SimulateAsync được dùng
    internal sealed class FakeRunner : IRecommendationRunner
    {
        public Recommendation? Latest { get; set; }
        public bool Throw { get; set; }
        public Guid? LastUserId { get; private set; }
        public SimulationOverrides? LastOverrides { get; private set; }
        public IReadOnlyList<int>? LastPref { get; private set; }
        public int SimulateCalls { get; private set; }
        public string ItemsJson { get; set; } = """{"reach":[{"code":"A"}]}""";

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) => Task.FromResult(Latest);

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            if (Throw) throw new InvalidOperationException("Chưa có hồ sơ.");
            (LastUserId, LastOverrides, LastPref) = (userId, overrides, pref);
            SimulateCalls++;
            var g = new GroupSummary(1, 1, 0, 0, 0, 0);
            var profile = new SimulationProfile(null, null, null, null, null, null, 3);
            return Task.FromResult(new SimulationResult(profile, g, g, [], "{}", ItemsJson));
        }

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
}
