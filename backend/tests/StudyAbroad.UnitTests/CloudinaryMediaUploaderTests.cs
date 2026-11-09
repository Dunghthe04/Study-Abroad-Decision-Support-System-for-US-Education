using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SkiaSharp;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Infrastructure.MediaUploads;

namespace StudyAbroad.UnitTests;

public class CloudinaryMediaUploaderTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg", "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.JPEG", "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "photo.png", "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "photo.webp", "image/webp")]
    public async Task Upload_ValidImage_ReturnsProviderResultAndDetectedType(
        SKEncodedImageFormat format, string fileName, string contentType)
    {
        var bytes = CreateImage(format);
        using var stream = new MemoryStream(bytes);
        var client = new FakeUploadClient();
        var uploader = CreateUploader(client);

        var result = await uploader.UploadImageAsync(new MediaUploadRequest(stream, fileName, "text/html"));

        Assert.Equal(1, client.Calls);
        Assert.Equal(bytes, client.Bytes);
        Assert.Equal(contentType, client.ContentType);
        Assert.Equal("https://res.cloudinary.com/test/image/upload/asset.png", result.SecureUrl);
        Assert.Equal("usas/asset", result.PublicId);
        Assert.Equal(bytes.Length, result.SizeBytes);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Upload_UnsafeFileName_UsesRandomNameWithoutPath()
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));

        await CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "../../private\\secret.png"));

        Assert.Matches("^[a-f0-9]{32}\\.png$", client.FileName!);
        Assert.DoesNotContain("secret", client.FileName!);
    }

    [Theory]
    [InlineData("photo.svg")]
    [InlineData("photo.gif")]
    [InlineData("photo.html")]
    [InlineData("photo")]
    [InlineData("photo.png.exe")]
    public async Task Upload_UnsupportedExtension_RejectsBeforeNetwork(string fileName)
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, fileName)));

        Assert.Equal(0, client.Calls);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>not an image</html>")]
    [InlineData("RIFF0000WEBPnot an image")]
    public async Task Upload_InvalidContents_RejectsBeforeNetwork(string content)
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png", "image/png")));

        Assert.Equal(0, client.Calls);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Upload_OnlyPngSignature_RejectsBeforeNetwork()
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png")));

        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Upload_TruncatedPng_RejectsBeforeNetwork()
    {
        var client = new FakeUploadClient();
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        using var stream = new MemoryStream(bytes[..(bytes.Length / 2)]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png")));

        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Upload_ContentDoesNotMatchExtension_RejectsBeforeNetwork()
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Jpeg));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png", "image/png")));

        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Upload_OverSizeLimit_ReadsOnlyLimitPlusOneAndRejects()
    {
        var client = new FakeUploadClient();
        var options = ValidOptions();
        options.MaxFileSizeBytes = 20;
        using var stream = new MemoryStream(new byte[1000]);

        await Assert.ThrowsAsync<MediaUploadSizeLimitException>(() =>
            CreateUploader(client, options).UploadImageAsync(new MediaUploadRequest(stream, "photo.png")));

        Assert.Equal(21, stream.Position);
        Assert.Equal(0, client.Calls);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(100, 20)]
    [InlineData(20, 100)]
    public async Task Upload_PerRequestLimit_CannotExceedGlobalLimit(int globalLimit, int requestLimit)
    {
        var client = new FakeUploadClient();
        var options = ValidOptions();
        options.MaxFileSizeBytes = globalLimit;
        using var stream = new MemoryStream(new byte[1000]);

        await Assert.ThrowsAsync<MediaUploadSizeLimitException>(() =>
            CreateUploader(client, options).UploadImageAsync(new MediaUploadRequest(stream, "photo.png", MaxSizeBytes: requestLimit)));

        Assert.Equal(Math.Min(globalLimit, requestLimit) + 1, stream.Position);
        Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Upload_InvalidPerRequestLimit_RejectsBeforeReading(int limit)
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png", MaxSizeBytes: limit)));

        Assert.Equal(0, stream.Position);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Upload_ExactlySizeLimit_Succeeds()
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        var options = ValidOptions();
        options.MaxFileSizeBytes = bytes.Length;
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(bytes);

        await CreateUploader(client, options).UploadImageAsync(new MediaUploadRequest(stream, "photo.png"));

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Upload_OverPixelLimit_RejectsBeforeNetwork()
    {
        var options = ValidOptions();
        options.MaxImagePixels = 3;
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateUploader(client, options).UploadImageAsync(new MediaUploadRequest(stream, "photo.png")));

        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Upload_NonSeekableStream_SucceedsWithoutDisposingCallerStream()
    {
        var client = new FakeUploadClient();
        using var stream = new NonSeekableStream(CreateImage(SKEncodedImageFormat.Png));

        await CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png"));

        Assert.Equal(1, client.Calls);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Upload_Cancelled_LeavesStreamOpenAndDoesNotCallNetwork()
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateUploader(client)
            .UploadImageAsync(new MediaUploadRequest(stream, "photo.png"), cancellation.Token));

        Assert.Equal(0, client.Calls);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Upload_PassesCancellationTokenToProvider()
    {
        var client = new FakeUploadClient();
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));
        using var cancellation = new CancellationTokenSource();

        await CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png"), cancellation.Token);

        Assert.Equal(cancellation.Token, client.CancellationToken);
    }

    [Fact]
    public async Task Upload_ProviderFails_DoesNotDisposeCallerStream()
    {
        var client = new FakeUploadClient { Fail = true };
        using var stream = new MemoryStream(CreateImage(SKEncodedImageFormat.Png));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateUploader(client).UploadImageAsync(new MediaUploadRequest(stream, "photo.png")));

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Registration_WithoutConfiguration_IsLazyButRejectsUploaderResolution()
    {
        var services = new ServiceCollection();
        services.AddCloudinaryMedia(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IMediaUploader>());

        Assert.Contains("Cloudinary:ApiSecret is required.", exception.Failures);
    }

    [Fact]
    public void Registration_BindsOptionsAndResolvesUploaderWithoutNetwork()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "test-cloud",
            ["Cloudinary:ApiKey"] = "test-key",
            ["Cloudinary:ApiSecret"] = "test-secret",
            ["Cloudinary:Folder"] = "usas/test",
            ["Cloudinary:MaxFileSizeBytes"] = "5000",
            ["Cloudinary:MaxImagePixels"] = "1000",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddCloudinaryMedia(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<CloudinaryMediaUploader>(provider.GetRequiredService<IMediaUploader>());
        var options = provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value;
        Assert.Equal("usas/test", options.Folder);
        Assert.Equal(5000, options.MaxFileSizeBytes);
        Assert.Equal(1000, options.MaxImagePixels);
    }

    [Theory]
    [InlineData("CloudName", "")]
    [InlineData("CloudName", "https://invalid/path")]
    [InlineData("ApiKey", " ")]
    [InlineData("ApiSecret", "")]
    [InlineData("Folder", "../unsafe")]
    [InlineData("Folder", "/leading")]
    [InlineData("Folder", "trailing/")]
    [InlineData("Folder", "double//slash")]
    [InlineData("Folder", "")]
    public void Options_InvalidStrings_RejectWithoutExposingSecret(string property, string value)
    {
        var options = ValidOptions();
        typeof(CloudinaryOptions).GetProperty(property)!.SetValue(options, value);

        var result = new CloudinaryOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        if (!string.IsNullOrEmpty(options.ApiSecret))
            Assert.DoesNotContain(options.ApiSecret, string.Join(" ", result.Failures!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20 * 1024 * 1024 + 1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 40_000_001)]
    public void Options_InvalidLimits_Reject(int bytes, int pixels)
    {
        var options = ValidOptions();
        options.MaxFileSizeBytes = bytes;
        options.MaxImagePixels = pixels;

        Assert.True(new CloudinaryOptionsValidator().Validate(null, options).Failed);
    }

    private static CloudinaryMediaUploader CreateUploader(FakeUploadClient client, CloudinaryOptions? options = null) =>
        new(client, Options.Create(options ?? ValidOptions()));

    private static CloudinaryOptions ValidOptions() => new()
    {
        CloudName = "test-cloud",
        ApiKey = "test-key",
        ApiSecret = "test-secret",
    };

    private static byte[] CreateImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private sealed class FakeUploadClient : ICloudinaryUploadClient
    {
        public int Calls { get; private set; }
        public byte[]? Bytes { get; private set; }
        public string? FileName { get; private set; }
        public string? ContentType { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public bool Fail { get; init; }

        public async Task<MediaUploadResult> UploadAsync(
            Stream content, string safeFileName, string contentType, CancellationToken cancellationToken)
        {
            Calls++;
            CancellationToken = cancellationToken;
            FileName = safeFileName;
            ContentType = contentType;
            if (Fail)
                throw new InvalidOperationException("Upload failed.");
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Bytes = copy.ToArray();
            return new MediaUploadResult("https://res.cloudinary.com/test/image/upload/asset.png", "usas/asset", contentType, Bytes.Length, 2, 2);
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
