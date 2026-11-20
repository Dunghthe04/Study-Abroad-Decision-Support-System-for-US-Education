using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// 1.5a: phụ huynh thêm / đổi tên / xóa con chưa có tài khoản (con managed).
// Chỉ đụng được con managed có liên kết active với đúng phụ huynh này.
public class ManagedChildServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeAuditLogRepository _audit = new();

    private readonly Guid _parent = Guid.NewGuid();
    private static readonly DateOnly Dob = new(2010, 5, 1);

    public ManagedChildServiceTests()
    {
        _users.Users.Add(new User { Id = _parent, Email = "Parent@Test.com", FullName = "Phụ huynh", Role = UserRoles.Parent, Status = UserStatuses.Active });
    }

    private ManagedChildService CreateService() => new(_users, _links, _uow, _audit);

    private Task<ManagedChildResult> AddAsync(string? name = "Minh", DateOnly? dob = null) =>
        CreateService().AddAsync(_parent, new AddManagedChildRequest(name, dob ?? Dob), CancellationToken.None);

    // Thêm sẵn một học sinh (managed hoặc thật) có liên kết active với phụ huynh
    private Guid AddLinkedStudent(string status, Guid? parentId = null)
    {
        var id = Guid.NewGuid();
        _users.Users.Add(new User { Id = id, Email = $"{id:N}@test.com", FullName = "Con", Role = UserRoles.Student, Status = status });
        _links.Links.Add(new ParentStudentLink { StudentUserId = id, ParentUserId = parentId ?? _parent, ParentEmail = "parent@test.com", Status = ParentLinkStatuses.Active });
        return id;
    }

    // 1
    [Fact]
    public async Task Add_CreatesManagedUserAndActiveLink()
    {
        var result = await AddAsync("  Minh  ");

        Assert.Null(result.Error);
        var child = _users.Users.Single(u => u.Id == result.Child!.StudentUserId);
        Assert.Equal("Minh", child.FullName);
        Assert.Equal(UserRoles.Student, child.Role);
        Assert.Equal(UserStatuses.Managed, child.Status);
        Assert.Equal(Dob, child.DateOfBirth);
        Assert.Empty(child.PasswordHash);
        Assert.StartsWith("managed-", child.Email);
        Assert.EndsWith("@" + ManagedChildService.EmailDomain, child.Email);

        var link = Assert.Single(_links.Links);
        Assert.Equal(child.Id, link.StudentUserId);
        Assert.Equal(_parent, link.ParentUserId);
        Assert.Equal("parent@test.com", link.ParentEmail);
        Assert.Equal(ParentLinkStatuses.Active, link.Status);
        Assert.NotNull(link.AcceptedAt);
        Assert.Null(link.TokenHash);

        Assert.Equal(1, _uow.Calls);
        Assert.Equal("parent.managed_child.add", Assert.Single(_audit.Entries).Action);
    }

    // 2
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_EmptyName_Fails(string? name)
    {
        var result = await AddAsync(name);

        Assert.NotNull(result.Error);
        Assert.Single(_users.Users);   // chỉ còn phụ huynh
        Assert.Empty(_links.Links);
    }

    // 3
    [Fact]
    public async Task Add_NameTooLong_Fails()
    {
        Assert.NotNull((await AddAsync(new string('a', 129))).Error);
        Assert.Null((await AddAsync(new string('a', 128))).Error);
    }

    // 4
    [Fact]
    public async Task Add_MissingOrFutureOrTooOldDob_Fails()
    {
        var service = CreateService();
        var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);

        Assert.NotNull((await service.AddAsync(_parent, new AddManagedChildRequest("Minh", null), CancellationToken.None)).Error);
        Assert.NotNull((await AddAsync(dob: today.AddDays(1))).Error);
        Assert.NotNull((await AddAsync(dob: today.AddYears(-101))).Error);
        Assert.Empty(_links.Links);
    }

    // 5
    [Fact]
    public async Task Add_CallerNotParent_Fails()
    {
        var student = Guid.NewGuid();
        _users.Users.Add(new User { Id = student, Email = "s@test.com", FullName = "S", Role = UserRoles.Student, Status = UserStatuses.Active });

        var result = await CreateService().AddAsync(student, new AddManagedChildRequest("Minh", Dob), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Empty(_links.Links);
    }

    // 6
    [Fact]
    public async Task Add_SixthManagedChild_Fails()
    {
        for (var i = 0; i < ManagedChildService.MaxManagedChildren; i++)
            Assert.Null((await AddAsync($"Con {i}")).Error);

        var result = await AddAsync("Con thứ 6");

        Assert.NotNull(result.Error);
        Assert.Equal(ManagedChildService.MaxManagedChildren, _links.Links.Count);
    }

    // 7. Con thật đã liên kết và con managed đã xóa không tính vào giới hạn
    [Fact]
    public async Task Add_RealChildrenAndDeletedManaged_NotCounted()
    {
        AddLinkedStudent(UserStatuses.Active);
        AddLinkedStudent(UserStatuses.Active);
        for (var i = 0; i < ManagedChildService.MaxManagedChildren; i++)
            Assert.Null((await AddAsync($"Con {i}")).Error);

        var first = _users.Users.First(u => u.Status == UserStatuses.Managed).Id;
        Assert.True(await CreateService().DeleteAsync(_parent, first, CancellationToken.None));

        Assert.Null((await AddAsync("Con mới")).Error);
    }

    // 8
    [Fact]
    public async Task Rename_ManagedChild_UpdatesNameAndAudit()
    {
        var child = AddLinkedStudent(UserStatuses.Managed);

        var result = await CreateService().RenameAsync(_parent, child, new RenameManagedChildRequest(" Lan "), CancellationToken.None);

        Assert.Equal("Lan", result.Child!.FullName);
        Assert.Equal("Lan", _users.Users.Single(u => u.Id == child).FullName);
        Assert.Equal("parent.managed_child.rename", Assert.Single(_audit.Entries).Action);
    }

    // 9
    [Fact]
    public async Task Rename_EmptyName_FailsWithoutChange()
    {
        var child = AddLinkedStudent(UserStatuses.Managed);

        var result = await CreateService().RenameAsync(_parent, child, new RenameManagedChildRequest(" "), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal("Con", _users.Users.Single(u => u.Id == child).FullName);
    }

    // 10. Con thật, con managed của phụ huynh khác, liên kết đã thu hồi → không tìm thấy
    [Fact]
    public async Task RenameOrDelete_NotOwnManagedChild_NotFound()
    {
        var realChild = AddLinkedStudent(UserStatuses.Active);
        var otherParentsChild = AddLinkedStudent(UserStatuses.Managed, parentId: Guid.NewGuid());
        var revoked = AddLinkedStudent(UserStatuses.Managed);
        _links.Links.Single(l => l.StudentUserId == revoked).Status = ParentLinkStatuses.Revoked;
        var service = CreateService();

        foreach (var id in new[] { realChild, otherParentsChild, revoked, Guid.NewGuid() })
        {
            Assert.True((await service.RenameAsync(_parent, id, new RenameManagedChildRequest("X"), CancellationToken.None)).NotFound);
            Assert.False(await service.DeleteAsync(_parent, id, CancellationToken.None));
        }

        Assert.DoesNotContain(_users.Users, u => u.FullName == "X");
        Assert.DoesNotContain(_users.Users, u => u.Status == UserStatuses.Deleted);
        Assert.Empty(_audit.Entries);
    }

    // 11
    [Fact]
    public async Task Delete_SoftDeletesUserRevokesLinkAndSessions()
    {
        var child = AddLinkedStudent(UserStatuses.Managed);
        _users.Sessions.Add(new UserSession { UserId = child, TokenHash = "h" });

        Assert.True(await CreateService().DeleteAsync(_parent, child, CancellationToken.None));

        var user = _users.Users.Single(u => u.Id == child);
        Assert.Equal(UserStatuses.Deleted, user.Status);
        Assert.NotNull(user.DeletedAt);
        Assert.True(Assert.Single(_users.Sessions).IsRevoked);

        var link = Assert.Single(_links.Links);
        Assert.Equal(ParentLinkStatuses.Revoked, link.Status);
        Assert.NotNull(link.RevokedAt);
        Assert.Equal(_parent, link.RevokedByUserId);

        Assert.Equal(1, _uow.Calls);
        Assert.Equal("parent.managed_child.delete", Assert.Single(_audit.Entries).Action);
    }

    // 12. Đã xóa thì xóa / đổi tên lần nữa → không tìm thấy
    [Fact]
    public async Task DeletedChild_CannotBeRenamedOrDeletedAgain()
    {
        var child = AddLinkedStudent(UserStatuses.Managed);
        var service = CreateService();
        Assert.True(await service.DeleteAsync(_parent, child, CancellationToken.None));

        Assert.False(await service.DeleteAsync(_parent, child, CancellationToken.None));
        Assert.True((await service.RenameAsync(_parent, child, new RenameManagedChildRequest("X"), CancellationToken.None)).NotFound);
    }
}
