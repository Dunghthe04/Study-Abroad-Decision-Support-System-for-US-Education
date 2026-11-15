namespace StudyAbroad.Application.MediaUploads;

public interface IMediaUploader
{
    Task<MediaUploadResult> UploadImageAsync(MediaUploadRequest request, CancellationToken cancellationToken = default);
}

public sealed record MediaUploadRequest(
    Stream Content,
    string FileName,
    string? ContentType = null,
    int? MaxSizeBytes = null,
    string? FolderSuffix = null,
    bool ModerateContent = false);

public sealed class MediaUploadSizeLimitException : ArgumentException
{
    public MediaUploadSizeLimitException() : base("The image exceeds the allowed file size limit.")
    {
    }
}

/// <summary>Ảnh bị hệ thống kiểm duyệt nội dung từ chối (ảnh nhạy cảm, khiêu dâm, bạo lực...).</summary>
public sealed class MediaContentRejectedException : ArgumentException
{
    public MediaContentRejectedException() : base("Ảnh có nội dung nhạy cảm hoặc không phù hợp nên không thể tải lên.")
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
