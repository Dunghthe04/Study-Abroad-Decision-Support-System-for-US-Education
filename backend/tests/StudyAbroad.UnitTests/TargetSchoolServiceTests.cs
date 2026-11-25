using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class TargetSchoolServiceTests
{
    private readonly FakeTargetSchoolRepository _repo = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly TargetSchoolService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _uniId = Guid.NewGuid();

    public TargetSchoolServiceTests()
    {
        _service = new TargetSchoolService(_repo, _auditLogs);

        // Chuẩn bị sẵn một trường đại học hợp lệ trong repo
        _repo.Universities.Add(new University
        {
            Id = _uniId,
            Code = "MIT",
            Name = "Massachusetts Institute of Technology",
            City = "Cambridge",
            State = "MA",
            Website = "https://www.mit.edu",
            Control = "private",
            AcceptanceRate = 0.04m,
            IsActive = true,
        });

        _repo.DeadlinesJsonMap[_uniId] = "{\"EA\": \"2026-11-01\", \"RD\": \"2027-01-05\"}";
    }

    [Fact]
    public async Task AddTargetSchool_WhenValid_AddsSuccessfullyAndReturnsDto()
    {
        var request = new AddTargetSchoolRequest(
            UniversityId: _uniId,
            Category: "reach",
            ApplicationRound: "Early Action",
            Deadline: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes: "Trường mơ ước hàng đầu"
        );

        var result = await _service.AddTargetSchoolAsync(_userId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.TargetSchool);
        Assert.Equal(_userId, result.TargetSchool.UserId);
        Assert.Equal(_uniId, result.TargetSchool.UniversityId);
        Assert.Equal("MIT", result.TargetSchool.UniversityCode);
        Assert.Equal("Massachusetts Institute of Technology", result.TargetSchool.UniversityName);
        Assert.Equal("reach", result.TargetSchool.Category);
        Assert.Equal("considering", result.TargetSchool.Status);
        Assert.Equal("Early Action", result.TargetSchool.ApplicationRound);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), result.TargetSchool.Deadline);
        Assert.Equal("Trường mơ ước hàng đầu", result.TargetSchool.Notes);

        // Kiểm tra đã ghi Audit Log
        Assert.Single(_auditLogs.Entries);
        var audit = _auditLogs.Entries[0];
        Assert.Equal("target_school.add", audit.Action);
        Assert.Equal("target_school", audit.EntityType);
        Assert.Equal(_userId, audit.ActorUserId);
    }

    [Fact]
    public async Task AddTargetSchool_WhenCategoryNull_DefaultsFromLatestRecommendationHistory()
    {
        var fakeRecRepo = new FakeRecommendationHistoryRepository();
        fakeRecRepo.Rows.Add(new Recommendation
        {
            UserId = _userId,
            ItemsJson = """{"reach":[{"code":"MIT"}],"match":[],"safety":[]}"""
        });
        var serviceWithRec = new TargetSchoolService(_repo, _auditLogs, fakeRecRepo);

        var request = new AddTargetSchoolRequest(
            UniversityId: _uniId,
            Category: null
        );

        var result = await serviceWithRec.AddTargetSchoolAsync(_userId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.TargetSchool);
        Assert.Equal("reach", result.TargetSchool.Category);
    }

    [Fact]
    public async Task AddTargetSchool_WhenUniversityNotFound_ReturnsError()
    {
        var nonExistentId = Guid.NewGuid();
        var request = new AddTargetSchoolRequest(UniversityId: nonExistentId);

        var result = await _service.AddTargetSchoolAsync(_userId, request);

        Assert.False(result.Success);
        Assert.Contains("không tồn tại", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task AddTargetSchool_WhenDuplicate_ReturnsError()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);

        var first = await _service.AddTargetSchoolAsync(_userId, request);
        Assert.True(first.Success);

        var second = await _service.AddTargetSchoolAsync(_userId, request);
        Assert.False(second.Success);
        Assert.Contains("đã có trong danh sách mục tiêu", second.ErrorMessage);
    }

    [Fact]
    public async Task AddTargetSchool_WithInvalidCategory_ReturnsError()
    {
        var request = new AddTargetSchoolRequest(
            UniversityId: _uniId,
            Category: "invalid_category"
        );

        var result = await _service.AddTargetSchoolAsync(_userId, request);

        Assert.False(result.Success);
        Assert.Contains("reach", result.ErrorMessage);
    }

    [Fact]
    public async Task AddTargetSchool_WithoutDeadline_ExtractsFromDeadlinesJson()
    {
        var request = new AddTargetSchoolRequest(
            UniversityId: _uniId,
            ApplicationRound: "Early Action",
            Deadline: null
        );

        var result = await _service.AddTargetSchoolAsync(_userId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.TargetSchool);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), result.TargetSchool.Deadline);
    }

    [Fact]
    public async Task AddTargetSchool_WhenUsingUniversityCode_AddsSuccessfully()
    {
        var request = new AddTargetSchoolRequest(
            UniversityId: null,
            UniversityCode: "MIT",
            Category: "reach"
        );

        var result = await _service.AddTargetSchoolAsync(_userId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.TargetSchool);
        Assert.Equal(_uniId, result.TargetSchool.UniversityId);
        Assert.Equal("MIT", result.TargetSchool.UniversityCode);
        Assert.Equal("reach", result.TargetSchool.Category);
    }

    [Fact]
    public async Task GetById_WhenOwnedByUser_ReturnsDto()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);
        var addResult = await _service.AddTargetSchoolAsync(_userId, request);
        var id = addResult.TargetSchool!.Id;

        var dto = await _service.GetByIdAsync(id, _userId);

        Assert.NotNull(dto);
        Assert.Equal(id, dto.Id);
    }

    [Fact]
    public async Task GetById_WhenDifferentUser_ReturnsNull()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);
        var addResult = await _service.AddTargetSchoolAsync(_userId, request);
        var id = addResult.TargetSchool!.Id;

        var otherUser = Guid.NewGuid();
        var dto = await _service.GetByIdAsync(id, otherUser);

        Assert.Null(dto);
    }

    [Fact]
    public async Task RemoveTargetSchool_WhenExistsAndOwned_DeletesAndLogsAudit()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach");
        var addResult = await _service.AddTargetSchoolAsync(_userId, request);
        var id = addResult.TargetSchool!.Id;

        var removed = await _service.RemoveTargetSchoolAsync(_userId, id);

        Assert.True(removed);
        Assert.Empty(_repo.TargetSchools);
        Assert.Contains(_auditLogs.Entries, a => a.Action == "target_school.remove" && a.EntityId == id.ToString() && a.ActorUserId == _userId);
    }

    [Fact]
    public async Task RemoveTargetSchool_WhenDifferentUser_ReturnsFalseAndDoesNotDelete()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);
        var addResult = await _service.AddTargetSchoolAsync(_userId, request);
        var id = addResult.TargetSchool!.Id;

        var otherUser = Guid.NewGuid();
        var removed = await _service.RemoveTargetSchoolAsync(otherUser, id);

        Assert.False(removed);
        Assert.Single(_repo.TargetSchools);
    }

    [Fact]
    public async Task RemoveTargetSchool_WhenNotFound_ReturnsFalse()
    {
        var removed = await _service.RemoveTargetSchoolAsync(_userId, Guid.NewGuid());
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveTargetSchoolByUniversity_WhenByCode_DeletesSuccessfully()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);
        await _service.AddTargetSchoolAsync(_userId, request);

        var removed = await _service.RemoveTargetSchoolByUniversityAsync(_userId, "MIT");

        Assert.True(removed);
        Assert.Empty(_repo.TargetSchools);
    }

    [Fact]
    public async Task RemoveTargetSchoolByUniversity_WhenById_DeletesSuccessfully()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId);
        await _service.AddTargetSchoolAsync(_userId, request);

        var removed = await _service.RemoveTargetSchoolByUniversityAsync(_userId, _uniId.ToString());

        Assert.True(removed);
        Assert.Empty(_repo.TargetSchools);
    }

    [Fact]
    public async Task CheckTargetSchool_WhenSaved_ReturnsDto()
    {
        var request = new AddTargetSchoolRequest(UniversityId: _uniId, Category: "match");
        await _service.AddTargetSchoolAsync(_userId, request);

        var dto = await _service.CheckTargetSchoolAsync(_userId, "MIT", null);

        Assert.NotNull(dto);
        Assert.Equal("MIT", dto.UniversityCode);
        Assert.Equal("match", dto.Category);
    }

    [Fact]
    public async Task CheckTargetSchool_WhenNotSaved_ReturnsNull()
    {
        var dto = await _service.CheckTargetSchoolAsync(_userId, "MIT", null);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetTargetSchools_WhenEmpty_ReturnsZeroCountsAndEmptyList()
    {
        var summary = await _service.GetTargetSchoolsAsync(_userId);
        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(0, summary.ReachCount);
        Assert.Equal(0, summary.MatchCount);
        Assert.Equal(0, summary.SafetyCount);
        Assert.Null(summary.NextDeadline);
        Assert.Empty(summary.Items);
    }

    [Fact]
    public async Task GetTargetSchools_WithMultipleSchools_ComputesCountsAndNextDeadline()
    {
        // Thêm 1 trường Reach (hạn tương lai)
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(
            UniversityId: _uniId,
            Category: "reach",
            ApplicationRound: "Regular Decision",
            Deadline: DateTime.UtcNow.AddMonths(1)
        ));

        // Thêm 1 trường Match
        var uniMatchId = Guid.NewGuid();
        _repo.Universities.Add(new University { Id = uniMatchId, Code = "STANFORD", Name = "Stanford", IsActive = true });
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(
            UniversityId: uniMatchId,
            Category: "match",
            ApplicationRound: "Early Action",
            Deadline: DateTime.UtcNow.AddMonths(2)
        ));

        // Thêm 1 trường Safety
        var uniSafetyId = Guid.NewGuid();
        _repo.Universities.Add(new University { Id = uniSafetyId, Code = "UCI", Name = "UC Irvine", IsActive = true });
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(
            UniversityId: uniSafetyId,
            Category: "safety"
        ));

        var summary = await _service.GetTargetSchoolsAsync(_userId);

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(1, summary.ReachCount);
        Assert.Equal(1, summary.MatchCount);
        Assert.Equal(1, summary.SafetyCount);
        Assert.NotNull(summary.NextDeadline);
        Assert.Equal(3, summary.Items.Count);
    }

    [Fact]
    public async Task GetTargetSchools_WithCategoryFilter_FiltersItemsWhileKeepingCounts()
    {
        // 1 reach, 1 safety
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));

        var uniSafetyId = Guid.NewGuid();
        _repo.Universities.Add(new University { Id = uniSafetyId, Code = "SJSU", Name = "San Jose State", IsActive = true });
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: uniSafetyId, Category: "safety"));

        var summary = await _service.GetTargetSchoolsAsync(_userId, category: "reach");

        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(1, summary.ReachCount);
        Assert.Equal(1, summary.SafetyCount);
        Assert.Single(summary.Items);
        Assert.Equal("reach", summary.Items[0].Category);
    }

    [Fact]
    public async Task GetTargetSchools_OnlyReturnsCurrentUsersSchools()
    {
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));

        var otherUser = Guid.NewGuid();
        var uniOther = Guid.NewGuid();
        _repo.Universities.Add(new University { Id = uniOther, Code = "HARVARD", Name = "Harvard", IsActive = true });
        await _service.AddTargetSchoolAsync(otherUser, new AddTargetSchoolRequest(UniversityId: uniOther, Category: "match"));

        var userSummary = await _service.GetTargetSchoolsAsync(_userId);
        Assert.Equal(1, userSummary.TotalCount);
        Assert.Equal("MIT", userSummary.Items[0].UniversityCode);

        var otherSummary = await _service.GetTargetSchoolsAsync(otherUser);
        Assert.Equal(1, otherSummary.TotalCount);
        Assert.Equal("HARVARD", otherSummary.Items[0].UniversityCode);
    }

    [Fact]
    public async Task UpdateApplicationRound_Success_UpdatesRoundAndDeadlineAndWritesAudit()
    {
        // 1. Thêm trường mục tiêu ban đầu
        _repo.DeadlinesJsonMap[_uniId] = "{\"EA\":\"2026-11-01\",\"ED\":\"2026-11-15\",\"RD\":\"2027-01-05\"}";
        var addResult = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));
        Assert.True(addResult.Success);
        var tsId = addResult.TargetSchool!.Id;

        // 2. Chọn vòng nộp Early Decision (ED) và tự động khớp deadline
        var updateResult = await _service.UpdateApplicationRoundAsync(_userId, tsId, new UpdateApplicationRoundRequest("Early Decision", AutoResolveDeadline: true));

        Assert.True(updateResult.Success);
        Assert.NotNull(updateResult.TargetSchool);
        Assert.Equal("Early Decision", updateResult.TargetSchool.ApplicationRound);
        Assert.NotNull(updateResult.TargetSchool.Deadline);
        Assert.Equal(2026, updateResult.TargetSchool.Deadline.Value.Year);
        Assert.Equal(11, updateResult.TargetSchool.Deadline.Value.Month);
        Assert.Equal(15, updateResult.TargetSchool.Deadline.Value.Day);

        // 3. Kiểm tra Audit log
        Assert.Contains(_auditLogs.Entries, a => a.Action == "target_school.update_round" && a.EntityId == tsId.ToString());
    }

    [Fact]
    public async Task UpdateApplicationRound_OtherUser_ReturnsFailAntiIdor()
    {
        var addResult = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));
        var tsId = addResult.TargetSchool!.Id;

        var otherUser = Guid.NewGuid();
        var updateResult = await _service.UpdateApplicationRoundAsync(otherUser, tsId, new UpdateApplicationRoundRequest("Regular Decision"));

        Assert.False(updateResult.Success);
        Assert.Contains("Không tìm thấy trường mục tiêu trong danh sách của bạn", updateResult.ErrorMessage);
    }

    [Fact]
    public async Task UpdateApplicationRound_EmptyRound_ReturnsFail()
    {
        var addResult = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));
        var tsId = addResult.TargetSchool!.Id;

        var updateResult = await _service.UpdateApplicationRoundAsync(_userId, tsId, new UpdateApplicationRoundRequest("   "));

        Assert.False(updateResult.Success);
        Assert.Contains("Vui lòng chọn vòng nộp hồ sơ", updateResult.ErrorMessage);
    }

    // Phụ huynh gợi ý (Status parent_suggested): chưa là trường của con cho tới khi con thêm.
    private TargetSchool AddParentSuggestion(string? note = "Bố mẹ thấy trường này hợp")
    {
        var row = new TargetSchool { UserId = _userId, UniversityId = _uniId, Status = "parent_suggested", Notes = note };
        _repo.TargetSchools.Add(row);
        return row;
    }

    [Fact]
    public async Task GetTargetSchools_ParentSuggestion_NotCountedButReturnedWhenAskedByStatus()
    {
        AddParentSuggestion();

        var main = await _service.GetTargetSchoolsAsync(_userId);
        Assert.Empty(main.Items);
        Assert.Equal(0, main.TotalCount);
        Assert.Equal(0, main.UndecidedCategoryCount);

        var suggestions = await _service.GetTargetSchoolsAsync(_userId, status: "parent_suggested");
        Assert.Equal("MIT", Assert.Single(suggestions.Items).UniversityCode);
    }

    [Fact]
    public async Task AddTargetSchool_WhenParentSuggested_AcceptsSameRowKeepingNote()
    {
        var row = AddParentSuggestion();

        var result = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, Category: "reach"));

        Assert.True(result.Success);
        Assert.Equal(row.Id, result.TargetSchool!.Id);                 // dùng lại dòng gợi ý, không thêm dòng mới
        Assert.Single(_repo.TargetSchools);
        Assert.Equal("considering", result.TargetSchool.Status);
        Assert.Equal("reach", result.TargetSchool.Category);
        Assert.Equal("Bố mẹ thấy trường này hợp", result.TargetSchool.Notes);
        Assert.Equal(1, (await _service.GetTargetSchoolsAsync(_userId)).TotalCount);
    }

    [Fact]
    public async Task CheckTargetSchool_WhenOnlyParentSuggested_ReturnsNull()
    {
        AddParentSuggestion();

        Assert.Null(await _service.CheckTargetSchoolAsync(_userId, "MIT", null));
        Assert.Null(await _service.CheckTargetSchoolAsync(_userId, null, _uniId));
    }

    [Fact]
    public async Task AddTargetSchool_WhenRoundNotSupportedBySchool_ReturnsFail()
    {
        // MIT trong setup chỉ có EA và RD, không có ED
        var result = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(
            UniversityId: _uniId,
            ApplicationRound: "Early Decision"
        ));

        Assert.False(result.Success);
        Assert.Contains("không hỗ trợ vòng nộp", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateApplicationRound_WhenRoundNotSupported_ReturnsFail()
    {
        var added = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId));
        Assert.True(added.Success);

        var updated = await _service.UpdateApplicationRoundAsync(_userId, added.TargetSchool!.Id, new UpdateApplicationRoundRequest("Early Decision"));

        Assert.False(updated.Success);
        Assert.False(updated.NotFound);
        Assert.Contains("không hỗ trợ vòng nộp", updated.ErrorMessage);
    }

    [Fact]
    public async Task UpdateApplicationRound_WhenNotFound_ReturnsNotFoundFlag()
    {
        var updated = await _service.UpdateApplicationRoundAsync(_userId, Guid.NewGuid(), new UpdateApplicationRoundRequest("Regular Decision"));

        Assert.False(updated.Success);
        Assert.True(updated.NotFound);
    }

    [Fact]
    public async Task AddAndList_InheritsLatestRecommendedCategoryUntilUserOverrides()
    {
        _repo.RecommendedCategories[_userId] = new Dictionary<Guid, string> { [_uniId] = "safety" };

        var added = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId));
        Assert.True(added.Success);
        Assert.Equal("safety", added.TargetSchool!.Category);
        Assert.False(added.TargetSchool.IsCategoryUserSelected);

        // Khi có gợi ý mới đổi nhóm thành reach, danh sách tự cập nhật nếu người dùng chưa đổi tay
        _repo.RecommendedCategories[_userId][_uniId] = "reach";
        var summary = await _service.GetTargetSchoolsAsync(_userId);
        Assert.Equal("reach", summary.Items[0].Category);
        Assert.Equal(1, summary.ReachCount);

        // Khi người dùng tự đổi nhóm sang match, đánh dấu IsCategoryUserSelected = true và không bị gợi ý ghi đè
        var changed = await _service.UpdateCategoryAsync(_userId, added.TargetSchool.Id, new UpdateTargetCategoryRequest("match"));
        Assert.True(changed.Success);
        Assert.Equal("match", changed.TargetSchool!.Category);
        Assert.True(changed.TargetSchool.IsCategoryUserSelected);

        var afterOverride = await _service.GetTargetSchoolsAsync(_userId);
        Assert.Equal("match", afterOverride.Items[0].Category);
        Assert.True(afterOverride.Items[0].IsCategoryUserSelected);
        Assert.Equal(1, afterOverride.MatchCount);
    }

    [Fact]
    public async Task GetDefaults_ReturnsRecommendedCategoryAndSupportedRounds()
    {
        _repo.RecommendedCategories[_userId] = new Dictionary<Guid, string> { [_uniId] = "match" };
        _repo.DeadlinesJsonMap[_uniId] = "{\"RD\":\"2027-01-05\",\"RollingFinal\":\"2027-03-01\"}";

        var defaults = await _service.GetDefaultsAsync(_userId, "MIT", null);

        Assert.Equal("match", defaults.RecommendedCategory);
        Assert.Equal(["Regular Decision", "Rolling"], defaults.AvailableRounds);
    }

    [Fact]
    public async Task ExtractAvailableRounds_SupportsEd2AndReaAndScea()
    {
        var rounds = TargetSchoolService.ExtractAvailableRounds(
            "{\"ED\":\"2026-11-01\",\"ED2\":\"2027-01-01\",\"EA\":\"2026-11-01\",\"REA\":\"2026-11-01\",\"RD\":\"2027-01-15\",\"Rolling\":\"2027-05-01\"}");

        Assert.Equal(
            ["Early Decision", "Early Decision II", "Early Action", "Restrictive Early Action", "Regular Decision", "Rolling"],
            rounds);
    }

    [Fact]
    public async Task UpdateApplicationRound_SecondEdOrEd2School_RejectedWithConflictingSchoolName()
    {
        var uni2 = Guid.NewGuid();
        _repo.Universities.Add(new University { Id = uni2, Code = "NYU", Name = "New York University", IsActive = true });
        _repo.DeadlinesJsonMap[_uniId] = "{\"ED\":\"2026-11-01\",\"ED2\":\"2027-01-01\",\"RD\":\"2027-01-05\"}";
        _repo.DeadlinesJsonMap[uni2] = "{\"ED\":\"2026-11-01\",\"ED2\":\"2027-01-01\",\"RD\":\"2027-01-05\"}";

        var first = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, ApplicationRound: "Early Decision"));
        Assert.True(first.Success);

        // E1: trường thứ 2 không được chọn ED khi đã có MIT ở ED
        var secondAdd = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: uni2, ApplicationRound: "Early Decision"));
        Assert.False(secondAdd.Success);
        Assert.Contains("Massachusetts Institute of Technology", secondAdd.ErrorMessage);

        // Trường thứ 2 chọn ED II thì được phép (mỗi vòng ED và ED II tối đa 1 trường riêng biệt)
        var secondEd2 = await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: uni2, ApplicationRound: "Early Decision II"));
        Assert.True(secondEd2.Success);
        Assert.Equal("Early Decision II", secondEd2.TargetSchool!.ApplicationRound);

        // Chuyển MIT sang ED II bị chặn vì NYU đang giữ ED II
        var mitToEd2 = await _service.UpdateApplicationRoundAsync(_userId, first.TargetSchool!.Id, new UpdateApplicationRoundRequest("Early Decision II"));
        Assert.False(mitToEd2.Success);
        Assert.Contains("New York University", mitToEd2.ErrorMessage);
    }

    [Fact]
    public async Task GetDefaults_AndSummary_ExposeMandatoryScholarshipAndExistingBindingSchools()
    {
        _repo.ScholarshipNeeds[_userId] = "mandatory";
        _repo.DeadlinesJsonMap[_uniId] = "{\"ED\":\"2026-11-01\",\"RD\":\"2027-01-05\"}";
        await _service.AddTargetSchoolAsync(_userId, new AddTargetSchoolRequest(UniversityId: _uniId, ApplicationRound: "Early Decision"));

        var defaults = await _service.GetDefaultsAsync(_userId, "MIT", null);
        Assert.True(defaults.IsScholarshipMandatory);
        Assert.Equal("Massachusetts Institute of Technology", defaults.ExistingEdSchoolName);
        Assert.Null(defaults.ExistingEd2SchoolName);

        var summary = await _service.GetTargetSchoolsAsync(_userId);
        Assert.True(summary.IsScholarshipMandatory);
    }

    private sealed class FakeTargetSchoolRepository : ITargetSchoolRepository
    {
        public List<TargetSchool> TargetSchools { get; } = [];
        public List<University> Universities { get; } = [];
        public Dictionary<Guid, string> DeadlinesJsonMap { get; } = [];
        public Dictionary<Guid, Dictionary<Guid, string>> RecommendedCategories { get; } = [];

        public Task<Dictionary<Guid, string>> GetLatestRecommendedCategoriesAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(RecommendedCategories.TryGetValue(userId, out var map) ? new Dictionary<Guid, string>(map) : []);

        public Task<string?> GetLatestRecommendedCategoryAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(RecommendedCategories.TryGetValue(userId, out var map) && map.TryGetValue(universityId, out var cat) ? cat : null);

        public Dictionary<Guid, string?> ScholarshipNeeds { get; } = [];

        public Task<string?> GetUserScholarshipNeedAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(ScholarshipNeeds.TryGetValue(userId, out var need) ? need : null);

        public Task<string?> FindSchoolWithRoundAsync(Guid userId, string canonicalRound, Guid? excludeTargetId = null, CancellationToken ct = default)
        {
            var hit = TargetSchools.FirstOrDefault(ts =>
                ts.UserId == userId
                && ts.Status != "parent_suggested"
                && ts.Id != excludeTargetId
                && !string.IsNullOrWhiteSpace(ts.ApplicationRound)
                && string.Equals(TargetSchoolService.CanonicalRound(ts.ApplicationRound!), canonicalRound, StringComparison.OrdinalIgnoreCase));
            if (hit is null) return Task.FromResult<string?>(null);
            return Task.FromResult(Universities.FirstOrDefault(u => u.Id == hit.UniversityId)?.Name);
        }

        public Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(TargetSchools.FirstOrDefault(x => x.Id == id));

        public Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default)
        {
            var ts = TargetSchools.FirstOrDefault(x => x.Id == id);
            if (ts == null) return Task.FromResult<TargetSchoolDto?>(null);

            var u = Universities.FirstOrDefault(x => x.Id == ts.UniversityId);
            if (u == null) return Task.FromResult<TargetSchoolDto?>(null);

            return Task.FromResult<TargetSchoolDto?>(new TargetSchoolDto(
                ts.Id,
                ts.UserId,
                ts.UniversityId,
                u.Code,
                u.Name,
                u.City,
                u.State,
                u.Website,
                u.Control,
                u.AcceptanceRate,
                ts.Category,
                ts.Status,
                ts.Priority,
                ts.Notes,
                ts.ApplicationRound,
                ts.Deadline,
                ts.CreatedAt,
                ts.UpdatedAt,
                IsCategoryUserSelected: ts.IsCategoryUserSelected));
        }

        public Task<List<TargetSchoolDto>> GetListByUserAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
        {
            var list = TargetSchools
                .Where(ts => ts.UserId == userId)
                .Select(ts =>
                {
                    var u = Universities.FirstOrDefault(x => x.Id == ts.UniversityId);
                    return new TargetSchoolDto(
                        ts.Id,
                        ts.UserId,
                        ts.UniversityId,
                        u?.Code ?? "",
                        u?.Name ?? "",
                        u?.City,
                        u?.State,
                        u?.Website,
                        u?.Control,
                        u?.AcceptanceRate,
                        ts.Category,
                        ts.Status,
                        ts.Priority,
                        ts.Notes,
                        ts.ApplicationRound,
                        ts.Deadline,
                        ts.CreatedAt,
                        ts.UpdatedAt,
                        IsCategoryUserSelected: ts.IsCategoryUserSelected);
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(category))
                list = list.Where(x => string.Equals(x.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(status))
                list = list.Where(x => string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            return Task.FromResult(list);
        }

        public Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(TargetSchools.FirstOrDefault(x => x.UserId == userId && x.UniversityId == universityId));

        public Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(TargetSchools.Any(x => x.UserId == userId && x.UniversityId == universityId));

        public Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default)
        {
            TargetSchools.Add(targetSchool);
            return Task.FromResult(targetSchool);
        }

        public Task DeleteAsync(TargetSchool targetSchool, CancellationToken ct = default)
        {
            TargetSchools.Remove(targetSchool);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TargetSchool targetSchool, CancellationToken ct = default)
        {
            var idx = TargetSchools.FindIndex(x => x.Id == targetSchool.Id);
            if (idx >= 0)
            {
                TargetSchools[idx] = targetSchool;
            }
            return Task.CompletedTask;
        }

        public Task<TargetSchool?> GetByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default)
        {
            var u = Universities.FirstOrDefault(x => x.Code == code);
            if (u == null) return Task.FromResult<TargetSchool?>(null);
            return Task.FromResult(TargetSchools.FirstOrDefault(x => x.UserId == userId && x.UniversityId == u.Id));
        }

        public Task<TargetSchoolDto?> GetDtoByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default)
        {
            var u = Universities.FirstOrDefault(x => x.Code == code);
            if (u == null) return Task.FromResult<TargetSchoolDto?>(null);
            var ts = TargetSchools.FirstOrDefault(x => x.UserId == userId && x.UniversityId == u.Id);
            if (ts == null) return Task.FromResult<TargetSchoolDto?>(null);
            return Task.FromResult<TargetSchoolDto?>(new TargetSchoolDto(
                ts.Id,
                ts.UserId,
                ts.UniversityId,
                u.Code,
                u.Name,
                u.City,
                u.State,
                u.Website,
                u.Control,
                u.AcceptanceRate,
                ts.Category,
                ts.Status,
                ts.Priority,
                ts.Notes,
                ts.ApplicationRound,
                ts.Deadline,
                ts.CreatedAt,
                ts.UpdatedAt));
        }

        public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(Universities.FirstOrDefault(x => x.Id == universityId && x.IsActive));

        public Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Universities.FirstOrDefault(x => x.Code == code && x.IsActive));

        public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(DeadlinesJsonMap.TryGetValue(universityId, out var json) ? json : null);
    }

    private sealed class FakeRecommendationHistoryRepository : IRecommendationHistoryRepository
    {
        public List<Recommendation> Rows { get; } = new();

        public Task<List<Recommendation>> ListByUserAsync(Guid userId, int take, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(r => r.UserId == userId).Take(take).ToList());

        public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));
    }
}
