using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Khu "Con của tôi": phụ huynh chỉ xem / gợi ý trường mục tiêu cho con có liên kết còn hiệu lực.
public class ParentChildrenServiceTests
{
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeTargetSchools _targets = new();
    private readonly FakeAuditLogRepository _audit = new();

    private readonly Guid _parent = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();
    private readonly Guid _otherStudent = Guid.NewGuid();

    private ParentChildrenService CreateService() => new(_links, _targets, _audit);

    private void Link(string status) =>
        _links.Links.Add(new ParentStudentLink { StudentUserId = _child, ParentUserId = _parent, ParentEmail = "p@test.com", Status = status });

    private void AddSchool(Guid userId, string name) =>
        _targets.Items.Add(new TargetSchoolDto(Guid.NewGuid(), userId, Guid.NewGuid(), name, name,
            null, null, null, null, null, "match", "considering", 0, null, null, null, DateTime.UtcNow, null));

    // 1
    [Fact]
    public async Task ActiveLink_ReturnsOnlyChildSchools()
    {
        Link(ParentLinkStatuses.Active);
        AddSchool(_child, "A");
        AddSchool(_otherStudent, "X");

        var list = await CreateService().GetTargetSchoolsAsync(_parent, _child, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Equal("A", Assert.Single(list).UniversityName);   // không lẫn trường của học sinh khác
    }

    // 2
    [Fact]
    public async Task NoLink_ReturnsNull()
    {
        AddSchool(_child, "A");

        Assert.Null(await CreateService().GetTargetSchoolsAsync(_parent, _child, CancellationToken.None));
    }

    // 3
    [Fact]
    public async Task RevokedLink_ReturnsNull()
    {
        Link(ParentLinkStatuses.Revoked);
        AddSchool(_child, "A");

        Assert.Null(await CreateService().GetTargetSchoolsAsync(_parent, _child, CancellationToken.None));
    }

    // 4
    [Fact]
    public async Task ChildWithoutSchools_ReturnsEmpty()
    {
        Link(ParentLinkStatuses.Active);

        var list = await CreateService().GetTargetSchoolsAsync(_parent, _child, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Empty(list);
    }

    // 5
    [Fact]
    public async Task Suggest_ActiveLink_AddsParentSuggestedRowAndAudit()
    {
        Link(ParentLinkStatuses.Active);
        var uni = AddUniversity("MIT");

        var result = await CreateService().SuggestTargetSchoolAsync(_parent, _child,
            new SuggestTargetSchoolRequest(UniversityCode: "MIT", Note: "  Bố mẹ thích trường này  "), CancellationToken.None);

        Assert.Null(result.Error);
        var row = Assert.Single(_targets.Rows);
        Assert.Equal(_child, row.UserId);                                  // lưu vào danh sách của con, không phải của phụ huynh
        Assert.Equal(uni.Id, row.UniversityId);
        Assert.Equal(TargetSchoolStatuses.ParentSuggested, row.Status);
        Assert.Equal("Bố mẹ thích trường này", row.Notes);
        Assert.Equal("MIT", result.TargetSchool!.UniversityCode);
        var audit = Assert.Single(_audit.Entries);
        Assert.Equal("target_school.parent_suggest", audit.Action);
        Assert.Equal(_parent, audit.ActorUserId);
    }

    // 6
    [Fact]
    public async Task Suggest_NoLink_ReturnsNotLinkedAndAddsNothing()
    {
        AddUniversity("MIT");

        var result = await CreateService().SuggestTargetSchoolAsync(_parent, _child,
            new SuggestTargetSchoolRequest(UniversityCode: "MIT"), CancellationToken.None);

        Assert.True(result.NotLinked);
        Assert.Empty(_targets.Rows);
    }

    // 7
    [Fact]
    public async Task Suggest_ChildAlreadyHasSchool_ReturnsError()
    {
        Link(ParentLinkStatuses.Active);
        var uni = AddUniversity("MIT");
        _targets.Rows.Add(new TargetSchool { UserId = _child, UniversityId = uni.Id, Status = TargetSchoolStatuses.Considering });

        var result = await CreateService().SuggestTargetSchoolAsync(_parent, _child,
            new SuggestTargetSchoolRequest(UniversityId: uni.Id), CancellationToken.None);

        Assert.Equal("Con đã có trường này trong danh sách mục tiêu.", result.Error);
        Assert.Single(_targets.Rows);
    }

    // 8
    [Fact]
    public async Task Suggest_AlreadySuggested_ReturnsWaitingError()
    {
        Link(ParentLinkStatuses.Active);
        var uni = AddUniversity("MIT");
        _targets.Rows.Add(new TargetSchool { UserId = _child, UniversityId = uni.Id, Status = TargetSchoolStatuses.ParentSuggested });

        var result = await CreateService().SuggestTargetSchoolAsync(_parent, _child,
            new SuggestTargetSchoolRequest(UniversityId: uni.Id), CancellationToken.None);

        Assert.Equal("Bạn đã gợi ý trường này, đang chờ con xem.", result.Error);
    }

    // 9
    [Fact]
    public async Task Suggest_UnknownUniversity_ReturnsError()
    {
        Link(ParentLinkStatuses.Active);

        var result = await CreateService().SuggestTargetSchoolAsync(_parent, _child,
            new SuggestTargetSchoolRequest(UniversityCode: "NOPE"), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Empty(_targets.Rows);
    }

    // 10
    [Fact]
    public async Task Withdraw_PendingSuggestion_DeletesRow()
    {
        Link(ParentLinkStatuses.Active);
        var row = new TargetSchool { UserId = _child, UniversityId = Guid.NewGuid(), Status = TargetSchoolStatuses.ParentSuggested };
        _targets.Rows.Add(row);

        Assert.True(await CreateService().WithdrawSuggestionAsync(_parent, _child, row.Id, CancellationToken.None));
        Assert.Empty(_targets.Rows);
        Assert.Equal("target_school.parent_withdraw", Assert.Single(_audit.Entries).Action);
    }

    // 11
    [Fact]
    public async Task Withdraw_SchoolChildAlreadyAdded_ReturnsFalseAndKeepsRow()
    {
        Link(ParentLinkStatuses.Active);
        var row = new TargetSchool { UserId = _child, UniversityId = Guid.NewGuid(), Status = TargetSchoolStatuses.Considering };
        _targets.Rows.Add(row);

        Assert.False(await CreateService().WithdrawSuggestionAsync(_parent, _child, row.Id, CancellationToken.None));
        Assert.Single(_targets.Rows);
    }

    // 12
    [Fact]
    public async Task Withdraw_SuggestionOfOtherStudent_ReturnsFalse()
    {
        Link(ParentLinkStatuses.Active);
        var row = new TargetSchool { UserId = _otherStudent, UniversityId = Guid.NewGuid(), Status = TargetSchoolStatuses.ParentSuggested };
        _targets.Rows.Add(row);

        Assert.False(await CreateService().WithdrawSuggestionAsync(_parent, _child, row.Id, CancellationToken.None));
        Assert.Single(_targets.Rows);
    }

    // 13
    [Fact]
    public async Task Withdraw_NoLink_ReturnsNull()
    {
        var row = new TargetSchool { UserId = _child, UniversityId = Guid.NewGuid(), Status = TargetSchoolStatuses.ParentSuggested };
        _targets.Rows.Add(row);

        Assert.Null(await CreateService().WithdrawSuggestionAsync(_parent, _child, row.Id, CancellationToken.None));
        Assert.Single(_targets.Rows);
    }

    private University AddUniversity(string code)
    {
        var uni = new University { Id = Guid.NewGuid(), Code = code, Name = code + " University", IsActive = true };
        _targets.Universities.Add(uni);
        return uni;
    }

    // Giả lập: Items là DTO có sẵn (test xem danh sách), Rows là dòng service thêm / xóa (test gợi ý).
    internal sealed class FakeTargetSchools : ITargetSchoolRepository
    {
        public List<TargetSchoolDto> Items { get; } = [];
        public List<TargetSchool> Rows { get; } = [];
        public List<University> Universities { get; } = [];

        public Task<List<TargetSchoolDto>> GetListByUserAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Concat(Rows.Select(ToDto)).Where(x => x.UserId == userId).ToList());

        public Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(x => x.Id == id));

        public Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(x => x.Id == id).Select(ToDto).FirstOrDefault());

        public Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(x => x.UserId == userId && x.UniversityId == universityId));

        public Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default)
        {
            Rows.Add(targetSchool);
            return Task.FromResult(targetSchool);
        }

        public Task DeleteAsync(TargetSchool targetSchool, CancellationToken ct = default)
        {
            Rows.Remove(targetSchool);
            return Task.CompletedTask;
        }

        public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(Universities.FirstOrDefault(x => x.Id == universityId && x.IsActive));

        public Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Universities.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) && x.IsActive));

        private TargetSchoolDto ToDto(TargetSchool ts)
        {
            var u = Universities.FirstOrDefault(x => x.Id == ts.UniversityId);
            return new TargetSchoolDto(ts.Id, ts.UserId, ts.UniversityId, u?.Code ?? "", u?.Name ?? "", null, null, null, null, null,
                ts.Category, ts.Status, ts.Priority, ts.Notes, ts.ApplicationRound, ts.Deadline, ts.CreatedAt, ts.UpdatedAt);
        }

        // Service không gọi các hàm dưới
        public Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(TargetSchool targetSchool, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchool?> GetByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchoolDto?> GetDtoByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
