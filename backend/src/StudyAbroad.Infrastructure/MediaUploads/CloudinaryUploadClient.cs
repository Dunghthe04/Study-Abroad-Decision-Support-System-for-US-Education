using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.MediaUploads;

namespace StudyAbroad.Infrastructure.MediaUploads;

public interface ICloudinaryUploadClient
{
    Task<MediaUploadResult> UploadAsync(
        Stream content,
        string safeFileName,
        string contentType,
        string? folderSuffix,
        bool moderateContent,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        string publicId,
        MediaResourceType resourceType,
        string deliveryType,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        string publicId,
        MediaResourceType resourceType,
        string deliveryType,
        CancellationToken cancellationToken);
}

public sealed class CloudinaryUploadClient : ICloudinaryUploadClient
{
    private readonly Cloudinary _cloudinary;
    private readonly string _folder;
    private readonly string _chatModeration;

    public CloudinaryUploadClient(IOptions<CloudinaryOptions> options)
    {
        var settings = options.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _folder = settings.Folder;
        _chatModeration = settings.ChatModeration;
    }

    public async Task<MediaUploadResult> UploadAsync(
        Stream content,
        string safeFileName,
        string contentType,
        string? folderSuffix,
        bool moderateContent,
        CancellationToken cancellationToken)
    {
        var folder = string.IsNullOrWhiteSpace(folderSuffix) ? _folder : $"{_folder}/{folderSuffix.Trim('/')}";
        var moderation = moderateContent && !string.IsNullOrWhiteSpace(_chatModeration) ? _chatModeration : null;
        var parameters = new ImageUploadParams
        {
            File = new FileDescription(safeFileName, content),
            Folder = folder,
            Moderation = moderation,
            PublicId = Guid.NewGuid().ToString("N"),
            UseFilename = false,
            Overwrite = false,
        };

        ImageUploadResult result;
        try
        {
            result = await _cloudinary.UploadAsync(parameters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The media provider could not complete the upload.");
        }

        if (moderation is not null && result.Moderation?.Any(m => m.Status == ModerationStatus.Rejected) == true)
        {
            try { await _cloudinary.DestroyAsync(new DeletionParams(result.PublicId)); } catch (Exception) { /* ảnh bị từ chối, dọn dẹp best-effort */ }
            throw new MediaContentRejectedException();
        }

        if (result.Error is not null || (int)result.StatusCode is < 200 or >= 300 ||
            result.SecureUrl is null || result.SecureUrl.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(result.PublicId) || result.Bytes <= 0 || result.Width <= 0 || result.Height <= 0)
            throw new InvalidOperationException("The media provider returned an unsuccessful upload response.");

        return new MediaUploadResult(
            result.SecureUrl.AbsoluteUri, result.PublicId, contentType, result.Bytes, result.Width, result.Height);
    }

    public async Task<bool> ExistsAsync(
        string publicId,
        MediaResourceType resourceType,
        string deliveryType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new GetResourceParams(publicId)
        {
            ResourceType = ToResourceType(resourceType),
            Type = deliveryType,
        };

        GetResourceResult result;
        try
        {
            result = await _cloudinary.GetResourceAsync(parameters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The media provider could not verify the media asset.");
        }

        return InterpretExistsResult(result);
    }

    public async Task<bool> DeleteAsync(
        string publicId,
        MediaResourceType resourceType,
        string deliveryType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new DeletionParams(publicId)
        {
            ResourceType = ToResourceType(resourceType),
            Type = deliveryType,
            Invalidate = true,
        };

        DeletionResult result;
        try
        {
            // CloudinaryDotNet 1.29.3 DestroyAsync không nhận CancellationToken.
            result = await _cloudinary.DestroyAsync(parameters);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The media provider could not delete the media asset.");
        }

        return InterpretDeleteResult(result);
    }

    public static bool InterpretExistsResult(GetResourceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (result.StatusCode == HttpStatusCode.OK && result.Error is null && !string.IsNullOrWhiteSpace(result.PublicId))
            return true;
        throw new InvalidOperationException("The media provider returned an unsuccessful resource lookup response.");
    }

    public static bool InterpretDeleteResult(DeletionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (result.StatusCode == HttpStatusCode.OK && result.Error is null)
        {
            if (string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(result.Result, "not found", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        throw new InvalidOperationException("The media provider returned an unsuccessful deletion response.");
    }

    private static ResourceType ToResourceType(MediaResourceType resourceType) => resourceType switch
    {
        MediaResourceType.Image => ResourceType.Image,
        MediaResourceType.Video => ResourceType.Video,
        MediaResourceType.Raw => ResourceType.Raw,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported media resource type."),
    };
}
