using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Khu "Con của tôi": phụ huynh chỉ xem được trường mục tiêu của con có liên kết còn hiệu lực.
public class ParentChildrenServiceTests
{
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeTargetSchools _targets = new();

    private readonly Guid _parent = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();
    private readonly Guid _otherStudent = Guid.NewGuid();

    private ParentChildrenService CreateService() => new(_links, _targets);

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

    // Giả lập tối thiểu: service chỉ gọi GetListByUserAsync, hàm khác không được gọi tới.
    private sealed class FakeTargetSchools : ITargetSchoolRepository
    {
        public List<TargetSchoolDto> Items { get; } = [];

        public Task<List<TargetSchoolDto>> GetListByUserAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.UserId == userId).ToList());

        public Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(TargetSchool targetSchool, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(TargetSchool targetSchool, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchool?> GetByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TargetSchoolDto?> GetDtoByUserAndUniversityCodeAsync(Guid userId, string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
