using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Import;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminSlmSamplesControllerTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private const string ValidIntents = "[{\"type\":\"knowledge\",\"span\":\"OPT là gì\",\"entities\":{}}]";

    private static SaveSlmSampleRequest ValidRequest() =>
        new(SlmSampleTypes.Knowledge, "OPT là gì", null, null, ValidIntents, null, null, null);

    private static AdminSlmSamplesController CreateController(FakeSlmSampleService service, bool withUser = true)
    {
        var controller = new AdminSlmSamplesController(service, new SlmTemplateWriter())
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

    [Fact]
    public async Task Create_ValidRequest_Returns201()
    {
        var controller = CreateController(new FakeSlmSampleService());
        var result = await controller.Create(ValidRequest(), CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_InvalidRequest_Returns400WithError()
    {
        var controller = CreateController(new FakeSlmSampleService { ThrowArgument = true });
        var result = await controller.Create(ValidRequest(), CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("error", System.Text.Json.JsonSerializer.Serialize(bad.Value));
    }

    [Fact]
    public async Task Create_NoUserId_Returns401()
    {
        var controller = CreateController(new FakeSlmSampleService(), withUser: false);
        var result = await controller.Create(ValidRequest(), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Update_DeletedSample_Returns409()
    {
        var controller = CreateController(new FakeSlmSampleService { ThrowDeleted = true });
        var result = await controller.Update(Guid.NewGuid(), ValidRequest(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Restore_And_RestoreVersion_Return200Or404()
    {
        var svc = new FakeSlmSampleService();
        var controller = CreateController(svc);
        Assert.IsType<OkObjectResult>((await controller.Restore(Guid.NewGuid(), CancellationToken.None)).Result);
        Assert.IsType<OkObjectResult>((await controller.RestoreVersion(Guid.NewGuid(), 1, CancellationToken.None)).Result);

        var missingCtrl = CreateController(new FakeSlmSampleService { ReturnNull = true });
        Assert.IsType<NotFoundResult>((await missingCtrl.Restore(Guid.NewGuid(), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await missingCtrl.RestoreVersion(Guid.NewGuid(), 1, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task GetSources_Returns200WithCustomSources()
    {
        var controller = CreateController(new FakeSlmSampleService());
        var ok = Assert.IsType<OkObjectResult>((await controller.GetSources(CancellationToken.None)).Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<string>>(ok.Value);
        Assert.Contains("Website ĐSQ", list);
    }

    [Fact]
    public async Task GetPage_PassesDeletedFlagToService()
    {
        var svc = new FakeSlmSampleService();
        var controller = CreateController(svc);

        await controller.GetPage(null, null, null, deleted: true, ct: CancellationToken.None);

        Assert.True(svc.LastFilter!.Deleted);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204_AndMissingReturns404()
    {
        Assert.IsType<NoContentResult>(await CreateController(new FakeSlmSampleService { DeleteResult = true }).Delete(Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(new FakeSlmSampleService { DeleteResult = false }).Delete(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Import_ValidatesExtensionEmptyAndMaxSize()
    {
        var controller = CreateController(new FakeSlmSampleService());
        Assert.IsType<BadRequestObjectResult>((await controller.Import(null, CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Import(MakeFormFile("a.csv", [1]), CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Import(MakeFormFile("a.xlsx", []), CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Import(MakeFormFile("a.xlsx", new byte[10 * 1024 * 1024 + 1]), CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Preview_ValidFile_Returns200WithoutUser()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service, withUser: false);
        var ok = Assert.IsType<OkObjectResult>((await controller.Preview(MakeFormFile("a.xlsx", [1, 2, 3]), CancellationToken.None)).Result);
        Assert.Equal(1, Assert.IsType<SlmImportPreviewDto>(ok.Value).TotalRows);
    }

    [Fact]
    public async Task Export_ReturnsFile()
    {
        var controller = CreateController(new FakeSlmSampleService());
        var file = Assert.IsType<FileContentResult>(await controller.Export("jsonl", CancellationToken.None));
        Assert.Equal("application/x-ndjson", file.ContentType);
    }

    private static IFormFile MakeFormFile(string name, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private sealed class FakeSlmSampleService : ISlmSampleService
    {
        public bool ThrowArgument { get; init; }
        public bool ThrowDeleted { get; init; }
        public bool ReturnNull { get; init; }
        public bool DeleteResult { get; init; } = true;
        public SlmSampleFilter? LastFilter { get; private set; }

        private static SlmSampleDto Sample() => new(
            Guid.NewGuid(), SlmSampleTypes.Knowledge, "OPT là gì", null, null, ValidIntents, null,
            SlmSampleSplits.Train, null, DateTime.UtcNow, null);

        public Task<PagedResult<SlmSampleDto>> GetPageAsync(SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default)
        {
            LastFilter = filter;
            return Task.FromResult(new PagedResult<SlmSampleDto>([], page, pageSize, 0));
        }

        public Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new SlmSampleSummaryDto(0, 0, new Dictionary<string, int>()));

        public Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<SlmSampleDto?>(ReturnNull ? null : Sample());

        public Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<SlmSampleDetailDto?>(ReturnNull ? null : new SlmSampleDetailDto(Sample(), []));

        public Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlmSampleCreatorDto>>([]);

        public Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["manual", "excel", "Website ĐSQ"]);

        public Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad");
            return Task.FromResult(Sample());
        }

        public Task<SlmSampleDto?> UpdateAsync(Guid id, SaveSlmSampleRequest request, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad");
            if (ThrowDeleted) throw new SlmSampleDeletedException();
            return Task.FromResult<SlmSampleDto?>(ReturnNull ? null : Sample());
        }

        public Task<bool> DeleteAsync(Guid id, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
            Task.FromResult(DeleteResult);

        public Task<SlmSampleDto?> RestoreAsync(Guid id, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
            Task.FromResult<SlmSampleDto?>(ReturnNull ? null : Sample());

        public Task<SlmSampleDto?> RestoreVersionAsync(Guid id, int version, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (ThrowDeleted) throw new SlmSampleDeletedException();
            return Task.FromResult<SlmSampleDto?>(ReturnNull ? null : Sample());
        }

        public Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
            Task.FromResult(new SlmImportResultDto(1, 1, 0, []));

        public Task<SlmImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default) =>
            Task.FromResult(new SlmImportPreviewDto(1, [], []));

        public Task<SlmExportFile> ExportAsync(string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default) =>
            Task.FromResult(new SlmExportFile("{}"u8.ToArray(), "application/x-ndjson", "slm-samples.jsonl"));
    }
}
