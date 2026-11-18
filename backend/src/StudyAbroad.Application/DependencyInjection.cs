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
        services.AddScoped<StudyAbroad.Application.Notifications.INotificationService, StudyAbroad.Application.Notifications.NotificationService>();
        services.AddScoped<ICenterProfileChangeReviewService, CenterProfileChangeReviewService>();   // [UC-114] Moderator kiểm tra thay đổi website/fanpage
        services.AddScoped<IAdminUniversityService, AdminUniversityService>();
        services.AddScoped<StudyAbroad.Application.Admin.Scholarships.IAdminScholarshipService, StudyAbroad.Application.Admin.Scholarships.AdminScholarshipService>();
        services.AddScoped<StudyAbroad.Application.Admin.Settings.IGradeScaleSettingService, StudyAbroad.Application.Admin.Settings.GradeScaleSettingService>();
        services.AddScoped<StudyAbroad.Application.Admin.AiUsage.IAiUsageReportService, StudyAbroad.Application.Admin.AiUsage.AiUsageReportService>();   // token của các lần gọi AI cho admin
        services.AddScoped<StudyAbroad.Application.Admin.SlmSamples.ISlmSampleService, StudyAbroad.Application.Admin.SlmSamples.SlmSampleService>();
        services.AddScoped<StudyAbroad.Application.Admin.ReviewBatches.IReviewBatchService, StudyAbroad.Application.Admin.ReviewBatches.ReviewBatchService>();   // đợt duyệt mẫu
        services.AddScoped<StudyAbroad.Application.Leads.ILeadService, StudyAbroad.Application.Leads.LeadService>();
        services.AddScoped<StudyAbroad.Application.Leads.ILeadParentConfirmService, StudyAbroad.Application.Leads.LeadParentConfirmService>();   // [UC-116] phụ huynh xác nhận lead qua mail
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewService, StudyAbroad.Application.StudyCenters.CenterReviewService>();
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewModerationService, StudyAbroad.Application.StudyCenters.CenterReviewModerationService>();
        services.AddScoped<ICenterDeletionService, CenterDeletionService>();   // [UC-93] Admin xử lý yêu cầu xóa trung tâm
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<SessionRefreshService>();   // gia hạn phiên bằng refresh token (đổi token mỗi lần)
        services.AddScoped<IAccountPurgeService, AccountPurgeService>();   // [UC-80] job xóa hẳn tài khoản gọi qua scope
        // [Q11] Liên kết phụ huynh–học sinh và sự đồng ý xử lý dữ liệu trẻ em
        services.AddScoped<IParentLinkService, ParentLinkService>();
        services.AddScoped<IParentProfileShareService, ParentProfileShareService>();
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
        services.AddScoped<ParentChildrenService>();   // khu "Con của tôi": phụ huynh xem con, chỉ đọc
        services.AddScoped<ParentPreferenceService>();   // UC-76: ưu tiên của phụ huynh cho từng con
        services.AddScoped<IParentPreferenceRefresher, ParentPreferenceRefresher>();   // UC-76: con chạy gợi ý → tính lại danh sách theo cha mẹ
        services.AddScoped<ManagedChildService>();     // 1.5a: phụ huynh thêm / đổi tên / xóa con chưa có tài khoản
        services.AddScoped<ActingSessionGuard>();      // 1.5b: kiểm tra phiên làm thay con managed ở mỗi request
        services.AddScoped<ActingService>();           // 1.5b: vào / thoát làm thay
        return services;
    }
}
