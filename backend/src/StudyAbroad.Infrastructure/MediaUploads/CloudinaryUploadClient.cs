using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.MediaUploads;

namespace StudyAbroad.Infrastructure.MediaUploads;

public interface ICloudinaryUploadClient
{
    Task<MediaUploadResult> UploadAsync(Stream content, string safeFileName, string contentType, CancellationToken cancellationToken);
}

public sealed class CloudinaryUploadClient : ICloudinaryUploadClient
{
    private readonly Cloudinary _cloudinary;
    private readonly string _folder;

    public CloudinaryUploadClient(IOptions<CloudinaryOptions> options)
    {
        var settings = options.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _folder = settings.Folder;
    }

    public async Task<MediaUploadResult> UploadAsync(
        Stream content, string safeFileName, string contentType, CancellationToken cancellationToken)
    {
        var parameters = new ImageUploadParams
        {
            File = new FileDescription(safeFileName, content),
            Folder = _folder,
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

        if (result.Error is not null || (int)result.StatusCode is < 200 or >= 300 ||
            result.SecureUrl is null || result.SecureUrl.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(result.PublicId) || result.Bytes <= 0 || result.Width <= 0 || result.Height <= 0)
            throw new InvalidOperationException("The media provider returned an unsuccessful upload response.");

        return new MediaUploadResult(
            result.SecureUrl.AbsoluteUri, result.PublicId, contentType, result.Bytes, result.Width, result.Height);
    }
}
