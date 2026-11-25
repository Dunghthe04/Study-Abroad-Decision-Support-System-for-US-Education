using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Import;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminKnowledgeBaseControllerTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private static SaveKnowledgeEntryRequest Request() =>
        new(7, "faq", "LABEL_VISA_F1", "OPT là gì?", "Trả lời", "Nguồn", null, new DateOnly(2026, 11, 1), null, null);

    private static AdminKnowledgeBaseController Controller(FakeKnowledgeRepository repo, bool withUser = true)
    {
        var controller = new AdminKnowledgeBaseController(
            new KnowledgeBaseService(repo, new FakeKnowledgeReader(), new FakeKnowledgeAdvisor(), new FakeAuditLogs(), NullLogger<KnowledgeBaseService>.Instance),
            new KnowledgeTemplateWriter())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = withUser
                        ? new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()),
                             new Claim(ClaimTypes.Role, UserRoles.Admin)], "TestAuth"))
                        : new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        return controller;
    }

    private static IFormFile File(string name, int length) =>
        new FormFile(new MemoryStream(new byte[length]), 0, length, "file", name);

    [Fact]
    public async Task EmbedStatus_And_StartEmbed_Return200WithTheAdvisorNumbers()
    {
        var controller = Controller(new FakeKnowledgeRepository());

        var status = Assert.IsType<OkObjectResult>((await controller.EmbedStatus(CancellationToken.None)).Result);
        var started = Assert.IsType<OkObjectResult>((await controller.StartEmbed(CancellationToken.None)).Result);

        Assert.Equal(494, Assert.IsType<KnowledgeEmbedStatusDto>(status.Value).Pending);
        Assert.True(Assert.IsType<KnowledgeEmbedStatusDto>(started.Value).Started);
    }

    [Fact]
    public void TheEmbedRoutes_AreOnlyForModeratorAndAdmin_LikeTheRestOfTheController()
    {
        var authorize = typeof(AdminKnowledgeBaseController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();

        Assert.Contains(UserRoles.Moderator, authorize.Roles!);
        Assert.Equal("embed-status", typeof(AdminKnowledgeBaseController).GetMethod(nameof(AdminKnowledgeBaseController.EmbedStatus))!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
        Assert.Equal("embed", typeof(AdminKnowledgeBaseController).GetMethod(nameof(AdminKnowledgeBaseController.StartEmbed))!
            .GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>().Single().Template);
    }

    [Fact]
    public async Task Create_Returns201_ThenSameEntryNoReturns409WithExistingId()
    {
        var controller = Controller(new FakeKnowledgeRepository());

        var created = Assert.IsType<CreatedAtActionResult>((await controller.Create(Request(), CancellationToken.None)).Result);
        var conflict = Assert.IsType<ConflictObjectResult>((await controller.Create(Request(), CancellationToken.None)).Result);

        var existingId = conflict.Value!.GetType().GetProperty("existingId")!.GetValue(conflict.Value);
        Assert.Equal(((KnowledgeEntryDto)created.Value!).Id, existingId);
    }

    [Fact]
    public async Task Delete_ThenUpdateReturns409_ThenRestoreReturns200()
    {
        var repo = new FakeKnowledgeRepository();
        var controller = Controller(repo);

        var created = (KnowledgeEntryDto)Assert.IsType<CreatedAtActionResult>((await controller.Create(Request(), CancellationToken.None)).Result).Value!;
        Assert.IsType<NoContentResult>(await controller.Delete(created.Id, CancellationToken.None));

        Assert.IsType<ConflictObjectResult>((await controller.Update(created.Id, Request() with { Answer = "Sửa" }, CancellationToken.None)).Result);

        var restored = Assert.IsType<OkObjectResult>((await controller.Restore(created.Id, CancellationToken.None)).Result);
        Assert.Null(Assert.IsType<KnowledgeEntryDto>(restored.Value).DeletedAt);
    }

    [Fact]
    public async Task Versions_And_RestoreVersion_WorkEndToEnd()
    {
        var controller = Controller(new FakeKnowledgeRepository());
        var created = (KnowledgeEntryDto)Assert.IsType<CreatedAtActionResult>((await controller.Create(Request(), CancellationToken.None)).Result).Value!;
        await controller.Update(created.Id, Request() with { Answer = "Trả lời mới" }, CancellationToken.None);

        var versionsRes = Assert.IsType<OkObjectResult>((await controller.GetVersions(created.Id, CancellationToken.None)).Result);
        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyList<KnowledgeEntryVersionDto>>(versionsRes.Value).Count);

        var restored = Assert.IsType<OkObjectResult>((await controller.RestoreVersion(created.Id, 1, CancellationToken.None)).Result);
        var dto = Assert.IsType<KnowledgeEntryDto>(restored.Value);
        Assert.Equal((3, "Trả lời"), (dto.Version, dto.Answer));
    }

    [Fact]
    public async Task Create_InvalidReturns400()
    {
        var bad = await Controller(new FakeKnowledgeRepository()).Create(Request() with { Question = " " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    [Fact]
    public async Task UnknownId_Returns404_ForGetUpdateDeleteRestoreVersions()
    {
        var controller = Controller(new FakeKnowledgeRepository());
        var id = Guid.NewGuid();

        Assert.IsType<NotFoundResult>((await controller.Get(id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetVersions(id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Update(id, Request(), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await controller.Delete(id, CancellationToken.None));
        Assert.IsType<NotFoundResult>((await controller.Restore(id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.RestoreVersion(id, 1, CancellationToken.None)).Result);
    }

    [Theory]
    [InlineData("data.csv", 10)]
    [InlineData("data.xlsx", 0)]
    [InlineData("data.xlsx", 10 * 1024 * 1024 + 1)]
    public async Task Import_RejectsWrongExtensionEmptyOrTooLarge(string name, int length)
    {
        var result = await Controller(new FakeKnowledgeRepository()).Import(File(name, length), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPage_UnknownSort_Returns400()
    {
        var result = await Controller(new FakeKnowledgeRepository())
            .GetPage(null, null, null, null, sort: "random", ct: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Template_IsXlsx_AndClassAllowsModeratorAndAdmin()
    {
        var file = Assert.IsType<FileContentResult>(Controller(new FakeKnowledgeRepository()).Template());
        var roles = typeof(AdminKnowledgeBaseController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal("knowledge-base-template.xlsx", file.FileDownloadName);
        Assert.Equal($"{UserRoles.Moderator},{UserRoles.Admin}", roles);
    }
}
