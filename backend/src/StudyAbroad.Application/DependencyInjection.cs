using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudyCenterService, StudyCenterService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFinancialProfileService, FinancialProfileService>();
        services.AddScoped<IAcademicProfileService, AcademicProfileService>();
        return services;
    }
}
