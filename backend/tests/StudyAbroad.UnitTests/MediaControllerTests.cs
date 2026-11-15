using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Api.MediaUploads;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.MediaUploads;

namespace StudyAbroad.UnitTests;

public class MediaControllerTests
{
    [Theory]
    [InlineData("school-logo", ImageUploadPolicy.SchoolLogoMaxBytes)]
    [InlineData("school-cover", ImageUploadPolicy.SchoolCoverMaxBytes)]
    public async Task Upload_ValidPurpose_ForwardsLimitAndReturnsMetadata(string purpose, int expectedLimit)
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);
        using var cancellation = new CancellationTokenSource();

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = purpose }, cancellation.Token);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(uploader.Result, ok.Value);
        Assert.Equal(expectedLimit, uploader.Request!.MaxSizeBytes);
        Assert.Equal(file.FileName, uploader.Request.FileName);
        Assert.Equal(file.ContentType, uploader.Request.ContentType);
        Assert.Equal(cancellation.Token, uploader.Token);
        Assert.Equal(1, uploader.Calls);
        Assert.Throws<ObjectDisposedException>(() => uploader.Request.Content.ReadByte());
    }

    [Fact]
    public async Task Upload_GlobalLimitIsLower_UsesGlobalLimit()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader, 100);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-cover" }, default);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(100, uploader.Request!.MaxSizeBytes);
    }

    [Fact]
    public async Task Upload_ClientSuppliedMaxSize_IsIgnored()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);
        controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["maxSizeBytes"] = "999999999",
            ["maxMegabytes"] = "9999",
        }, new FormFileCollection { file });

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(ImageUploadPolicy.SchoolLogoMaxBytes, uploader.Request!.MaxSizeBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("SCHOOL-LOGO")]
    [InlineData("../../school-logo")]
    public async Task Upload_UnknownPurpose_Returns400WithoutCallingUploader(string? purpose)
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = purpose }, default);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, uploader.Calls);
    }

    [Fact]
    public async Task Upload_MissingFile_Returns400()
    {
        using var provider = CreateServices(new FakeUploader());
        var controller = CreateController(provider);

        var response = await controller.UploadImage(new UploadImageForm { Purpose = "school-logo" }, default);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        using var provider = CreateServices(new FakeUploader());
        var file = CreateFile(0);
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task Upload_MultipleFiles_Returns400()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file, CreateFile());

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, uploader.Calls);
    }

    [Theory]
    [InlineData("school-logo", ImageUploadPolicy.SchoolLogoMaxBytes + 1)]
    [InlineData("school-cover", ImageUploadPolicy.SchoolCoverMaxBytes + 1)]
    public async Task Upload_OverPurposeLimit_Returns413BeforeOpeningStream(string purpose, long length)
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile(length);
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = purpose }, default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, uploader.Calls);
    }

    [Fact]
    public async Task Upload_OverGlobalLimit_Returns413()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader, 5);
        var file = CreateFile(6);
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-cover" }, default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, uploader.Calls);
    }

    [Fact]
    public async Task Upload_RequestAboveHardCeiling_Returns413()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);
        controller.Request.ContentLength = ImageUploadPolicy.MaxRequestBytes + 1;

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, uploader.Calls);
    }

    [Fact]
    public async Task Upload_InvalidImage_Returns400()
    {
        var uploader = new FakeUploader { Failure = new ArgumentException("Invalid image contents.") };
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal("Invalid image contents.", Assert.IsType<MediaUploadError>(badRequest.Value).Error);
    }

    [Fact]
    public async Task Upload_ActualBytesExceedLimit_Returns413()
    {
        var uploader = new FakeUploader { Failure = new MediaUploadSizeLimitException() };
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(response).StatusCode);
    }

    [Fact]
    public async Task Upload_ProviderFailure_ReturnsSanitized502()
    {
        var uploader = new FakeUploader { Failure = new InvalidOperationException("private-provider-secret") };
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        var error = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.DoesNotContain("private-provider-secret", Assert.IsType<MediaUploadError>(error.Value).Error);
    }

    [Fact]
    public async Task Upload_MissingConfiguration_ReturnsSanitized503()
    {
        var services = new ServiceCollection();
        services.AddCloudinaryMedia(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var file = CreateFile();
        var controller = CreateController(provider, file);

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        var error = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.DoesNotContain("ApiSecret", Assert.IsType<MediaUploadError>(error.Value).Error);
    }

    [Fact]
    public async Task Upload_MissingUserId_Returns401()
    {
        var uploader = new FakeUploader();
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var response = await controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, uploader.Calls);
    }

    [Fact]
    public async Task Upload_Cancelled_DoesNotMapCancellationToProviderError()
    {
        var uploader = new FakeUploader { Failure = new OperationCanceledException() };
        using var provider = CreateServices(uploader);
        var file = CreateFile();
        var controller = CreateController(provider, file);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            controller.UploadImage(new UploadImageForm { File = file, Purpose = "school-logo" }, default));

        Assert.Throws<ObjectDisposedException>(() => uploader.Request!.Content.ReadByte());
    }

    [Fact]
    public void Endpoint_RequiresAdminOrModeratorAndHasBoundedMultipartAndRateLimit()
    {
        var controller = typeof(MediaController);
        var authorization = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", authorization!.Roles);
        Assert.Equal("api/v1/media", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        var action = controller.GetMethod(nameof(MediaController.UploadImage))!;
        Assert.Equal("images", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal(MediaUploadRateLimit.PolicyName, action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        var sizeLimit = (Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)action.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        Assert.Equal(ImageUploadPolicy.MaxRequestBytes, sizeLimit.MaxRequestBodySize);
        Assert.Equal(ImageUploadPolicy.MaxFileBytes, action.GetCustomAttribute<RequestFormLimitsAttribute>()!.MultipartBodyLengthLimit);
        Assert.True(ImageUploadPolicy.MaxRequestBytes <= 20 * 1024 * 1024 + 64 * 1024);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task RateLimit_ExcessUploads_Returns429WithRetryAfterAndJson()
    {
        var policy = new MediaUploadRateLimit.Policy();
        var context = new DefaultHttpContext();
        context.User = Principal(Guid.NewGuid());
        context.Response.Body = new MemoryStream();
        var partition = policy.GetPartition(context);
        using var limiter = partition.Factory(partition.PartitionKey);
        for (var count = 0; count < MediaUploadRateLimit.PermitsPerWindow; count++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }
        using var rejected = limiter.AttemptAcquire();
        Assert.False(rejected.IsAcquired);

        await policy.OnRejected!(new OnRejectedContext { HttpContext = context, Lease = rejected }, default);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.True(int.Parse(context.Response.Headers.RetryAfter.ToString()) > 0);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        Assert.Contains("Too many image uploads", body.GetProperty("error").GetString());
    }

    [Fact]
    public void RateLimit_PartitionsByAuthenticatedUser()
    {
        var policy = new MediaUploadRateLimit.Policy();
        var first = new DefaultHttpContext { User = Principal(Guid.NewGuid()) };
        var second = new DefaultHttpContext { User = Principal(Guid.NewGuid()) };

        Assert.NotEqual(policy.GetPartition(first).PartitionKey, policy.GetPartition(second).PartitionKey);
        Assert.Equal(policy.GetPartition(first).PartitionKey, policy.GetPartition(first).PartitionKey);
    }

    private static ServiceProvider CreateServices(FakeUploader uploader, int maxBytes = 10 * 1024 * 1024)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediaUploader>(uploader);
        services.AddSingleton<IOptions<CloudinaryOptions>>(Options.Create(new CloudinaryOptions { MaxFileSizeBytes = maxBytes }));
        return services.BuildServiceProvider();
    }

    private static FormFile CreateFile(long length = 10) => new(new MemoryStream(new byte[10]), 0, length, "file", "image.png")
    {
        Headers = new HeaderDictionary(),
        ContentType = "image/png",
    };

    private static MediaController CreateController(IServiceProvider provider, params IFormFile[] files)
    {
        var fileCollection = new FormFileCollection();
        foreach (var file in files)
            fileCollection.Add(file);
        var context = new DefaultHttpContext
        {
            User = Principal(Guid.NewGuid()),
            RequestServices = provider,
        };
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), fileCollection);
        return new MediaController(provider, provider.GetRequiredService<IOptions<CloudinaryOptions>>())
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim(ClaimTypes.Role, UserRoles.Admin),
    ], "test"));

    private sealed class FakeUploader : IMediaUploader
    {
        public int Calls { get; private set; }
        public MediaUploadRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; init; }
        public MediaUploadResult Result { get; } = new("https://cdn.example/image.png", "usas/image", "image/png", 10, 2, 2);

        public Task<MediaUploadResult> UploadImageAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            Token = cancellationToken;
            if (Failure is not null)
                throw Failure;
            return Task.FromResult(Result);
        }
    }
}
