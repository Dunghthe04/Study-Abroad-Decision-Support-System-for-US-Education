using Microsoft.Extensions.Options;
using SkiaSharp;
using StudyAbroad.Application.MediaUploads;

namespace StudyAbroad.Infrastructure.MediaUploads;

public sealed class CloudinaryMediaUploader : IMediaUploader
{
    private readonly ICloudinaryUploadClient _client;
    private readonly CloudinaryOptions _options;

    public CloudinaryMediaUploader(ICloudinaryUploadClient client, IOptions<CloudinaryOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<MediaUploadResult> UploadImageAsync(
        MediaUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (!request.Content.CanRead)
            throw new ArgumentException("The image stream must be readable.", nameof(request));
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = Path.GetFileName(request.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new ArgumentException("Only JPEG, PNG and WebP images are supported.", nameof(request)),
        };

        if (request.MaxSizeBytes is <= 0)
            throw new ArgumentException("The per-upload size limit must be positive.", nameof(request));
        var maxSizeBytes = Math.Min(_options.MaxFileSizeBytes, request.MaxSizeBytes ?? _options.MaxFileSizeBytes);
        using var buffered = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var remaining = maxSizeBytes - (int)buffered.Length;
            var readSize = Math.Min(buffer.Length, remaining + 1);
            var bytesRead = await request.Content.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);
            if (bytesRead == 0)
                break;
            if (bytesRead > remaining)
                throw new MediaUploadSizeLimitException();
            await buffered.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
        if (buffered.Length == 0)
            throw new ArgumentException("The image must not be empty.", nameof(request));

        var contentType = ValidateImage(buffered, expectedContentType, cancellationToken);
        buffered.Position = 0;
        var safeFileName = Guid.NewGuid().ToString("N") + extension;
        cancellationToken.ThrowIfCancellationRequested();
        return await _client.UploadAsync(buffered, safeFileName, contentType, cancellationToken);
    }

    private string ValidateImage(
        MemoryStream content, string expectedContentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var data = SKData.CreateCopy(content.GetBuffer().AsSpan(0, (int)content.Length));
        using var codec = SKCodec.Create(data);
        if (codec is null)
            throw new ArgumentException("The file does not contain a supported image.");

        var contentType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => throw new ArgumentException("Only JPEG, PNG and WebP images are supported."),
        };
        if (contentType != expectedContentType)
            throw new ArgumentException("The image contents do not match the supported file extension.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > _options.MaxImagePixels)
            throw new ArgumentException("The image exceeds the configured pixel limit.");
        if (codec.FrameCount > 1)
            throw new ArgumentException("Animated images are not supported.");

        var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(imageInfo);
        var result = codec.GetPixels(imageInfo, bitmap.GetPixels());
        cancellationToken.ThrowIfCancellationRequested();
        if (result != SKCodecResult.Success)
            throw new ArgumentException("The image contents are invalid or incomplete.");
        return contentType;
    }
}
