using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Gia hạn phiên bằng refresh token: đổi token mỗi lần, dùng lại token cũ sau 30 giây thì thu hồi phiên.
public class SessionRefreshServiceTests
{
    // Mỗi lần sinh một access token khác nhau, để kiểm tra token cũ hết tác dụng
    private sealed class CountingTokenService : ITokenService
    {
        private int _n;
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) =>
            ($"access-{user.Id:N}-{++_n}", DateTime.UtcNow.AddMinutes(15));

        public string HashToken(string token) => "hash:" + token;
    }

    private readonly FakeUserRepository _users = new();
    private readonly CountingTokenService _tokens = new();
    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "a@test.com", FullName = "A", Role = UserRoles.Student, Status = UserStatuses.Active };

    public SessionRefreshServiceTests() => _users.Users.Add(_user);

    private SessionRefreshService CreateService() => new(_users, _tokens, new ActingSessionGuard(_users, new FakeParentLinkRepository()));

    // Một phiên vừa đăng nhập, trả refresh token thô
    private (UserSession Session, string Refresh) AddSession(DateTime? expiresAt = null)
    {
        var refresh = RefreshTokens.Generate();
        var session = new UserSession
        {
            UserId = _user.Id,
            TokenHash = _tokens.HashToken("access-login"),
            RefreshTokenHash = _tokens.HashToken(refresh),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.Add(RefreshTokens.Lifetime),
        };
        _users.Sessions.Add(session);
        return (session, refresh);
    }

    // 1
    [Fact]
    public async Task Refresh_Valid_RotatesBothTokens_KeepsSessionExpiry()
    {
        var (session, refresh) = AddSession();
        var sessionExpiresAt = session.ExpiresAt;

        var result = await CreateService().RefreshAsync(refresh, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Ok, result.Outcome);
        Assert.NotEqual(refresh, result.RefreshToken);
        Assert.Equal(_tokens.HashToken(result.RefreshToken!), session.RefreshTokenHash);
        Assert.Equal(_tokens.HashToken(refresh), session.PreviousRefreshTokenHash);
        // Access token cũ không còn khớp phiên nên OnTokenValidated từ chối ngay
        Assert.Equal(_tokens.HashToken(result.AccessToken!), session.TokenHash);
        Assert.NotEqual(_tokens.HashToken("access-login"), session.TokenHash);
        // Gia hạn không kéo dài hạn 7 ngày của phiên
        Assert.Equal(sessionExpiresAt, session.ExpiresAt);
        Assert.Equal(sessionExpiresAt, result.SessionExpiresAt);
        Assert.False(session.IsRevoked);
    }

    // 2. Dùng tiếp refresh token mới thì gia hạn được nhiều lần
    [Fact]
    public async Task Refresh_ChainOfNewTokens_Works()
    {
        var (_, refresh) = AddSession();
        var service = CreateService();

        for (var i = 0; i < 3; i++)
        {
            var result = await service.RefreshAsync(refresh, CancellationToken.None);
            Assert.Equal(RefreshOutcome.Ok, result.Outcome);
            refresh = result.RefreshToken!;
        }
    }

    // 3. Tab chậm hơn gửi token vừa bị đổi trong 30 giây: báo thử lại, không thu hồi phiên
    [Fact]
    public async Task Refresh_OldTokenWithinGrace_Retry_SessionKept()
    {
        var (session, refresh) = AddSession();
        var service = CreateService();
        var first = await service.RefreshAsync(refresh, CancellationToken.None);

        var second = await service.RefreshAsync(refresh, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Retry, second.Outcome);
        Assert.False(session.IsRevoked);
        Assert.Equal(RefreshOutcome.Ok, (await service.RefreshAsync(first.RefreshToken, CancellationToken.None)).Outcome);
    }

    // 4. Token cũ bị dùng lại sau 30 giây: coi là bị lộ, thu hồi cả phiên, token mới cũng mất tác dụng
    [Fact]
    public async Task Refresh_OldTokenAfterGrace_RevokesSession()
    {
        var (session, refresh) = AddSession();
        var service = CreateService();
        var first = await service.RefreshAsync(refresh, CancellationToken.None);
        session.UpdatedAt = DateTime.UtcNow - RefreshTokens.ReuseGrace - TimeSpan.FromSeconds(1);

        var reuse = await service.RefreshAsync(refresh, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Unauthorized, reuse.Outcome);
        Assert.True(session.IsRevoked);
        Assert.Equal(RefreshOutcome.Unauthorized, (await service.RefreshAsync(first.RefreshToken, CancellationToken.None)).Outcome);
    }

    // 5
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-co-trong-db")]
    public async Task Refresh_MissingOrUnknownToken_Unauthorized(string? raw)
    {
        AddSession();

        Assert.Equal(RefreshOutcome.Unauthorized, (await CreateService().RefreshAsync(raw, CancellationToken.None)).Outcome);
    }

    // 6. Phiên quá 7 ngày
    [Fact]
    public async Task Refresh_SessionExpired_Unauthorized()
    {
        var (session, refresh) = AddSession(expiresAt: DateTime.UtcNow.AddSeconds(-1));

        Assert.Equal(RefreshOutcome.Unauthorized, (await CreateService().RefreshAsync(refresh, CancellationToken.None)).Outcome);
        Assert.Equal(_tokens.HashToken(refresh), session.RefreshTokenHash);
    }

    // 7. Phiên đã thu hồi (đăng xuất, đổi mật khẩu, Admin khóa)
    [Fact]
    public async Task Refresh_SessionRevoked_Unauthorized()
    {
        var (session, refresh) = AddSession();
        session.IsRevoked = true;

        Assert.Equal(RefreshOutcome.Unauthorized, (await CreateService().RefreshAsync(refresh, CancellationToken.None)).Outcome);
    }

    // 8. Bị Admin khóa hoặc đã xóa thì không gia hạn, tạm khóa (temp_locked) thì vẫn được như OnTokenValidated
    [Theory]
    [InlineData(UserStatuses.Locked, RefreshOutcome.Unauthorized)]
    [InlineData(UserStatuses.Deleted, RefreshOutcome.Unauthorized)]
    [InlineData(UserStatuses.TempLocked, RefreshOutcome.Ok)]
    public async Task Refresh_UserStatus(string status, RefreshOutcome expected)
    {
        var (_, refresh) = AddSession();
        _user.Status = status;

        Assert.Equal(expected, (await CreateService().RefreshAsync(refresh, CancellationToken.None)).Outcome);
    }

    // 9. Đăng xuất bằng refresh token (access token đã hết hạn): phiên bị thu hồi, không gia hạn được nữa
    [Fact]
    public async Task Revoke_ByRefreshToken_RevokesSession()
    {
        var (session, refresh) = AddSession();
        var service = CreateService();

        await service.RevokeAsync(refresh, CancellationToken.None);

        Assert.True(session.IsRevoked);
        Assert.Equal(RefreshOutcome.Unauthorized, (await service.RefreshAsync(refresh, CancellationToken.None)).Outcome);
    }

    // 10. Refresh token là chuỗi ngẫu nhiên 256 bit dạng base64url, không trùng nhau
    [Fact]
    public void Generate_Is43CharBase64UrlAndUnique()
    {
        var a = RefreshTokens.Generate();
        var b = RefreshTokens.Generate();

        Assert.Equal(43, a.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
        Assert.NotEqual(a, b);
    }
}
