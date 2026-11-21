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
        return await _client.UploadAsync(
            buffered, safeFileName, contentType, request.FolderSuffix, request.ModerateContent, cancellationToken);
    }

    public Task<bool> ExistsAsync(
        string publicIdOrUrl, MediaResourceType? resourceType = null, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAsset(publicIdOrUrl, resourceType, cancellationToken, out var asset))
            return Task.FromResult(false);
        return _client.ExistsAsync(asset.PublicId, asset.ResourceType, asset.DeliveryType, cancellationToken);
    }

    public Task<bool> DeleteAsync(
        string publicIdOrUrl, MediaResourceType? resourceType = null, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAsset(publicIdOrUrl, resourceType, cancellationToken, out var asset))
            return Task.FromResult(false);
        return _client.DeleteAsync(asset.PublicId, asset.ResourceType, asset.DeliveryType, cancellationToken);
    }

    private bool TryResolveAsset(
        string publicIdOrUrl,
        MediaResourceType? resourceType,
        CancellationToken cancellationToken,
        out ResolvedAsset asset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicIdOrUrl);
        if (resourceType is { } rt && !Enum.IsDefined(rt))
            throw new ArgumentOutOfRangeException(nameof(resourceType), rt, "Unsupported media resource type.");
        cancellationToken.ThrowIfCancellationRequested();

        var trimmed = publicIdOrUrl.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal) || Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                throw new ArgumentException("The media URL is invalid.", nameof(publicIdOrUrl));
            return TryParseCloudinaryUrl(trimmed, uri, resourceType, out asset);
        }

        ValidateRawPublicId(trimmed, nameof(publicIdOrUrl));
        asset = new ResolvedAsset(trimmed, resourceType ?? MediaResourceType.Image, "upload");
        return true;
    }

    private static void ValidateRawPublicId(string publicId, string paramName)
    {
        if (publicId.StartsWith('/') || publicId.EndsWith('/') ||
            publicId.Contains('\\') || publicId.Contains('?') || publicId.Contains('#') ||
            publicId.Any(char.IsControl))
            throw new ArgumentException("The Cloudinary public ID contains invalid characters.", paramName);

        foreach (var segment in publicId.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new ArgumentException("The Cloudinary public ID contains an invalid path segment.", paramName);
        }
    }

    private bool TryParseCloudinaryUrl(
        string rawUrl, Uri uri, MediaResourceType? requestedType, out ResolvedAsset asset)
    {
        asset = default;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;
        if (!string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return false;
        if (HasRawDotSegment(rawUrl))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4)
            return false;
        if (!string.Equals(segments[0], _options.CloudName, StringComparison.OrdinalIgnoreCase))
            return false;

        MediaResourceType parsedType;
        switch (segments[1].ToLowerInvariant())
        {
            case "image":
                parsedType = MediaResourceType.Image;
                break;
            case "video":
                parsedType = MediaResourceType.Video;
                break;
            case "raw":
                parsedType = MediaResourceType.Raw;
                break;
            default:
                return false;
        }

        if (requestedType is { } expected && expected != parsedType)
            return false;

        var deliveryType = segments[2].ToLowerInvariant();
        if (deliveryType is not ("upload" or "private" or "authenticated"))
            return false;

        var index = 3;
        if (index < segments.Length &&
            segments[index].StartsWith("s--", StringComparison.Ordinal) &&
            segments[index].EndsWith("--", StringComparison.Ordinal))
            index++;

        if (index >= segments.Length)
            return false;

        if (IsVersionSegment(segments[index]))
        {
            index++;
        }
        else
        {
            var versionIndex = -1;
            for (var i = index + 1; i < segments.Length - 1; i++)
            {
                if (IsVersionSegment(segments[i]) && LooksLikeTransformChain(segments, index, i))
                {
                    versionIndex = i;
                    break;
                }
            }

            if (versionIndex >= 0)
            {
                index = versionIndex + 1;
            }
            else
            {
                // Không có version segment: chỉ bỏ qua phần tử chứa dấu ',' hoặc ':' (tức chuỗi transform),
                // giữ nguyên tên thư mục hợp lệ như "lead_chat".
                while (index < segments.Length - 1 && (segments[index].Contains(',') || segments[index].Contains(':')))
                    index++;
            }
        }

        if (index >= segments.Length)
            return false;

        var decoded = new string[segments.Length - index];
        for (var i = 0; i < decoded.Length; i++)
        {
            var part = Uri.UnescapeDataString(segments[index + i]);
            if (part.Length == 0 || part is "." or ".." || part.Contains('\\') || part.Any(char.IsControl))
                return false;
            decoded[i] = part;
        }

        if (parsedType is MediaResourceType.Image or MediaResourceType.Video)
        {
            var last = decoded[^1];
            var dot = last.LastIndexOf('.');
            if (dot == 0)
                return false;
            if (dot > 0)
                decoded[^1] = last[..dot];
        }

        asset = new ResolvedAsset(string.Join('/', decoded), parsedType, deliveryType);
        return true;
    }

    private static bool HasRawDotSegment(string rawUrl)
    {
        var schemeSep = rawUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeSep < 0)
            return false;
        var pathStart = rawUrl.IndexOf('/', schemeSep + 3);
        if (pathStart < 0)
            return false;
        var pathEnd = rawUrl.IndexOfAny(['?', '#'], pathStart);
        var rawPath = pathEnd < 0 ? rawUrl[pathStart..] : rawUrl[pathStart..pathEnd];
        foreach (var segment in rawPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            var decoded = Uri.UnescapeDataString(segment);
            if (decoded is "." or ".." ||
                decoded.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part is "." or ".."))
                return true;
        }
        return false;
    }

    private static bool IsVersionSegment(string segment) =>
        segment.Length >= 2 && segment[0] == 'v' && segment.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

    private static bool LooksLikeTransformChain(string[] segments, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!segments[i].Contains(',') && !segments[i].Contains(':') && !segments[i].Contains('_'))
                return false;
        }
        return true;
    }

    private readonly record struct ResolvedAsset(string PublicId, MediaResourceType ResourceType, string DeliveryType);

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
