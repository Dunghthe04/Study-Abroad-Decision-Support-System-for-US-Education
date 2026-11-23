using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StudyAbroad.Api.Authorization;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// [UC-104] Chặn Ở SERVER: chưa đồng ý bản bắt buộc thì API cần đăng nhập trả 403 policy_acceptance_required.
public class PolicyAcceptanceMiddlewareTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Parent = Guid.NewGuid();

    private sealed class StubGate : IPolicyAcceptanceService
    {
        public bool Block { get; set; }
        public List<(Guid UserId, string Role)> Asked { get; } = [];

        public Task<bool> MustAcceptAsync(Guid userId, string role, CancellationToken ct = default)
        {
            Asked.Add((userId, role));
            return Task.FromResult(Block);
        }

        public Task<PolicyAcceptanceStatusDto> GetStatusAsync(Guid userId, string role, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PolicyAcceptanceStatusDto> AcceptAsync(Guid userId, string role, AcceptPoliciesRequest request, string? ipAddress, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static DefaultHttpContext Request(string path, bool authorized = true, bool loggedIn = true, string role = UserRoles.Student, string? actingParent = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Response.Body = new MemoryStream();

        var metadata = authorized ? new object[] { new AuthorizeAttribute() } : [new AllowAnonymousAttribute()];
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));

        if (loggedIn)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, User.ToString()), new(ClaimTypes.Role, role) };
            if (actingParent is not null)
                claims.Add(new Claim(ActingSessionGuard.ActorClaim, $"{{\"sub\":\"{actingParent}\"}}"));
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
        return http;
    }

    private static async Task<bool> Run(StubGate gate, DefaultHttpContext http)
    {
        var reachedNext = false;
        await new PolicyAcceptanceMiddleware(_ => { reachedNext = true; return Task.CompletedTask; }).InvokeAsync(http, gate);
        return reachedNext;
    }

    [Fact]
    public async Task Blocked_user_gets_403_with_the_stable_code_and_never_reaches_the_api()
    {
        var gate = new StubGate { Block = true };
        var http = Request("/api/v1/profile/academic");

        var reached = await Run(gate, http);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(http.Response.Body);
        Assert.Equal("policy_acceptance_required", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task User_who_accepted_passes_through()
    {
        var gate = new StubGate { Block = false };
        Assert.True(await Run(gate, Request("/api/v1/profile/academic")));
        Assert.Single(gate.Asked);
        Assert.Equal((User, UserRoles.Student), gate.Asked[0]);
    }

    [Theory]
    [InlineData("/api/v1/policy-acceptance")]      // đọc và ghi đồng ý: không thì không có cách nào hết bị chặn
    [InlineData("/api/v1/policies/terms_of_use")]  // đọc toàn văn
    [InlineData("/api/v1/auth/me")]                // biết mình là ai
    [InlineData("/api/v1/auth/logout")]            // đăng xuất
    [InlineData("/api/v1/auth/refresh")]
    public async Task Exempt_paths_are_never_blocked_and_the_gate_is_not_even_asked(string path)
    {
        var gate = new StubGate { Block = true };

        Assert.True(await Run(gate, Request(path)));
        Assert.Empty(gate.Asked);
    }

    [Fact]
    public async Task A_blocked_user_can_still_delete_their_own_account()
    {
        // DELETE /api/v1/auth/me nằm trong nhóm auth: người không đồng ý phải có đường rời đi
        var gate = new StubGate { Block = true };
        Assert.True(await Run(gate, Request("/api/v1/auth/me")));
    }

    [Fact]
    public async Task A_path_that_only_starts_with_an_exempt_word_is_not_exempt()
    {
        // "/api/v1/authority" không phải "/api/v1/auth": so theo từng đoạn đường dẫn, không so chữ đầu
        var gate = new StubGate { Block = true };
        Assert.False(await Run(gate, Request("/api/v1/authority/secrets")));
    }

    [Fact]
    public async Task Public_endpoints_are_not_checked_even_for_a_logged_in_user()
    {
        var gate = new StubGate { Block = true };
        Assert.True(await Run(gate, Request("/api/v1/universities", authorized: false)));
        Assert.Empty(gate.Asked);
    }

    [Fact]
    public async Task Visitors_are_not_checked()
    {
        var gate = new StubGate { Block = true };
        Assert.True(await Run(gate, Request("/api/v1/profile/academic", loggedIn: false)));
        Assert.Empty(gate.Asked);
    }

    [Fact]
    public async Task Parent_acting_for_a_managed_child_is_checked_as_the_parent()
    {
        var gate = new StubGate { Block = true };
        var http = Request("/api/v1/profile/academic", role: UserRoles.Student, actingParent: Parent.ToString());

        Assert.False(await Run(gate, http));
        Assert.Equal([(Parent, UserRoles.Parent)], gate.Asked);   // tài khoản con không bao giờ tự đồng ý được
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Center)]
    public async Task The_role_is_passed_on_so_the_service_decides_who_is_subject(string role)
    {
        var gate = new StubGate { Block = false };
        await Run(gate, Request("/api/v1/notifications", role: role));
        Assert.Equal([(User, role)], gate.Asked);
    }
}
