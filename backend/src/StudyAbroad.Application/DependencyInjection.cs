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
        // [USAS-365] Đăng ký service phân tích điểm học thuật
        services.AddScoped<StudyAbroad.Application.Profile.Academic.IAcademicAnalysisService, StudyAbroad.Application.Profile.Academic.AcademicAnalysisService>();
        services.AddScoped<IAcademicProfileService, AcademicProfileService>();
        // [USAS-640] Đăng ký service metadata động cho biểu mẫu hồ sơ
        services.AddScoped<StudyAbroad.Application.Profile.Flow.IProfileMetadataService, StudyAbroad.Application.Profile.Flow.ProfileMetadataService>();
        // [USAS-640] Đăng ký service luồng hồ sơ một cửa
        services.AddScoped<StudyAbroad.Application.Profile.Flow.IProfileFlowService, StudyAbroad.Application.Profile.Flow.ProfileFlowService>();
        return services;
    }
}
