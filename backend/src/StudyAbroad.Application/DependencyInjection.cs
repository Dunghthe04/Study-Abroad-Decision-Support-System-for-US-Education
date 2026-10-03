using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudyCenterService, StudyCenterService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
