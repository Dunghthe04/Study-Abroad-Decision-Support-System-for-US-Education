namespace StudyAbroad.Api.MediaUploads;

public static class ImageUploadPolicy
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 64 * 1024;
    public const int SchoolLogoMaxBytes = 2 * 1024 * 1024;
    public const int SchoolCoverMaxBytes = 10 * 1024 * 1024;
    public const int CenterLogoMaxBytes = 2 * 1024 * 1024;
    public const int CenterCoverMaxBytes = 10 * 1024 * 1024;

    public static int? GetPurposeLimit(string? purpose) => purpose switch
    {
        "school-logo" => SchoolLogoMaxBytes,
        "school-cover" => SchoolCoverMaxBytes,
        "center-logo" => CenterLogoMaxBytes,
        "center-cover" => CenterCoverMaxBytes,
        _ => null,
    };

    /// <summary>Ảnh của trung tâm tư vấn: chỉ vai trò Center được tải (ảnh của trường chỉ Admin và Moderator).</summary>
    public static bool IsCenterPurpose(string? purpose) => purpose is "center-logo" or "center-cover";
}

public sealed class UploadImageForm
{
    public IFormFile? File { get; set; }
    public string? Purpose { get; set; }
}

public sealed record MediaUploadError(string Error);
