using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

// Controller chỉ đổi kết quả của service ra mã HTTP: có liên kết → 200, không → 404, thiếu user → 401.
public class ParentChildrenControllerTests
{
    private readonly FakeParentLinkRepository _links = new();
    private readonly ParentChildrenServiceTests.FakeTargetSchools _targets = new();

    private readonly Guid _parent = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();

    private ParentChildrenController CreateController(params Claim[] claims) =>
        new(new ParentChildrenService(_links, _targets))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            }
        };

    private ParentChildrenController CreateParentController() =>
        CreateController(new Claim(ClaimTypes.NameIdentifier, _parent.ToString()), new Claim(ClaimTypes.Role, UserRoles.Parent));

    // 1
    [Fact]
    public async Task Linked_Returns200WithChildSchools()
    {
        _links.Links.Add(new ParentStudentLink { StudentUserId = _child, ParentUserId = _parent, ParentEmail = "p@test.com", Status = ParentLinkStatuses.Active });
        _targets.Items.Add(new TargetSchoolDto(Guid.NewGuid(), _child, Guid.NewGuid(), "A", "A",
            null, null, null, null, null, "match", "considering", 0, null, null, null, DateTime.UtcNow, null));

        var result = await CreateParentController().GetTargetSchools(_child, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<TargetSchoolDto>>(ok.Value);
        Assert.Equal("A", Assert.Single(list).UniversityName);
    }

    // 2
    [Fact]
    public async Task NotLinked_Returns404()
    {
        var result = await CreateParentController().GetTargetSchools(_child, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // 3
    [Fact]
    public async Task NoUserId_Returns401()
    {
        var result = await CreateController(new Claim(ClaimTypes.Role, UserRoles.Parent)).GetTargetSchools(_child, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
