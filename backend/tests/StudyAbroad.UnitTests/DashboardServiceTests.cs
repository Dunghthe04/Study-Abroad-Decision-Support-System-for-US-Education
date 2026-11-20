using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Dashboard;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class DashboardServiceTests
{
    private readonly FakeRecommendationHistoryRepository _historyRepo = new();
    private readonly FakeParentLinkRepository _linkRepo = new();
    private readonly FakeUserRepository _userRepo = new();
    private readonly FakeAcademicProfileRepo _profileRepo = new();
    private readonly FakeProfileFlowService _flowService = new();
    private readonly FakeTargetSchoolService _targetSchoolService = new();
    private readonly FakeDashboardRepository _dashboardRepo = new();

    private readonly RecommendationHistoryService _historyService;
    private readonly DashboardService _dashboardService;

    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _parentId = Guid.NewGuid();

    public DashboardServiceTests()
    {
        _historyService = new RecommendationHistoryService(_historyRepo, _linkRepo);
        _dashboardService = new DashboardService(
            _historyService,
            _flowService,
            _targetSchoolService,
            _dashboardRepo,
            _linkRepo,
            _profileRepo,
            _userRepo
        );

        _userRepo.Users.Add(new User
        {
            Id = _studentId,
            Email = "student@test.com",
            FullName = "Nguyễn Văn Học Sinh",
            Role = UserRoles.Student,
            Status = UserStatuses.Active
        });

        _userRepo.Users.Add(new User
        {
            Id = _parentId,
            Email = "parent@test.com",
            FullName = "Nguyễn Văn Phụ Huynh",
            Role = UserRoles.Parent,
            Status = UserStatuses.Active
        });
    }

    [Fact]
    public async Task GetStudentDashboard_WhenNoRuns_ReturnsHasRunFalse()
    {
        var result = await _dashboardService.GetStudentDashboardAsync(_studentId);

        Assert.NotNull(result);
        Assert.False(result.Recommendation.HasRun);
        Assert.Equal(0, result.Recommendation.Total);
        Assert.Contains("Chưa chạy gợi ý trường nào", result.Recommendation.Explanation);
    }

    [Fact]
    public async Task GetStudentDashboard_WhenRunWithSchools_ReturnsAccurateCounts()
    {
        var run = AddRecommendationRun(_studentId, DateTime.UtcNow,
            reach: ["MIT", "HARVARD"],
            match: ["NYU"],
            safety: ["ASU"],
            aid: ["AMHERST"]
        );

        var result = await _dashboardService.GetStudentDashboardAsync(_studentId);

        Assert.True(result.Recommendation.HasRun);
        Assert.Equal(2, result.Recommendation.Reach);
        Assert.Equal(1, result.Recommendation.Match);
        Assert.Equal(1, result.Recommendation.Safety);
        Assert.Equal(1, result.Recommendation.Aid);
        Assert.Equal(5, result.Recommendation.Total);
        Assert.NotNull(result.Recommendation.RunAt);
    }

    [Fact]
    public async Task GetStudentDashboard_ProfileProgress_ReflectsFlowState()
    {
        _flowService.State = new ProfileFlowStateDto(
            CurrentStep: 4,
            CompletedSteps: [1, 2, 3],
            Draft: new ProfileFlowDraftDto(),
            DraftTranscript: null,
            LastUpdatedAt: null,
            HasCommittedProfile: false
        );

        var result = await _dashboardService.GetStudentDashboardAsync(_studentId);

        Assert.Equal(3, result.ProfileProgress.CompletedSteps);
        Assert.Equal(5, result.ProfileProgress.TotalSteps);
        Assert.False(result.ProfileProgress.HasCommittedProfile);
        Assert.Contains("3/5", result.ProfileProgress.Explanation);
    }

    [Fact]
    public async Task GetStudentDashboard_UpcomingDeadlines_FiltersFutureDatesAndSortsAscending()
    {
        var today = DateTime.UtcNow.Date;
        var pastSchool = CreateTargetSchool(Guid.NewGuid(), "PAST", today.AddDays(-5));
        var soonSchool = CreateTargetSchool(Guid.NewGuid(), "SOON", today.AddDays(2));
        var laterSchool = CreateTargetSchool(Guid.NewGuid(), "LATER", today.AddDays(15));
        var undecidedSchool = CreateTargetSchool(Guid.NewGuid(), "UNDECIDED", null);

        _targetSchoolService.Summary = new TargetSchoolSummaryDto(
            [pastSchool, soonSchool, laterSchool, undecidedSchool],
            TotalCount: 4, ReachCount: 1, MatchCount: 2, SafetyCount: 1, UndecidedCategoryCount: 1, NextDeadline: today.AddDays(2)
        );

        var result = await _dashboardService.GetStudentDashboardAsync(_studentId);

        Assert.Equal(2, result.UpcomingDeadlines.Upcoming.Count);
        Assert.Equal("SOON", result.UpcomingDeadlines.Upcoming[0].UniversityCode);
        Assert.Equal("LATER", result.UpcomingDeadlines.Upcoming[1].UniversityCode);
        Assert.Equal(1, result.UpcomingDeadlines.UndecidedRoundCount);
        Assert.Equal(4, result.UpcomingDeadlines.TotalTargetSchools);
    }

    [Fact]
    public async Task GetStudentDashboard_DataCompleteness_Detects5ItemsCorrectly()
    {
        // Khi hồ sơ chỉ có GPA và Ngành học
        _flowService.State = new ProfileFlowStateDto(
            CurrentStep: 2,
            CompletedSteps: [1],
            Draft: new ProfileFlowDraftDto(
                Step1: new FlowGoalDraftDto(TargetMajor: "Computer Science", MajorUndecided: false),
                Step2: new FlowTranscriptDraftDto(CalculatedGpa4Scale: 3.8m)
            ),
            DraftTranscript: null,
            LastUpdatedAt: null,
            HasCommittedProfile: false
        );

        var result = await _dashboardService.GetStudentDashboardAsync(_studentId);

        Assert.Equal(2, result.DataCompleteness.FilledCount);
        Assert.Equal(5, result.DataCompleteness.TotalCount);
        Assert.Equal(3, result.DataCompleteness.MissingLabels.Count);
        Assert.True(result.DataCompleteness.Items.First(i => i.Key == "gpa").IsFilled);
        Assert.True(result.DataCompleteness.Items.First(i => i.Key == "major").IsFilled);
        Assert.False(result.DataCompleteness.Items.First(i => i.Key == "english").IsFilled);
        Assert.False(result.DataCompleteness.Items.First(i => i.Key == "standardized_test").IsFilled);
        Assert.False(result.DataCompleteness.Items.First(i => i.Key == "budget").IsFilled);
    }

    [Fact]
    public async Task GetParentChildDashboard_WhenLinkDoesNotExist_ReturnsNull()
    {
        var result = await _dashboardService.GetParentChildDashboardAsync(_parentId, _studentId);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetParentChildDashboard_WhenLinkIsRevoked_ReturnsNull()
    {
        _linkRepo.Links.Add(new ParentStudentLink
        {
            StudentUserId = _studentId,
            ParentUserId = _parentId,
            ParentEmail = "parent@test.com",
            Status = ParentLinkStatuses.Revoked
        });

        var result = await _dashboardService.GetParentChildDashboardAsync(_parentId, _studentId);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetParentChildDashboard_PrefersParentBudgetForNormalChild()
    {
        _linkRepo.Links.Add(new ParentStudentLink
        {
            StudentUserId = _studentId,
            ParentUserId = _parentId,
            ParentEmail = "parent@test.com",
            Status = ParentLinkStatuses.Active,
            ParentPreferenceJson = "{\"budgetUsd\": 45000}"
        });

        _profileRepo.Profiles[_studentId] = new StudentProfile
        {
            UserId = _studentId,
            BudgetMaxUsd = 30000
        };

        var result = await _dashboardService.GetParentChildDashboardAsync(_parentId, _studentId);

        Assert.NotNull(result);
        Assert.Equal(45000m, result.FinancialKpis.BudgetPerYear);
        Assert.Equal("parent_preference", result.FinancialKpis.BudgetSource);
        Assert.Contains("45,000", result.FinancialKpis.BudgetSourceLabel);
    }

    [Fact]
    public async Task GetParentChildDashboard_ManagedChild_UsesStudentProfileTier()
    {
        var managedStudentId = Guid.NewGuid();
        _userRepo.Users.Add(new User
        {
            Id = managedStudentId,
            Email = "managed@test.com",
            FullName = "Con Được Quản Lý",
            Role = UserRoles.Student,
            Status = UserStatuses.Managed
        });

        _linkRepo.Links.Add(new ParentStudentLink
        {
            StudentUserId = managedStudentId,
            ParentUserId = _parentId,
            ParentEmail = "parent@test.com",
            Status = ParentLinkStatuses.Active,
            ParentPreferenceJson = "{\"budgetUsd\": 60000}" // Dù có preference cũng bỏ qua đối với managed
        });

        _profileRepo.Profiles[managedStudentId] = new StudentProfile
        {
            UserId = managedStudentId,
            BudgetMaxUsd = 35000
        };

        var result = await _dashboardService.GetParentChildDashboardAsync(_parentId, managedStudentId);

        Assert.NotNull(result);
        Assert.True(result.IsManaged);
        Assert.Equal(35000m, result.FinancialKpis.BudgetPerYear);
        Assert.Equal("student_profile_tier", result.FinancialKpis.BudgetSource);
    }

    [Fact]
    public async Task GetParentChildDashboard_CalculatesSchoolShortfallAndTotals()
    {
        _linkRepo.Links.Add(new ParentStudentLink
        {
            StudentUserId = _studentId,
            ParentUserId = _parentId,
            ParentEmail = "parent@test.com",
            Status = ParentLinkStatuses.Active,
            ParentPreferenceJson = "{\"budgetUsd\": 40000}" // 4 năm = 160,000
        });

        var uniExpensive = Guid.NewGuid();
        var uniCheap = Guid.NewGuid();
        var uniNoCost = Guid.NewGuid();

        var school1 = CreateTargetSchool(uniExpensive, "MIT", DateTime.UtcNow.AddDays(10));
        var school2 = CreateTargetSchool(uniCheap, "SUNY", DateTime.UtcNow.AddDays(20));
        var school3 = CreateTargetSchool(uniNoCost, "UNKNOWN", null);

        _targetSchoolService.Summary = new TargetSchoolSummaryDto(
            [school1, school2, school3],
            TotalCount: 3, ReachCount: 1, MatchCount: 1, SafetyCount: 1, UndecidedCategoryCount: 1, NextDeadline: null
        );

        // MIT: 50,000/năm -> 4 năm = 200,000 (thiếu 40,000)
        _dashboardRepo.Costs[uniExpensive] = new UniversityCostRow(uniExpensive, 40000, 10000, 0, 0);

        // SUNY: 30,000/năm -> 4 năm = 120,000 (thiếu 0, trong ngân sách)
        _dashboardRepo.Costs[uniCheap] = new UniversityCostRow(uniCheap, 20000, 10000, 0, 0);

        // UNKNOWN: không có trong _dashboardRepo.Costs

        var result = await _dashboardService.GetParentChildDashboardAsync(_parentId, _studentId);

        Assert.NotNull(result);
        var fin = result.FinancialKpis;
        Assert.Equal(1, fin.SchoolsWithinBudgetCount); // SUNY
        Assert.Equal(2, fin.SchoolsTotalWithCostCount);
        Assert.Equal(1, fin.SchoolsMissingCostCount);
        Assert.Equal(320000m, fin.TotalCost4YearsAllSchools); // 200k + 120k

        Assert.Equal(3, fin.ShortfallList.Count);

        var mitShortfall = fin.ShortfallList.First(s => s.UniversityId == uniExpensive);
        Assert.True(mitShortfall.HasCostData);
        Assert.Equal(50000m, mitShortfall.TotalCost1Year);
        Assert.Equal(200000m, mitShortfall.TotalCost4Years);
        Assert.Equal(40000m, mitShortfall.Shortfall4Years);

        var sunyShortfall = fin.ShortfallList.First(s => s.UniversityId == uniCheap);
        Assert.True(sunyShortfall.HasCostData);
        Assert.Equal(30000m, sunyShortfall.TotalCost1Year);
        Assert.Equal(120000m, sunyShortfall.TotalCost4Years);
        Assert.Equal(0m, sunyShortfall.Shortfall4Years);

        Assert.NotNull(fin.MostShortfallSchool);
        Assert.Equal("MIT", fin.MostShortfallSchool!.UniversityCode);
        Assert.NotNull(fin.LeastShortfallSchool);
        Assert.Equal("SUNY", fin.LeastShortfallSchool!.UniversityCode);
    }

    // ========================================================================
    // HELPER & FAKES
    // ========================================================================

    private Recommendation AddRecommendationRun(
        Guid userId, DateTime createdAt,
        string[]? reach = null, string[]? match = null, string[]? safety = null, string[]? aid = null)
    {
        static object Block(string[]? codes) => (codes ?? []).Select(c => new { code = c, x = 0.5, why = Array.Empty<string>() }).ToList();
        var all = new[] { reach, match, safety, aid }.SelectMany(b => b ?? []).Distinct();
        var row = new Recommendation
        {
            UserId = userId,
            CreatedAt = createdAt,
            StudyLevel = "undergraduate",
            CriteriaJson = "{\"major\":\"CS\",\"pref\":[2,2,2]}",
            ItemsJson = JsonSerializer.Serialize(new
            {
                scored = all.Select(c => new { code = c, name = "Trường " + c }).ToList(),
                failed = Array.Empty<string>(),
                reach = Block(reach),
                match = Block(match),
                safety = Block(safety),
                aid = Block(aid),
                other = Block(null),
            }),
        };
        _historyRepo.Rows.Add(row);
        return row;
    }

    private static TargetSchoolDto CreateTargetSchool(Guid uniId, string uniCode, DateTime? deadline) =>
        new(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UniversityId: uniId,
            UniversityCode: uniCode,
            UniversityName: $"University {uniCode}",
            City: "City",
            State: "CA",
            Website: "https://example.com",
            Control: "public",
            AcceptanceRate: 0.2m,
            Category: "reach",
            Status: "considering",
            Priority: 0,
            Notes: null,
            ApplicationRound: "Regular Decision",
            Deadline: deadline,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null
        );

    private sealed class FakeAcademicProfileRepo : IAcademicProfileRepository
    {
        public readonly Dictionary<Guid, StudentProfile> Profiles = new();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Profiles.TryGetValue(userId, out var p) ? p : null);

        public Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid studentProfileId, CancellationToken ct = default) =>
            Task.FromResult(new List<TranscriptScore>());

        public Task<List<ProfileCertification>> GetCertificationsAsync(Guid studentProfileId, CancellationToken ct = default) =>
            Task.FromResult(new List<ProfileCertification>());
    }

    private sealed class FakeProfileFlowService : IProfileFlowService
    {
        public ProfileFlowStateDto State { get; set; } = new(1, [], new ProfileFlowDraftDto(), null, null, false);
        public ProfileFlowDraftDto? Committed { get; set; }

        public Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(State);
        public Task<ProfileFlowDraftDto?> GetCommittedDetailsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Committed);
        public Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ProfileFlowStateDto> PatchDraftAsync(Guid userId, System.Text.Json.Nodes.JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(Guid userId, System.IO.Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearDraftAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTargetSchoolService : ITargetSchoolService
    {
        public TargetSchoolSummaryDto Summary { get; set; } = new([], 0, 0, 0, 0, 0, null);

        public Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default) =>
            Task.FromResult(Summary);

        public Task<AddTargetSchoolResult> AddTargetSchoolAsync(Guid userId, AddTargetSchoolRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchoolDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> RemoveTargetSchoolAsync(Guid userId, Guid targetSchoolId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> RemoveTargetSchoolByUniversityAsync(Guid userId, string universityIdentifier, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchoolDto?> CheckTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(Guid userId, Guid targetSchoolId, UpdateApplicationRoundRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeDashboardRepository : IDashboardRepository
    {
        public readonly Dictionary<Guid, UniversityCostRow> Costs = new();

        public Task<IReadOnlyDictionary<Guid, UniversityCostRow>> GetUniversityCostsAsync(
            IEnumerable<Guid> universityIds,
            string level = "undergraduate",
            CancellationToken ct = default)
        {
            var dict = universityIds
                .Distinct()
                .Where(Costs.ContainsKey)
                .ToDictionary(id => id, id => Costs[id]);

            return Task.FromResult<IReadOnlyDictionary<Guid, UniversityCostRow>>(dict);
        }
    }
}
