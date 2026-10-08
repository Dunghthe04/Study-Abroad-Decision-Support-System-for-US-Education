using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudyCenterService, StudyCenterService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRecommendationService,RecommendationService>();
        services.AddScoped<IAcademicProfileService, AcademicProfileService>();
        return services;
    }
}
