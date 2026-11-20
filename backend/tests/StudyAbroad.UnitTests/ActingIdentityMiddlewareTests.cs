using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using StudyAbroad.Api.Authorization;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// 1.5b: đang nhập cho con managed thì chỉ API hồ sơ chạy dưới tên con, mọi API khác chạy dưới tên phụ huynh
public class ActingIdentityMiddlewareTests
{
    private readonly FakeUserRepository _users = new();
    private readonly User _parent = new() { Id = Guid.NewGuid(), Email = "p@test.com", FullName = "Phụ huynh", Role = UserRoles.Parent, Status = UserStatuses.Active };
    private readonly Guid _childId = Guid.NewGuid();

    public ActingIdentityMiddlewareTests() => _users.Users.Add(_parent);

    private ClaimsPrincipal ChildPrincipal(bool acting = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _childId.ToString()),
            new(ClaimTypes.Role, UserRoles.Student),
            new("status", UserStatuses.Managed),
        };
        if (acting)
            claims.Add(new Claim(ActingSessionGuard.ActorClaim, JsonSerializer.Serialize(new { sub = _parent.Id.ToString() })));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static Endpoint ControllerEndpoint(string controller) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller }), controller);

    // Chạy middleware, trả về danh tính mà controller nhìn thấy (null nếu không đi tiếp)
    private async Task<(ClaimsPrincipal? Seen, int Status)> RunAsync(ClaimsPrincipal user, string? controller)
    {
        var context = new DefaultHttpContext { User = user };
        if (controller is not null)
            context.SetEndpoint(ControllerEndpoint(controller));
        ClaimsPrincipal? seen = null;
        var middleware = new ActingIdentityMiddleware(ctx => { seen = ctx.User; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, _users);
        return (seen, context.Response.StatusCode);
    }

    private static string? Id(ClaimsPrincipal? p) => p?.FindFirstValue(ClaimTypes.NameIdentifier);

    [Theory]
    [InlineData("ProfileFlow")]
    [InlineData("Recommendations")]
    [InlineData("TargetSchools")]
    [InlineData("Scenarios")]
    [InlineData("Advisor")]
    [InlineData("Acting")]
    public async Task ProfileApis_KeepChildIdentity(string controller)
    {
        var (seen, _) = await RunAsync(ChildPrincipal(), controller);

        Assert.Equal(_childId.ToString(), Id(seen));
        Assert.NotNull(ActingSessionGuard.ReadActingParentId(seen));
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("StudyCenters")]
    [InlineData("Universities")]
    [InlineData("Notifications")]
    [InlineData("ParentChildren")]
    public async Task OtherApis_RunAsParent(string controller)
    {
        var (seen, _) = await RunAsync(ChildPrincipal(), controller);

        Assert.Equal(_parent.Id.ToString(), Id(seen));
        Assert.True(seen!.IsInRole(UserRoles.Parent));
        Assert.Equal(_parent.Status, seen.FindFirstValue("status"));
        Assert.Null(ActingSessionGuard.ReadActingParentId(seen));   // không còn act: API phụ huynh coi như phụ huynh tự gọi
        Assert.True(seen.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task NoEndpoint_RunsAsParent()
    {
        var (seen, _) = await RunAsync(ChildPrincipal(), controller: null);

        Assert.Equal(_parent.Id.ToString(), Id(seen));
    }

    [Fact]
    public async Task NotActing_IdentityUnchanged()
    {
        var user = ChildPrincipal(acting: false);

        var (seen, _) = await RunAsync(user, "StudyCenters");

        Assert.Same(user, seen);
    }

    [Fact]
    public async Task Anonymous_IdentityUnchanged()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var (seen, _) = await RunAsync(anonymous, "Universities");

        Assert.Same(anonymous, seen);
    }

    [Fact]
    public async Task ParentMissing_Returns401()
    {
        _users.Users.Clear();

        var (seen, status) = await RunAsync(ChildPrincipal(), "StudyCenters");

        Assert.Null(seen);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }
}
