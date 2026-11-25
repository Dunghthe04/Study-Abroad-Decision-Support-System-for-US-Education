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
        services.AddScoped<ICenterAccountService, CenterAccountService>();   // [UC-64 / UC-91] đăng ký, xác thực email, đổi liên lạc của trung tâm
        services.AddScoped<StudyAbroad.Application.Notifications.INotificationService, StudyAbroad.Application.Notifications.NotificationService>();
        services.AddScoped<ICenterProfileChangeReviewService, CenterProfileChangeReviewService>();   // [UC-114] Moderator kiểm tra thay đổi website/fanpage
        services.AddScoped<IAdminUniversityService, AdminUniversityService>();
        services.AddScoped<StudyAbroad.Application.Admin.Scholarships.IAdminScholarshipService, StudyAbroad.Application.Admin.Scholarships.AdminScholarshipService>();
        services.AddScoped<StudyAbroad.Application.Scholarships.IScholarshipSearchService, StudyAbroad.Application.Scholarships.ScholarshipSearchService>();
        services.AddScoped<StudyAbroad.Application.Costs.ICostService, StudyAbroad.Application.Costs.CostService>();
        services.AddScoped<StudyAbroad.Application.Costs.ICostParameterCatalog, StudyAbroad.Application.Costs.CostParameterCatalog>();
        services.AddScoped<StudyAbroad.Application.Admin.Settings.IGradeScaleSettingService, StudyAbroad.Application.Admin.Settings.GradeScaleSettingService>();
        services.AddScoped<StudyAbroad.Application.Admin.Settings.ICostParametersSettingService, StudyAbroad.Application.Admin.Settings.CostParametersSettingService>();
        services.AddScoped<StudyAbroad.Application.Admin.AiUsage.IAiUsageReportService, StudyAbroad.Application.Admin.AiUsage.AiUsageReportService>();   // token của các lần gọi AI cho admin
        services.AddScoped<StudyAbroad.Application.Admin.AiQuality.IAiQualityService, StudyAbroad.Application.Admin.AiQuality.AiQualityService>();   // báo cáo chất lượng AI cho admin (UC-90)
        services.AddScoped<StudyAbroad.Application.Admin.AiQuality.IAiQualitySampleService, StudyAbroad.Application.Admin.AiQuality.AiQualitySampleService>();   // mẫu hội thoại đã che cho admin (UC-90)
        services.AddScoped<StudyAbroad.Application.Advisor.History.IChatHistoryService, StudyAbroad.Application.Advisor.History.ChatHistoryService>();   // lịch sử chat của chính người dùng
        services.AddScoped<StudyAbroad.Application.Admin.SlmSamples.ISlmSampleService, StudyAbroad.Application.Admin.SlmSamples.SlmSampleService>();
        services.AddScoped<StudyAbroad.Application.Admin.KnowledgeBase.IKnowledgeBaseService, StudyAbroad.Application.Admin.KnowledgeBase.KnowledgeBaseService>();   // kho tri thức (UC-112..117)
        services.AddScoped<StudyAbroad.Application.Admin.ReviewBatches.IReviewBatchService, StudyAbroad.Application.Admin.ReviewBatches.ReviewBatchService>();   // đợt duyệt mẫu
        services.AddScoped<StudyAbroad.Application.Admin.Sensitivity.ISensitivityRunService, StudyAbroad.Application.Admin.Sensitivity.SensitivityRunService>();   // phân tích độ nhạy tham số (UC-85)
        services.AddScoped<StudyAbroad.Application.Admin.Policies.IPolicyService, StudyAbroad.Application.Admin.Policies.PolicyService>();   // quản lý chính sách và điều khoản (UC-103)
        services.AddScoped<StudyAbroad.Application.Policies.IPolicyAcceptanceService, StudyAbroad.Application.Policies.PolicyAcceptanceService>();   // xác nhận chính sách và điều khoản (UC-104)
        services.AddScoped<StudyAbroad.Application.Policies.IRegistrationConsent, StudyAbroad.Application.Policies.RegistrationConsent>();   // đồng ý điều khoản lúc đăng ký (UC-104)
        services.AddScoped<StudyAbroad.Application.Leads.ILeadService, StudyAbroad.Application.Leads.LeadService>();
        services.AddScoped<StudyAbroad.Application.Community.ICommunityService, StudyAbroad.Application.Community.CommunityService>();
        services.AddScoped<StudyAbroad.Application.Leads.ILeadParentConfirmService, StudyAbroad.Application.Leads.LeadParentConfirmService>();   // [UC-116] phụ huynh xác nhận lead qua mail
        services.AddScoped<StudyAbroad.Application.Leads.ILeadParentHoldService, StudyAbroad.Application.Leads.LeadParentHoldService>();   // [UC-110] giữ yêu cầu liên hệ khi sửa ngày sinh về dưới 16
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewService, StudyAbroad.Application.StudyCenters.CenterReviewService>();
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewModerationService, StudyAbroad.Application.StudyCenters.CenterReviewModerationService>();
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewReportService, StudyAbroad.Application.StudyCenters.CenterReviewReportService>();   // [UC-151] báo cáo đánh giá trung tâm
        services.AddScoped<StudyAbroad.Application.StudyCenters.ICenterReviewReplyService, StudyAbroad.Application.StudyCenters.CenterReviewReplyService>();   // [UC-152] trung tâm phản hồi đánh giá
        services.AddScoped<ICenterDeletionService, CenterDeletionService>();   // [UC-93] Admin xử lý yêu cầu xóa trung tâm
        services.AddScoped<IAuthService, AuthService>();
        // [UC-75 1a] Cùng một AuthService trong scope, để dịch vụ liên kết gọi xóa tài khoản của con
        services.AddScoped<IParentRequestedDeletion>(sp => (AuthService)sp.GetRequiredService<IAuthService>());
        services.AddScoped<SessionRefreshService>();   // gia hạn phiên bằng refresh token (đổi token mỗi lần)
        services.AddScoped<IAccountPurgeService, AccountPurgeService>();   // [UC-80] job xóa hẳn tài khoản gọi qua scope
        // [Q11] Liên kết phụ huynh–học sinh và sự đồng ý xử lý dữ liệu trẻ em
        services.AddScoped<IParentLinkService, ParentLinkService>();
        services.AddScoped<IChildAccountDeletionService, ChildAccountDeletionService>();   // [UC-75 1a/4b] người đại diện xóa / khôi phục tài khoản con dưới 16
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
        // Dựng lại câu hồ sơ đang chờ khi mở phiên cũ: cùng một ChatService trong phạm vi yêu cầu
        services.AddScoped<StudyAbroad.Application.Advisor.History.IChatPendingQuestion>(sp => sp.GetRequiredService<StudyAbroad.Application.Advisor.ChatService>());
        services.AddScoped<RecommendationHistoryService>();   // [UC-87] xem lịch sử gợi ý
        services.AddScoped<ParentChildrenService>();   // khu "Con của tôi": phụ huynh xem con, chỉ đọc
        services.AddScoped<ParentPreferenceService>();   // UC-76: ưu tiên của phụ huynh cho từng con
        services.AddScoped<IParentPreferenceRefresher, ParentPreferenceRefresher>();   // UC-76: con chạy gợi ý → tính lại danh sách theo cha mẹ
        services.AddScoped<ManagedChildService>();     // 1.5a: phụ huynh thêm / đổi tên / xóa con chưa có tài khoản
        services.AddScoped<ActingSessionGuard>();      // 1.5b: kiểm tra phiên làm thay con managed ở mỗi request
        services.AddScoped<ActingService>();           // 1.5b: vào / thoát làm thay
        services.AddScoped<StudyAbroad.Application.Dashboard.IDashboardService, StudyAbroad.Application.Dashboard.DashboardService>(); // [UC-94] Dashboard KPI
        services.AddScoped<StudyAbroad.Application.Audit.IAccessLogService, StudyAbroad.Application.Audit.AccessLogService>();   // [UC-91] nhật ký truy cập dữ liệu nhạy cảm
        services.AddScoped<StudyAbroad.Application.Billing.IEntitlementService, StudyAbroad.Application.Billing.EntitlementService>();   // gói dịch vụ: tier, pool trả cho lượt chat
        services.AddScoped<StudyAbroad.Application.Billing.Admin.IPlanAdminService, StudyAbroad.Application.Billing.Admin.PlanAdminService>();   // UC-79: admin quản lý gói
        services.AddScoped<StudyAbroad.Application.Billing.Family.IFamilyService, StudyAbroad.Application.Billing.Family.FamilyService>();   // gói Gia đình: lời mời, thành viên, rời gói (UC-126..131)
        services.AddScoped<StudyAbroad.Application.Billing.Orders.IOrderService, StudyAbroad.Application.Billing.Orders.OrderService>();   // đơn hàng, thanh toán payOS
        services.AddScoped<StudyAbroad.Application.Billing.IBillingAccountHooks, StudyAbroad.Application.Billing.BillingAccountHooks>();   // UC-80: xóa tài khoản kết thúc nhóm, đơn chờ
        services.AddScoped<StudyAbroad.Application.Billing.Jobs.OrderReconciler>();   // áp dụng trạng thái payOS vào đơn (job đối soát, admin kiểm lại)
        services.AddScoped<StudyAbroad.Application.Billing.Jobs.IBillingJobService, StudyAbroad.Application.Billing.Jobs.BillingJobService>();   // các bước của job nền thanh toán
        services.AddScoped<StudyAbroad.Application.Billing.Admin.IAdminOrderService, StudyAbroad.Application.Billing.Admin.AdminOrderService>();   // UC-132..134: admin xử lý đơn
        services.AddScoped<StudyAbroad.Application.BirthDateChanges.IBirthDateChangeService, StudyAbroad.Application.BirthDateChanges.BirthDateChangeService>();   // [UC-107] gửi, hủy yêu cầu sửa ngày sinh
        services.AddScoped<StudyAbroad.Application.BirthDateChanges.IAdminBirthDateChangeService, StudyAbroad.Application.BirthDateChanges.AdminBirthDateChangeService>();   // [UC-150] Admin duyệt, từ chối yêu cầu sửa ngày sinh
        return services;
    }
}
