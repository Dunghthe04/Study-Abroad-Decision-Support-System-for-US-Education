namespace StudyAbroad.Api.MediaUploads;

public static class ImageUploadPolicy
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 64 * 1024;
    public const int SchoolLogoMaxBytes = 2 * 1024 * 1024;
    public const int SchoolCoverMaxBytes = 10 * 1024 * 1024;

    public static int? GetPurposeLimit(string? purpose) => purpose switch
    {
        "school-logo" => SchoolLogoMaxBytes,
        "school-cover" => SchoolCoverMaxBytes,
        _ => null,
    };
}

public sealed class UploadImageForm
{
    public IFormFile? File { get; set; }
    public string? Purpose { get; set; }
}

public sealed record MediaUploadError(string Error);
