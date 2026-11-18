using System.Security.Claims;
using System.Text.Json;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// 1.5b: phụ huynh làm thay con managed. Chỉ phụ huynh còn quyền, có liên kết active, với đúng con managed của mình.
// Mất quyền giữa chừng (thu hồi liên kết, bị khóa, đăng xuất) thì request kế tiếp và lần gia hạn kế tiếp bị từ chối.
public class ActingServiceTests
{
    // Ghi lại phụ huynh trong token để kiểm tra claim act có được giữ khi gia hạn
    private sealed class ActingTokenService : ITokenService
    {
        private int _n;
        public Dictionary<string, Guid?> ActorOf { get; } = [];

        public (string Token, DateTime ExpiresAt) GenerateToken(User user) => Create(user, null);
        public (string Token, DateTime ExpiresAt) GenerateToken(User user, Guid actingParentId) => Create(user, actingParentId);
        public string HashToken(string token) => "hash:" + token;

        private (string, DateTime) Create(User user, Guid? parentId)
        {
            var token = $"access-{user.Id:N}-{++_n}";
            ActorOf[token] = parentId;
            return (token, DateTime.UtcNow.AddMinutes(15));
        }
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();
    private readonly ActingTokenService _tokens = new();

    private readonly User _parent = new() { Id = Guid.NewGuid(), Email = "p@test.com", FullName = "Phụ huynh", Role = UserRoles.Parent, Status = UserStatuses.Active };
    private readonly string _parentAccess;
    private readonly string _parentRefresh;
    private readonly UserSession _parentSession;

    public ActingServiceTests()
    {
        _users.Users.Add(_parent);
        (_parentSession, _parentAccess, _parentRefresh) = AddSession(_parent.Id);
    }

    private ActingSessionGuard Guard() => new(_users, _links);
    private SessionRefreshService Refresh() => new(_users, _tokens, Guard());
    private ActingService CreateService() => new(_users, _tokens, Guard(), Refresh(), _audit);

    private (UserSession Session, string Access, string Refresh) AddSession(Guid userId, DateTime? expiresAt = null)
    {
        var access = $"login-{Guid.NewGuid():N}";
        var refresh = RefreshTokens.Generate();
        var session = new UserSession
        {
            UserId = userId,
            TokenHash = _tokens.HashToken(access),
            RefreshTokenHash = _tokens.HashToken(refresh),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.Add(RefreshTokens.Lifetime),
        };
        _users.Sessions.Add(session);
        return (session, access, refresh);
    }

    private Guid AddChild(string status = UserStatuses.Managed, Guid? parentId = null, string linkStatus = ParentLinkStatuses.Active)
    {
        var id = Guid.NewGuid();
        _users.Users.Add(new User { Id = id, Email = $"{id:N}@test.com", FullName = "Con", Role = UserRoles.Student, Status = status });
        _links.Links.Add(new ParentStudentLink { StudentUserId = id, ParentUserId = parentId ?? _parent.Id, ParentEmail = "p@test.com", Status = linkStatus });
        return id;
    }

    private Task<ActingStartResult> StartAsync(Guid childId, string? access = null, string? refresh = null) =>
        CreateService().StartAsync(_parent.Id, childId, access ?? _parentAccess, refresh ?? _parentRefresh, "127.0.0.1", "test", CancellationToken.None);

    private static ClaimsPrincipal Principal(Guid? actingParentId, string claimType = ActingSessionGuard.ActorClaim, string? rawAct = null)
    {
        var claims = new List<Claim>();
        if (rawAct is not null)
            claims.Add(new Claim(claimType, rawAct));
        else if (actingParentId is { } id)
            claims.Add(new Claim(claimType, JsonSerializer.Serialize(new { sub = id.ToString() })));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private Task<bool> IsValidAsync(Guid userId, ClaimsPrincipal principal) =>
        Guard().IsValidAsync(_users.Users.Single(u => u.Id == userId), principal, CancellationToken.None);

    // 1. Vào làm thay: phiên mới của con mang act của phụ huynh, hạn không quá hạn phiên phụ huynh, có audit
    [Fact]
    public async Task Start_ManagedChild_CreatesChildSessionWithActor()
    {
        var child = AddChild();

        var result = await StartAsync(child);

        Assert.Equal(ActingStartOutcome.Ok, result.Outcome);
        Assert.Equal(_parent.Id, _tokens.ActorOf[result.AccessToken!]);
        var session = _users.Sessions.Single(s => s.UserId == child);
        Assert.Equal(_tokens.HashToken(result.AccessToken!), session.TokenHash);
        Assert.Equal(_tokens.HashToken(result.RefreshToken!), session.RefreshTokenHash);
        Assert.True(session.ExpiresAt <= _parentSession.ExpiresAt);
        Assert.Equal(_parentSession.ExpiresAt, result.ParentSessionExpiresAt);
        // Phiên phụ huynh vẫn còn để thoát thì lấy lại
        Assert.False(_parentSession.IsRevoked);
        var log = Assert.Single(_audit.Entries);
        Assert.Equal("parent.acting.start", log.Action);
        Assert.Equal(_parent.Id, log.ActorUserId);
        Assert.Equal(child.ToString(), log.EntityId);
    }

    // 2. Phiên con không sống lâu hơn phiên phụ huynh
    [Fact]
    public async Task Start_ChildSessionCappedAtParentSessionExpiry()
    {
        _parentSession.ExpiresAt = DateTime.UtcNow.AddHours(1);
        var child = AddChild();

        var result = await StartAsync(child);

        Assert.Equal(_parentSession.ExpiresAt, result.SessionExpiresAt);
    }

    // 3. Không được làm thay: con thật, con managed của phụ huynh khác, liên kết đã thu hồi, con không tồn tại
    [Fact]
    public async Task Start_NotOwnManagedChild_NotFound()
    {
        var realChild = AddChild(UserStatuses.Active);
        var otherParent = Guid.NewGuid();
        _users.Users.Add(new User { Id = otherParent, Email = "o@test.com", FullName = "Khác", Role = UserRoles.Parent, Status = UserStatuses.Active });
        var othersChild = AddChild(parentId: otherParent);
        var revokedChild = AddChild(linkStatus: ParentLinkStatuses.Revoked);

        foreach (var id in new[] { realChild, othersChild, revokedChild, Guid.NewGuid() })
            Assert.Equal(ActingStartOutcome.NotFound, (await StartAsync(id)).Outcome);

        Assert.DoesNotContain(_users.Sessions, s => s.UserId != _parent.Id);
        Assert.Empty(_audit.Entries);
    }

    // 4. Phụ huynh bị khóa / đã xóa, hoặc người gọi không phải phụ huynh
    [Theory]
    [InlineData(UserRoles.Parent, UserStatuses.Locked)]
    [InlineData(UserRoles.Parent, UserStatuses.Deleted)]
    [InlineData(UserRoles.Student, UserStatuses.Active)]
    public async Task Start_ParentNotAllowed_NotFound(string role, string status)
    {
        var child = AddChild();
        _parent.Role = role;
        _parent.Status = status;

        Assert.Equal(ActingStartOutcome.NotFound, (await StartAsync(child)).Outcome);
    }

    // 5. Thiếu hoặc sai refresh token của phiên phụ huynh, phiên đã thu hồi / hết hạn: 401
    [Fact]
    public async Task Start_ParentSessionInvalid_Unauthorized()
    {
        var child = AddChild();
        var (_, _, otherRefresh) = AddSession(_parent.Id);   // refresh của phiên khác cùng phụ huynh

        Assert.Equal(ActingStartOutcome.Unauthorized, (await CreateService().StartAsync(_parent.Id, child, _parentAccess, null, null, null, CancellationToken.None)).Outcome);
        Assert.Equal(ActingStartOutcome.Unauthorized, (await StartAsync(child, refresh: otherRefresh)).Outcome);
        Assert.Equal(ActingStartOutcome.Unauthorized, (await StartAsync(child, access: "khong-co")).Outcome);

        _parentSession.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        Assert.Equal(ActingStartOutcome.Unauthorized, (await StartAsync(child)).Outcome);

        _parentSession.ExpiresAt = DateTime.UtcNow.AddDays(1);
        _parentSession.IsRevoked = true;
        Assert.Equal(ActingStartOutcome.Unauthorized, (await StartAsync(child)).Outcome);
    }

    // 6. Mỗi request: con managed bắt buộc có act của phụ huynh còn quyền
    [Fact]
    public async Task Guard_ManagedChild_RequiresValidActor()
    {
        var child = AddChild();

        Assert.True(await IsValidAsync(child, Principal(_parent.Id)));
        // .NET có thể đổi tên claim act khi đọc token
        Assert.True(await IsValidAsync(child, Principal(_parent.Id, ClaimTypes.Actor)));
        Assert.False(await IsValidAsync(child, Principal(null)));
        Assert.False(await IsValidAsync(child, Principal(Guid.NewGuid())));
        Assert.False(await IsValidAsync(child, Principal(null, rawAct: "khong-phai-json")));
    }

    // 7. Tài khoản thường mang act (kể cả sai dạng) bị từ chối, không mang thì bình thường
    [Fact]
    public async Task Guard_RegularAccount_RejectsActorClaim()
    {
        var realChild = AddChild(UserStatuses.Active);

        Assert.True(await IsValidAsync(realChild, Principal(null)));
        Assert.False(await IsValidAsync(realChild, Principal(_parent.Id)));
        Assert.False(await IsValidAsync(realChild, Principal(null, rawAct: "rac")));
        Assert.True(await IsValidAsync(_parent.Id, Principal(null)));
    }

    // 8. Mất quyền giữa chừng: thu hồi liên kết, phụ huynh bị khóa, phụ huynh không còn phiên nào → request kế tiếp bị từ chối
    [Theory]
    [InlineData("revoke-link")]
    [InlineData("lock-parent")]
    [InlineData("parent-logout")]
    public async Task Guard_AccessLostMidSession_Rejected(string change)
    {
        var child = AddChild();
        Assert.True(await IsValidAsync(child, Principal(_parent.Id)));

        switch (change)
        {
            case "revoke-link": _links.Links.Single(l => l.StudentUserId == child).Status = ParentLinkStatuses.Revoked; break;
            case "lock-parent": _parent.Status = UserStatuses.Locked; break;
            case "parent-logout": _parentSession.IsRevoked = true; break;
        }

        Assert.False(await IsValidAsync(child, Principal(_parent.Id)));
    }

    // 9. Gia hạn phiên làm thay giữ act
    [Fact]
    public async Task Refresh_ActingSession_KeepsActor()
    {
        var child = AddChild();
        var start = await StartAsync(child);

        var refreshed = await Refresh().RefreshAsync(start.RefreshToken, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Ok, refreshed.Outcome);
        Assert.Equal(_parent.Id, _tokens.ActorOf[refreshed.AccessToken!]);
    }

    // 10. Gia hạn phiên làm thay khi đã mất quyền: từ chối và đóng phiên của con
    [Fact]
    public async Task Refresh_ActingSession_LinkRevoked_RevokesChildSession()
    {
        var child = AddChild();
        var start = await StartAsync(child);
        _links.Links.Single(l => l.StudentUserId == child).Status = ParentLinkStatuses.Revoked;

        var refreshed = await Refresh().RefreshAsync(start.RefreshToken, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Unauthorized, refreshed.Outcome);
        Assert.True(_users.Sessions.Single(s => s.UserId == child).IsRevoked);
    }

    // 11. Thoát làm thay: đóng phiên con, phiên phụ huynh được gia hạn (token không có act), có audit
    [Fact]
    public async Task Stop_RevokesChildSession_RestoresParent()
    {
        var child = AddChild();
        var start = await StartAsync(child);

        var result = await CreateService().StopAsync(start.AccessToken, start.RefreshToken, _parentRefresh, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Ok, result!.Outcome);
        Assert.Null(_tokens.ActorOf[result.AccessToken!]);
        Assert.Equal(_tokens.HashToken(result.AccessToken!), _parentSession.TokenHash);
        Assert.True(_users.Sessions.Single(s => s.UserId == child).IsRevoked);
        Assert.False(_parentSession.IsRevoked);
        Assert.Equal("parent.acting.stop", _audit.Entries.Last().Action);
    }

    // 12. Phiên con đã hết hạn / bị thu hồi vẫn thoát được về phụ huynh (chỉ cần cookie phụ huynh)
    [Fact]
    public async Task Stop_ChildSessionGone_StillRestoresParent()
    {
        var child = AddChild();
        await StartAsync(child);
        _users.Sessions.Single(s => s.UserId == child).IsRevoked = true;

        var result = await CreateService().StopAsync(null, null, _parentRefresh, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Ok, result!.Outcome);
    }

    // 13. Không có cookie phụ huynh: không đang làm thay, không đụng phiên nào
    [Fact]
    public async Task Stop_NoParentCookie_ReturnsNull_TouchesNothing()
    {
        var child = AddChild();
        var start = await StartAsync(child);

        Assert.Null(await CreateService().StopAsync(start.AccessToken, start.RefreshToken, null, CancellationToken.None));
        Assert.False(_users.Sessions.Single(s => s.UserId == child).IsRevoked);
    }

    // 14. Cookie phụ huynh cũ còn sót trên trình duyệt của tài khoản thường: không đóng phiên tài khoản thường
    [Fact]
    public async Task Stop_RegularAccountSession_NotRevoked()
    {
        var real = AddChild(UserStatuses.Active);
        var (realSession, realAccess, realRefresh) = AddSession(real);

        await CreateService().StopAsync(realAccess, realRefresh, _parentRefresh, CancellationToken.None);

        Assert.False(realSession.IsRevoked);
    }
}
