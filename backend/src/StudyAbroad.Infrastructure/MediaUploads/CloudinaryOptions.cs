using Microsoft.Extensions.Options;

namespace StudyAbroad.Infrastructure.MediaUploads;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Folder { get; set; } = "usas";
    public int MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxImagePixels { get; set; } = 25_000_000;
}

public sealed class CloudinaryOptionsValidator : IValidateOptions<CloudinaryOptions>
{
    public ValidateOptionsResult Validate(string? name, CloudinaryOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.CloudName) ||
            options.CloudName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            errors.Add("Cloudinary:CloudName must contain only letters, digits, hyphens or underscores.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            errors.Add("Cloudinary:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(options.ApiSecret))
            errors.Add("Cloudinary:ApiSecret is required.");
        if (options.MaxFileSizeBytes is < 1 or > 20 * 1024 * 1024)
            errors.Add("Cloudinary:MaxFileSizeBytes must be between 1 and 20971520.");
        if (options.MaxImagePixels is < 1 or > 40_000_000)
            errors.Add("Cloudinary:MaxImagePixels must be between 1 and 40000000.");
        if (string.IsNullOrWhiteSpace(options.Folder) || options.Folder.Length > 200)
        {
            errors.Add("Cloudinary:Folder must contain between 1 and 200 characters.");
        }
        else
        {
            foreach (var segment in options.Folder.Split('/'))
            {
                if (segment.Length == 0 || segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
                {
                    errors.Add("Cloudinary:Folder must use path segments containing only letters, digits, hyphens or underscores.");
                    break;
                }
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
