using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-86: controller trả đúng mã HTTP. Quyền chi tiết (ai xem, ai sửa) đã test ở ScenarioServiceTests.
public class ScenariosControllerTests
{
    private readonly FakeScenarioRepository _scenarios = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly Guid _student = Guid.NewGuid();

    private ScenariosController CreateController(Guid userId)
    {
        var service = new ScenarioService(_scenarios, new ScenarioServiceTests.FakeSimulator(), _links);
        return new ScenariosController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Student),
                    ], "TestAuth"))
                }
            }
        };
    }

    private static ScenariosController.SaveScenarioRequest Request(string? title = "Tập trung SAT", IReadOnlyList<int>? pref = null) =>
        new(title, null, 1450m, null, null, null, null, null, pref);

    private async Task<SavedScenarioDto> SaveAsync()
    {
        var result = await CreateController(_student).Save(Request(), CancellationToken.None);
        return Assert.IsType<SavedScenarioDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
    }

    [Fact]
    public async Task Save_Valid_Returns201()
    {
        var saved = await SaveAsync();
        Assert.Equal("Tập trung SAT", saved.Title);
        Assert.Single(_scenarios.Rows);
    }

    [Theory]
    [InlineData(new[] { 2, 2 })]          // thiếu một số
    [InlineData(new[] { 2, 5, 2 })]       // vượt quá 4
    public async Task Save_BadPref_Returns400(int[] pref)
    {
        var result = await CreateController(_student).Save(Request(pref: pref), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(_scenarios.Rows);
    }

    [Fact]
    public async Task Save_BlankTitle_Returns400()
    {
        var result = await CreateController(_student).Save(Request(title: " "), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task List_Mine_Returns200()
    {
        await SaveAsync();
        var result = await CreateController(_student).List(null, CancellationToken.None);
        var list = Assert.IsAssignableFrom<IReadOnlyList<SavedScenarioDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task OtherUser_GetsNotFound()
    {
        var saved = await SaveAsync();
        var other = CreateController(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(await other.Get(saved.Id, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await other.List(_student, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await other.Rename(saved.Id, new("Đổi trộm"), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await other.Delete(saved.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Rename_Returns200_BlankReturns400()
    {
        var saved = await SaveAsync();
        var controller = CreateController(_student);

        var ok = Assert.IsType<OkObjectResult>(await controller.Rename(saved.Id, new("SAT 1.450"), CancellationToken.None));
        Assert.Equal("SAT 1.450", Assert.IsType<SavedScenarioDto>(ok.Value).Title);
        Assert.IsType<BadRequestObjectResult>(await controller.Rename(saved.Id, new(""), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_Returns204_ThenNotFound()
    {
        var saved = await SaveAsync();
        var controller = CreateController(_student);

        Assert.IsType<NoContentResult>(await controller.Delete(saved.Id, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Delete(saved.Id, CancellationToken.None));
    }
}
