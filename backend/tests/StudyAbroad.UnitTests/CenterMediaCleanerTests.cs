using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Infrastructure.MediaUploads;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-93] Xóa ảnh và tài liệu của trung tâm trên Cloudinary khi xóa hẳn trung tâm.</summary>
public class CenterMediaCleanerTests
{
    private const string Logo = "https://res.cloudinary.com/demo/image/upload/v1/usas/centers/logo.png";
    private const string Cover = "https://res.cloudinary.com/demo/image/upload/v1/usas/centers/cover.png";

    private sealed class FakeUploader : IMediaUploader
    {
        public List<string> Deleted { get; } = [];
        public HashSet<string> Throwing { get; } = [];
        public HashSet<string> Invalid { get; } = [];

        public Task<MediaUploadResult> UploadImageAsync(MediaUploadRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(string publicIdOrUrl, MediaResourceType? resourceType = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string publicIdOrUrl, MediaResourceType? resourceType = null, CancellationToken cancellationToken = default)
        {
            if (Invalid.Contains(publicIdOrUrl)) throw new ArgumentException("The media URL is invalid.");
            if (Throwing.Contains(publicIdOrUrl)) throw new HttpRequestException("Cloudinary unavailable");
            Deleted.Add(publicIdOrUrl);
            return Task.FromResult(true);
        }
    }

    private static CenterMediaCleaner Create(FakeUploader uploader) =>
        new(new ServiceCollection().AddSingleton<IMediaUploader>(uploader).BuildServiceProvider());

    [Fact]
    public async Task Delete_RemovesEachDistinctUrlOnce()
    {
        var uploader = new FakeUploader();

        var failed = await Create(uploader).DeleteAsync([Logo, Cover, Logo, " ", ""]);

        Assert.Empty(failed);
        Assert.Equal([Logo, Cover], uploader.Deleted);
    }

    [Fact]
    public async Task Delete_OneFailureDoesNotStopTheRest_AndIsReturnedForManualCleanup()
    {
        var uploader = new FakeUploader();
        uploader.Throwing.Add(Logo);

        var failed = await Create(uploader).DeleteAsync([Logo, Cover]);

        Assert.Equal([Logo], failed);
        Assert.Equal([Cover], uploader.Deleted);
    }

    [Fact]
    public async Task Delete_ExternalLinkTheCenterTypedIn_IsIgnoredNotReportedAsFailure()
    {
        var uploader = new FakeUploader();
        uploader.Invalid.Add("https://[bad-link");

        var failed = await Create(uploader).DeleteAsync(["https://[bad-link", Cover]);

        Assert.Empty(failed);
        Assert.Equal([Cover], uploader.Deleted);
    }

    [Fact]
    public async Task Delete_CloudinaryNotConfigured_ReportsOnlyCloudinaryUrls()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMediaUploader>(_ => throw new OptionsValidationException("Cloudinary", typeof(CloudinaryOptions), ["CloudName is required"]))
            .BuildServiceProvider();

        var failed = await new CenterMediaCleaner(services).DeleteAsync([Logo, "https://example.com/brochure.pdf"]);

        Assert.Equal([Logo], failed);
    }

    [Fact]
    public async Task Delete_NoUrls_DoesNotResolveTheUploader()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMediaUploader>(_ => throw new InvalidOperationException("must not be resolved"))
            .BuildServiceProvider();

        Assert.Empty(await new CenterMediaCleaner(services).DeleteAsync([]));
    }
}
