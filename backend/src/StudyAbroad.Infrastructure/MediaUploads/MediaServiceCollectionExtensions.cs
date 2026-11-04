using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.MediaUploads;

namespace StudyAbroad.Infrastructure.MediaUploads;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddCloudinaryMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddSingleton<IValidateOptions<CloudinaryOptions>, CloudinaryOptionsValidator>();
        services.AddSingleton<ICloudinaryUploadClient, CloudinaryUploadClient>();
        services.AddSingleton<IMediaUploader, CloudinaryMediaUploader>();
        return services;
    }
}
