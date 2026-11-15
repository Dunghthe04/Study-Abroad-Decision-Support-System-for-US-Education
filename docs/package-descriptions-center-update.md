# Package descriptions: rows to replace for the study-center changes (copy from here)

Apply these on top of `package-descriptions-overview-fe-be-v2.docx` (the same rows exist in `package-descriptions-corrected.docx`). Every class name below was checked against the repository. Package names, counts and the diagram image do not change, so no package is added or removed.

## Frontend

**web.auth** (replace the description)
Login, register, verify-email, forgot-password, reset-password, and parent consent. Route file is page.tsx. Form components end with Form (LoginForm, RegisterForm, VerifyEmailForm, ForgotPasswordForm, ResetPasswordForm, ParentConsentForm, plus RegisterCenterForm and VerifyCenterEmailForm for a consulting-center account). Helpers: AgeGate, ProtectedRoute, AuthSplitLayout, PasswordInput.

**web.centers** (replace the description)
Public study-center pages: list and detail. Components are PublicCenterListView, StudyCenterDetailView, ContactRequestDialog, ConsultationBookingModal, CenterLegalInfoCard, CenterBadges (type, legal-mode and verification-status badges), and CenterReviewList (the visible reviews of a center, read with the use-center-reviews hook). A block of the detail page is shown only when the center has real data for it.

**web.center** (replace the description)
Workspace of a Consulting Center account (routes under app/center: dashboard, profile, verification, leads). Components are CenterDashboardView, CenterStatusBanner, CenterProfileView, CenterContactCard (change of phone and e-mail with an e-mail OTP), CenterVerificationView (legal data and documents for the Moderator review), CenterNav, and CenterLeadsListView with LeadConversationPanel for the contact requests the center received.

**web.admin** (replace the description)
Administrator and Moderator screens (routes under app/admin: accounts, users, universities, centers, parameters, plans, ai-usage, forum, knowledge). Components are AdminShell, AdminOverview, AdminUsersView, AdminSection, with sub-folders ai-usage, centers, parameters, universities, shared. The centers sub-folder is for the Moderator only: AdminCenterListView, AdminCenterDetailView, AdminCenterDetailPanel, AdminVerificationQueueModal and ReviewVerificationModal (approve, reject, request changes or suspend, with a reason), CenterActionConfirmDialog, EditCenterButton, CenterLegalFieldsSection.

## Backend

**StudyAbroad.Api.Controllers** (replace the controller lists)
Student side: AuthController, ParentLinksController, ProfileFlowController, ProfileMetadataController, AcademicProfileController, AcademicAnalysisController, FinancialProfileController, RecommendationsController, RecommendationHistoryController, ScenariosController, TargetSchoolsController, UniversitiesController, AdvisorController, StudyCentersController (public catalog and the reviews of a center), LeadsController, MediaController. Center side: CenterPortalController (profile, contact change, verification submission). Admin side: AdminUsersController, AdminUniversitiesController, AdminSettingsController, AdminAiUsageController, AdminChatReviewController, AdminCenterVerificationsController (the Moderator verification queue and decisions).

**StudyAbroad.Application.StudyCenters** (replace the description)
Center catalog, center self-service and the verification workflow. Service is StudyCenterService. Ports are IStudyCenterRepository, ICenterVerificationRepository (verification requests and their documents) and ICenterReviewRepository (visible reviews). Helper type is CenterReferral; DTOs are in StudyCenterDtos and CenterVerificationDtos.

**StudyAbroad.Application.Auth** (add one sentence)
Also issues and checks the e-mail OTP for a change of contact e-mail (purpose ChangeEmail).

**StudyAbroad.Infrastructure.Persistence** (replace the description)
EF Core. Context is AppDbContext. One {Entity}Configuration per entity (folder Configurations). One {Feature}Repository per application port (folder Repositories: UserRepository, ProfileFlowRepository, RecommendationRepository, LeadRepository, AuditLogRepository, StudyCenterRepository, CenterVerificationRepository, CenterReviewRepository). StudyCenterRepository computes the review count and average rating from center_reviews on every read. Folder Migrations holds the schema migrations and DbSeeder seeds sample data for development.

**StudyAbroad.Domain** (entities row, if it lists entities)
Add CenterVerificationRequest and CenterVerificationDocument, and the constants CenterVerificationStatuses and CenterDocumentTypes.

## What I checked

- Every class and port name in both package documents exists in the code (236 names checked, 0 missing).
- Controllers, repositories and ports that exist in code but are absent from the documents: AdminCenterVerificationsController, CenterPortalController, ICenterVerificationRepository, ICenterReviewRepository, CenterVerificationRepository, CenterReviewRepository, and the center-portal and admin-center components listed above. The rows above add them.
- The documents also leave out some older items that are not about centers: AdminUniversityRepository, AiUsageReportRepository, ChatReviewRepository, GradeScaleSettingRepository and the interfaces IAcademicAnalysisService, IAcademicProfileService, IFinancialProfileService, IProfileFlowService, IProfileMetadataService. The text there still says "one Repository per port", so this is an omission in the list, not an error.
- Not checked: the package diagram image itself (I cannot see it), and the counts "16 packages" for the Application layer.
