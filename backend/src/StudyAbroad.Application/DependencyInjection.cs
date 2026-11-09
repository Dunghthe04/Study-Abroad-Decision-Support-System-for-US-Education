using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<StudyAbroad.Application.Universities.IUniversityDetailService, StudyAbroad.Application.Universities.UniversityDetailService>();
        services.AddScoped<StudyAbroad.Application.TargetSchools.ITargetSchoolService, StudyAbroad.Application.TargetSchools.TargetSchoolService>();
        services.AddScoped<IStudyCenterService, StudyCenterService>();
        services.AddScoped<IAdminUniversityService, AdminUniversityService>();
        services.AddScoped<StudyAbroad.Application.Leads.ILeadService, StudyAbroad.Application.Leads.LeadService>();
        services.AddScoped<IAuthService, AuthService>();
        // [Q11] Liên kết phụ huynh–học sinh và sự đồng ý xử lý dữ liệu trẻ em
        services.AddScoped<IParentLinkService, ParentLinkService>();
        services.AddScoped<IFinancialProfileService, FinancialProfileService>();
        // [USAS-365] Đăng ký service phân tích điểm học thuật
        services.AddScoped<StudyAbroad.Application.Profile.Academic.IAcademicAnalysisService, StudyAbroad.Application.Profile.Academic.AcademicAnalysisService>();
        services.AddScoped<StudyAbroad.Application.Profile.Academic.IGradeScaleProvider, StudyAbroad.Application.Profile.Academic.GradeScaleProvider>();
        services.AddScoped<IAcademicProfileService, AcademicProfileService>();
        // [USAS-640] Đăng ký service metadata động cho biểu mẫu hồ sơ
        services.AddScoped<StudyAbroad.Application.Profile.Flow.IProfileMetadataService, StudyAbroad.Application.Profile.Flow.ProfileMetadataService>();
        // [USAS-640] Đăng ký service luồng hồ sơ một cửa
        services.AddScoped<StudyAbroad.Application.Profile.Flow.IProfileFlowService, StudyAbroad.Application.Profile.Flow.ProfileFlowService>();
        services.AddScoped<RecommendationService>();
        services.AddScoped<ScenarioService>();   // [UC-86] lưu và mở lại kịch bản what-if
        // Chat chạy gợi ý trường qua interface, dùng chung RecommendationService đã đăng ký ở trên
        services.AddScoped<IRecommendationRunner>(sp => sp.GetRequiredService<RecommendationService>());
        services.AddScoped<StudyAbroad.Application.Advisor.ChatService>();
        services.AddScoped<RecommendationHistoryService>();   // [UC-87] xem lịch sử gợi ý
        return services;
    }
}
