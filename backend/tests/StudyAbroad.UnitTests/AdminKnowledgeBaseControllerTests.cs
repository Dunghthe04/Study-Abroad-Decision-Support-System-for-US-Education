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
    private static SaveKnowledgeEntryRequest Request() =>
        new(7, "faq", "LABEL_VISA_F1", "OPT là gì?", "Trả lời", "Nguồn", null, new DateOnly(2026, 11, 1), null, null, null);

    private static AdminKnowledgeBaseController Controller(FakeKnowledgeRepository repo) =>
        new(new KnowledgeBaseService(repo, new FakeKnowledgeReader(), new FakeKnowledgeAdvisor(), NullLogger<KnowledgeBaseService>.Instance), new KnowledgeTemplateWriter());

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
    public async Task Create_InvalidReturns400()
    {
        var bad = await Controller(new FakeKnowledgeRepository()).Create(Request() with { Question = " " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    [Fact]
    public async Task UnknownId_Returns404_ForGetUpdateDelete()
    {
        var controller = Controller(new FakeKnowledgeRepository());
        var id = Guid.NewGuid();

        Assert.IsType<NotFoundResult>((await controller.Get(id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Update(id, Request(), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await controller.Delete(id, CancellationToken.None));
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