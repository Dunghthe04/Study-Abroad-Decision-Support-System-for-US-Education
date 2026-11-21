using System.Text.Json;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-60] Danh sách tài khoản của Admin: chuẩn hóa bộ lọc, giới hạn phân trang và giải thích lý do khóa.</summary>
public class AdminUserListServiceTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) => ("token", DateTime.UtcNow.AddDays(1));
        public string HashToken(string token) => "hash:" + token;
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();

    private AuthService Service() => new(
        _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
        new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository());

    private User AddUser(string email, string name, string role, string status = UserStatuses.Active, int createdDaysAgo = 0)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, FullName = name, Role = role, Status = status, PasswordHash = "x",
            CreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo),
        };
        _users.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task List_FiltersByKeywordRoleAndStatus_TrimmingTheFilterValues()
    {
        AddUser("nguyen.a@test.com", "Nguyen Van A", UserRoles.Student);
        AddUser("nguyen.b@test.com", "Nguyen Thi B", UserRoles.Parent);
        AddUser("tran.c@test.com", "Tran Van C", UserRoles.Student);
        AddUser("nguyen.d@test.com", "Nguyen Van D", UserRoles.Student, UserStatuses.Locked);

        var page = await Service().GetAdminUserAccountListAsync(
            new AdminUserAccountListQuery(Search: "  nguyen ", Role: " student ", Status: " active "));

        var item = Assert.Single(page.Items);
        Assert.Equal("nguyen.a@test.com", item.Email);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_BlankFilters_AreTreatedAsNoFilter_NewestFirst()
    {
        AddUser("old@test.com", "Old", UserRoles.Student, createdDaysAgo: 10);
        AddUser("new@test.com", "New", UserRoles.Parent, createdDaysAgo: 1);

        var page = await Service().GetAdminUserAccountListAsync(new AdminUserAccountListQuery(Search: "  ", Role: "", Status: " "));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["new@test.com", "old@test.com"], page.Items.Select(u => u.Email));
    }

    [Fact]
    public async Task List_ClampsPageAndPageSize()
    {
        for (var i = 0; i < 120; i++)
            AddUser($"u{i}@test.com", $"User {i}", UserRoles.Student, createdDaysAgo: i);

        var tooBig = await Service().GetAdminUserAccountListAsync(new AdminUserAccountListQuery(Page: 0, PageSize: 1000));
        var tooSmall = await Service().GetAdminUserAccountListAsync(new AdminUserAccountListQuery(Page: -3, PageSize: 0));

        Assert.Equal(1, tooBig.Page);
        Assert.Equal(100, tooBig.PageSize);
        Assert.Equal(100, tooBig.Items.Count);
        Assert.Equal(120, tooBig.TotalCount);
        Assert.Equal(1, tooSmall.Page);
        Assert.Equal(1, tooSmall.PageSize);
        Assert.Single(tooSmall.Items);
    }

    [Fact]
    public async Task List_LockedUser_ShowsTheLatestLockReasonFromTheAuditLog_OrADefaultText()
    {
        var withReason = AddUser("a@test.com", "A", UserRoles.Student, UserStatuses.Locked);
        var withoutReason = AddUser("b@test.com", "B", UserRoles.Student, UserStatuses.Locked);
        _audit.Entries.Add(new AuditLog
        {
            Action = "user.lock", EntityId = withReason.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { reason = "Vi phạm quy chế cộng đồng" }),
        });

        var page = await Service().GetAdminUserAccountListAsync(new AdminUserAccountListQuery(Status: UserStatuses.Locked));

        Assert.Equal("Vi phạm quy chế cộng đồng", page.Items.Single(u => u.Id == withReason.Id).LockReason);
        Assert.False(string.IsNullOrWhiteSpace(page.Items.Single(u => u.Id == withoutReason.Id).LockReason));
    }

    [Fact]
    public async Task List_TempLockedUser_ShowsAutoLockReasonAndLockoutEnd_ActiveUserShowsNone()
    {
        var lockoutEnd = DateTime.UtcNow.AddMinutes(10);
        var temp = AddUser("t@test.com", "T", UserRoles.Student, UserStatuses.TempLocked);
        temp.LockoutEnd = lockoutEnd;
        var active = AddUser("c@test.com", "C", UserRoles.Student);

        var page = await Service().GetAdminUserAccountListAsync(new AdminUserAccountListQuery());

        var tempDto = page.Items.Single(u => u.Id == temp.Id);
        Assert.False(string.IsNullOrWhiteSpace(tempDto.LockReason));
        Assert.Equal(lockoutEnd, tempDto.LockoutEnd);
        Assert.Null(page.Items.Single(u => u.Id == active.Id).LockReason);
    }
}
