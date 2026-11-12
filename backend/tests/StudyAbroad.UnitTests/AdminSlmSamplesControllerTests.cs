using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
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
        var controller = new AdminSlmSamplesController(service)
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
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_InvalidRequest_Returns400WithError()
    {
        var service = new FakeSlmSampleService { ThrowArgument = true };
        var controller = CreateController(service);

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Create_NoUserId_Returns401()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service, withUser: false);

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Update_InvalidRequest_Returns400WithError()
    {
        var service = new FakeSlmSampleService { ThrowArgument = true };
        var controller = CreateController(service);

        var result = await controller.Update(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Update_NoUserId_Returns401()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service, withUser: false);

        var result = await controller.Update(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Review_InvalidRequest_Returns400WithError()
    {
        var service = new FakeSlmSampleService { ThrowArgument = true };
        var controller = CreateController(service);

        var result = await controller.Review(
            new ReviewSlmSamplesRequest([Guid.NewGuid()], "bad_status", null), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Review_NoUserId_Returns401()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service, withUser: false);

        var result = await controller.Review(
            new ReviewSlmSamplesRequest([Guid.NewGuid()], SlmReviewStatuses.Approved, null), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Import_NullFile_Returns400()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);

        var result = await controller.Import(null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Import_NonXlsxName_Returns400()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);
        var file = MakeFormFile("test.csv", new byte[] { 1, 2, 3 });

        var result = await controller.Import(file, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Import_EmptyFile_Returns400()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);
        var file = MakeFormFile("data.xlsx", Array.Empty<byte>());

        var result = await controller.Import(file, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Import_ServiceThrowsInvalidDataException_Returns400()
    {
        var service = new FakeSlmSampleService { ThrowInvalidData = true };
        var controller = CreateController(service);
        var file = MakeFormFile("data.xlsx", new byte[] { 1, 2, 3 });

        var result = await controller.Import(file, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204()
    {
        var service = new FakeSlmSampleService { DeleteResult = true };
        var controller = CreateController(service);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        var service = new FakeSlmSampleService { DeleteResult = false };
        var controller = CreateController(service);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Export_NoSplit_ReturnsFileNamedSlmTrainingJsonlWithNdjsonContentType()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);

        var result = await controller.Export(null, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("slm_training.jsonl", file.FileDownloadName);
        Assert.Equal("application/x-ndjson", file.ContentType);
    }

    [Fact]
    public async Task Export_BadSplit_Returns400()
    {
        var service = new FakeSlmSampleService { ThrowArgument = true };
        var controller = CreateController(service);

        var result = await controller.Export("notavalidsplit", CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json);
    }

    private static IFormFile MakeFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    private class FakeSlmSampleService : ISlmSampleService
    {
        public bool ThrowArgument { get; init; }
        public bool ThrowInvalidData { get; init; }
        public bool DeleteResult { get; init; } = true;

        private static SlmSampleDto MakeDto() => new(
            Guid.NewGuid(), SlmSampleTypes.Knowledge, "OPT", null, null, ValidIntents,
            null, SlmSampleSplits.Train, null, SlmReviewStatuses.Draft, null, null, null,
            DateTime.UtcNow, null);

        public Task<PagedResult<SlmSampleDto>> GetPageAsync(
            SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad filter");
            return Task.FromResult(new PagedResult<SlmSampleDto>([], page, pageSize, 0));
        }

        public Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new SlmSampleSummaryDto(0,
                new Dictionary<string, int>(), new Dictionary<string, int>()));

        public Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<SlmSampleDto?>(null);

        public Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad request");
            return Task.FromResult(MakeDto());
        }

        public Task<SlmSampleDto?> UpdateAsync(Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad request");
            return Task.FromResult<SlmSampleDto?>(MakeDto());
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(DeleteResult);

        public Task<ReviewSlmSamplesResultDto> ReviewAsync(ReviewSlmSamplesRequest request, Guid actorUserId, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad status");
            return Task.FromResult(new ReviewSlmSamplesResultDto(1, 0));
        }

        public Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default)
        {
            if (ThrowInvalidData) throw new InvalidDataException("corrupt file");
            return Task.FromResult(new SlmImportResultDto(1, 1, 0, []));
        }

        public Task<string> ExportJsonlAsync(string? split, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad split");
            return Task.FromResult("{\"type\":\"knowledge\"}\n");
        }
    }
}
