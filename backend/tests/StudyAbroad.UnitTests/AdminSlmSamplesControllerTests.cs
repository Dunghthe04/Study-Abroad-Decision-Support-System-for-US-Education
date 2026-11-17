using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
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
    public async Task Import_FileOverTenMegabytes_Returns400()
    {
        var controller = CreateController(new FakeSlmSampleService());
        var file = MakeFormFile("data.xlsx", new byte[10 * 1024 * 1024 + 1]);

        var result = await controller.Import(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Preview_ValidFile_Returns200WithPreviewAndDoesNotNeedUser()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service, withUser: false);
        var file = MakeFormFile("data.xlsx", new byte[] { 1, 2, 3 });

        var result = await controller.Preview(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SlmImportPreviewDto>(ok.Value);
        Assert.Equal(1, preview.TotalRows);
        Assert.Equal(1, service.PreviewCalls);
    }

    [Fact]
    public async Task Preview_NullFile_Returns400WithoutCallingService()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);

        var result = await controller.Preview(null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("error", System.Text.Json.JsonSerializer.Serialize(bad.Value));
        Assert.Equal(0, service.PreviewCalls);
    }

    [Fact]
    public async Task Preview_EmptyFile_Returns400WithoutCallingService()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);
        var file = MakeFormFile("data.xlsx", Array.Empty<byte>());

        var result = await controller.Preview(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, service.PreviewCalls);
    }

    [Theory]
    [InlineData("data.xls")]
    [InlineData("data.csv")]
    [InlineData("data.xlsx.exe")]
    public async Task Preview_NonXlsxName_Returns400WithoutCallingService(string fileName)
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);
        var file = MakeFormFile(fileName, new byte[] { 1, 2, 3 });

        var result = await controller.Preview(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, service.PreviewCalls);
    }

    [Fact]
    public async Task Preview_FileOverTenMegabytes_Returns400WithoutCallingService()
    {
        var service = new FakeSlmSampleService();
        var controller = CreateController(service);
        var file = MakeFormFile("data.xlsx", new byte[10 * 1024 * 1024 + 1]);

        var result = await controller.Preview(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, service.PreviewCalls);
    }

    [Fact]
    public async Task Preview_ServiceThrowsInvalidDataException_Returns400WithMessage()
    {
        var service = new FakeSlmSampleService { ThrowInvalidData = true };
        var controller = CreateController(service);
        var file = MakeFormFile("data.xlsx", new byte[] { 1, 2, 3 });

        var result = await controller.Preview(file, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("corrupt file", System.Text.Json.JsonSerializer.Serialize(bad.Value));
    }

    [Fact]
    public void Template_ReturnsXlsxFileWithExpectedNameAndContentType()
    {
        var controller = CreateController(new FakeSlmSampleService());

        var result = controller.Template();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("slm-training-template.xlsx", file.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public void PreviewAndTemplate_RoutesAreDeclared()
    {
        var preview = typeof(AdminSlmSamplesController).GetMethod(nameof(AdminSlmSamplesController.Preview))!;
        var template = typeof(AdminSlmSamplesController).GetMethod(nameof(AdminSlmSamplesController.Template))!;

        Assert.Equal("preview", ((HttpPostAttribute)Attribute.GetCustomAttribute(preview, typeof(HttpPostAttribute))!).Template);
        Assert.Equal("template", ((HttpGetAttribute)Attribute.GetCustomAttribute(template, typeof(HttpGetAttribute))!).Template);
        Assert.NotNull(Attribute.GetCustomAttribute(preview, typeof(RequestSizeLimitAttribute)));
    }

    private static IFormFile MakeFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    private static readonly Guid DuplicateId = Guid.NewGuid();

    [Fact]
    public async Task Create_Duplicate_Returns409WithExistingId()
    {
        var controller = CreateController(new FakeSlmSampleService { ThrowDuplicate = true });

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains(DuplicateId.ToString(), System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }

    [Fact]
    public async Task Delete_PassesTheActor()
    {
        var service = new FakeSlmSampleService();

        await CreateController(service).Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ActorId, service.DeletedBy);
    }

    [Fact]
    public void Export_IsAdminOnly_WhileTheControllerAllowsModerators()
    {
        var method = typeof(AdminSlmSamplesController).GetMethod(nameof(AdminSlmSamplesController.Export))!;
        var classRoles = typeof(AdminSlmSamplesController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles;
        var actionRoles = method
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles;

        Assert.Contains(UserRoles.Moderator, classRoles);
        Assert.Equal(UserRoles.Admin, actionRoles);
        Assert.Equal("export", method.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template);
    }

    [Fact]
    public async Task Export_ReturnsFile_AndBadFormatIs400()
    {
        var ok = await CreateController(new FakeSlmSampleService()).Export("jsonl", CancellationToken.None);
        var bad = await CreateController(new FakeSlmSampleService { ThrowArgument = true }).Export("csv", CancellationToken.None);

        Assert.Equal("x.jsonl", Assert.IsType<FileContentResult>(ok).FileDownloadName);
        Assert.IsType<BadRequestObjectResult>(bad);
    }

    private class FakeSlmSampleService : ISlmSampleService
    {
        public bool ThrowArgument { get; init; }
        public bool ThrowInvalidData { get; init; }
        public bool DeleteResult { get; init; } = true;
        public int PreviewCalls { get; private set; }

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
            if (ThrowDuplicate) throw new SlmDuplicateSampleException(DuplicateId);
            return Task.FromResult(MakeDto());
        }

        public Task<SlmSampleDto?> UpdateAsync(Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad request");
            return Task.FromResult<SlmSampleDto?>(MakeDto());
        }

        public Guid? DeletedBy { get; private set; }
        public bool ThrowDuplicate { get; init; }
        public string? ExportedFormat { get; private set; }

        public Task<bool> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
        {
            DeletedBy = actorUserId;
            return Task.FromResult(DeleteResult);
        }

        public Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<SlmSampleDetailDto?>(null);

        public Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlmSampleCreatorDto>>([]);

        public Task<bool> CaptureUnansweredAsync(string? question, CancellationToken ct = default) => Task.FromResult(false);

        public Task<SlmExportFile> ExportAsync(
            string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            if (ThrowArgument) throw new ArgumentException("bad format");
            ExportedFormat = format;
            return Task.FromResult(new SlmExportFile([1], "application/x-ndjson", "x.jsonl"));
        }

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

        public Task<SlmImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default)
        {
            PreviewCalls++;
            if (ThrowInvalidData) throw new InvalidDataException("corrupt file");
            return Task.FromResult(new SlmImportPreviewDto(1, [], []));
        }
    }
}
