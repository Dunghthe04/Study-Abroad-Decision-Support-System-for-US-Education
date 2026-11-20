using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-76 (2c): controller chỉ đổi kết quả service ra mã HTTP: 200 / 400 / 404 / 409, thiếu user → 401.
public class ParentPreferenceControllerTests
{
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeUserRepository _users = new();
    private readonly ParentPreferenceServiceTests.FakeRunner _runner = new();

    private readonly Guid _parent = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();

    private ParentPreferenceService Service() =>
        new(_links, _users, _runner, new ParentPreferenceServiceTests.FakeRates(),
            new ParentPreferenceServiceTests.FakeProfiles(), new ParentPreferenceServiceTests.FakeNotifications());

    private static ControllerContext Ctx(params Claim[] claims) => new()
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
    };

    private ParentChildPreferenceController ParentController() =>
        new(Service()) { ControllerContext = Ctx(new Claim(ClaimTypes.NameIdentifier, _parent.ToString()), new Claim(ClaimTypes.Role, UserRoles.Parent)) };

    private ParentPreferencesController ChildController() =>
        new(Service()) { ControllerContext = Ctx(new Claim(ClaimTypes.NameIdentifier, _child.ToString()), new Claim(ClaimTypes.Role, UserRoles.Student)) };

    private void Link()
    {
        _users.Users.Add(new User { Id = _child, Email = "con@test.com", Status = UserStatuses.Active });
        _links.Links.Add(new ParentStudentLink { StudentUserId = _child, ParentUserId = _parent, ParentEmail = "p@test.com", Status = ParentLinkStatuses.Active });
    }

    private static int? Status(IActionResult r) => r switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null,
    };

    // 1
    [Fact]
    public async Task Save_And_Compare_200()
    {
        Link();
        var c = ParentController();
        Assert.Equal(200, Status(await c.Save(_child, new SaveParentPreferenceRequest([4, 2, 0], null), CancellationToken.None)));
        Assert.Equal(200, Status(await c.Get(_child, CancellationToken.None)));
        Assert.Equal(200, Status(await c.Compare(_child, "4,2,0", null, CancellationToken.None)));
        Assert.Equal(200, Status(await ChildController().Get(null, CancellationToken.None)));
    }

    // 2
    [Fact]
    public async Task BadInput_400()
    {
        Link();
        var c = ParentController();
        Assert.Equal(400, Status(await c.Save(_child, new SaveParentPreferenceRequest([9, 2, 0], null), CancellationToken.None)));
        Assert.Equal(400, Status(await c.Compare(_child, "4,x,0", null, CancellationToken.None)));
        Assert.Equal(400, Status(await c.Compare(_child, null, 1, CancellationToken.None)));
    }

    // 3
    [Fact]
    public async Task NoLink_404()
    {
        _users.Users.Add(new User { Id = _child, Email = "con@test.com", Status = UserStatuses.Active });
        var c = ParentController();
        Assert.Equal(404, Status(await c.Get(_child, CancellationToken.None)));
        Assert.Equal(404, Status(await c.Compare(_child, null, null, CancellationToken.None)));
        Assert.Equal(404, Status(await ChildController().Get(Guid.NewGuid(), CancellationToken.None)));
    }

    // 4
    [Fact]
    public async Task ProfileNotReady_409()
    {
        Link();
        _runner.Latest = new Recommendation { UserId = _child };
        _runner.Throw = true;
        Assert.Equal(409, Status(await ParentController().Compare(_child, "4,2,0", null, CancellationToken.None)));
    }

    // 5
    [Fact]
    public async Task NoUser_401()
    {
        var c = new ParentChildPreferenceController(Service()) { ControllerContext = Ctx() };
        Assert.Equal(401, Status(await c.Get(_child, CancellationToken.None)));
    }
}
