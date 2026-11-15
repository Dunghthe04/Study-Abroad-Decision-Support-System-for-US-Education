namespace StudyAbroad.Application.MediaUploads;

public interface IMediaUploader
{
    Task<MediaUploadResult> UploadImageAsync(MediaUploadRequest request, CancellationToken cancellationToken = default);
}

public sealed record MediaUploadRequest(
    Stream Content,
    string FileName,
    string? ContentType = null,
    int? MaxSizeBytes = null);

public sealed class MediaUploadSizeLimitException : ArgumentException
{
    public MediaUploadSizeLimitException() : base("The image exceeds the allowed file size limit.")
    {
    }
}

public sealed record MediaUploadResult(
    string SecureUrl,
    string PublicId,
    string ContentType,
    long SizeBytes,
    int Width,
    int Height);
