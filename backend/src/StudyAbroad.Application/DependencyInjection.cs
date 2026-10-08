using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudyCenterService, StudyCenterService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFinancialProfileService, FinancialProfileService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<ExtracurricularScoring>();
        // [USAS-365] Đăng ký service phân tích điểm học thuật
        services.AddScoped<StudyAbroad.Application.Profile.Academic.IAcademicAnalysisService, StudyAbroad.Application.Profile.Academic.AcademicAnalysisService>();
        services.AddScoped<IAcademicProfileService, AcademicProfileService>();
        return services;
    }
}
