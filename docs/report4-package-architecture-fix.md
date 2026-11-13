# Report 4: corrected rows for Architecture, Package descriptions and two other places (copy from here)

Every class, file and route name below was checked against the repository on 13/11/2026. The package diagram image was not checked (I cannot see it), so the package names in the image must match the names used here.

## A. Section I.1 Architecture component descriptions (fill the empty table)

Replace the two placeholder rows in each tier with the rows below.

| No | Component | Description |
| :---- | :---- | :---- |
| Client Tier | | |
| 01 | Pages (Next.js App Router) | Route files page.tsx under frontend/src/app: auth, account, profile wizard, academic analysis, recommendations, advisor chat, public center and university pages, consulting-center workspace (app/center) and the Administrator and Moderator screens (app/admin). |
| 02 | Components | React components grouped by feature (components/auth, flow, academic, recommendation, assistant, centers, center, admin, universities, ui). Server pages forward the session cookie to the API. |
| 03 | API client and proxy | frontend/src/lib/*-api.ts call the API with credentials included and cache: "no-store". The route app/api/[...path]/route.ts proxies /api to the Backend API, so the browser talks to one origin and never to the Advisor. |
| Application Tier | | |
| 04 | Nginx reverse proxy | TLS termination, redirect to HTTPS, security headers, request size limits and the first rate-limit layer (zones auth and advisor). Only Nginx publishes ports in production. |
| 05 | JWT Security Filter | JwtBearer handler in Program.cs. It reads the token from the HttpOnly cookie, validates signature, issuer, audience and expiry, then checks the session hash in user_sessions and the user status; a revoked or locked session gets 401. |
| 06 | Authorization policies | [Authorize(Roles = ...)] for the five roles and the age policy in AgeFeatureAuthorization.cs (AgeFeatureRequirement, AgeFeatureHandler, AgeRestrictionResultHandler). |
| 07 | Rate limiters | ASP.NET Core rate-limit policies AuthRateLimit, TranscriptUploadRateLimit and MediaUploadRateLimit (second layer after Nginx). |
| 08 | Controllers | One {Feature}Controller per use case, including CenterPortalController and AdminCenterVerificationsController for the consulting-center workflow. They only call services. |
| 09 | Application services | Business rules, one service per use case (AuthService, ProfileFlowService, RecommendationService, ChatService, StudyCenterService, LeadService). They depend on ports (I...Repository, IAdvisorClient), not on EF Core or HTTP. |
| 10 | Repositories and EF Core | AppDbContext, entity configurations and {Feature}Repository classes that implement the ports. |
| 11 | Advisor client | AdvisorClient and TranscriptOcrClient call the Advisor with an X-Api-Key header and explicit timeouts. |
| 12 | Background services | OtpCleanupBackgroundService, AccountUnlockBackgroundService and EmailDispatchBackgroundService (queued e-mail). |
| Advisor Tier (Python FastAPI) | | |
| 13 | Advisor API routes | app/api/routes: chat.py (chat and chat stream), recommend.py (ec, explain, what-if, compare, goal-seek), profile.py (transcript-ocr), health.py. Reachable only from the Backend API. |
| 14 | Skills and prompts | Skill modules in app/services/skills and prompt files in app/skills (skill_*.md). |
| 15 | Model clients | ClaudeClient (cloud model, only when a key is set) and OllamaClient (local model), plus OpenAiEmbeddingClient for embeddings. Personal data is masked first by pii_mask.mask_personal_info. |
| 16 | Knowledge retrieval | KnowledgeRetriever reads verified rows of knowledge_documents by vector similarity. |
| Data and External Services | | |
| 17 | PostgreSQL 16 with pgvector | Schema app, 51 tables; the vector index on knowledge_documents uses HNSW. |
| 18 | File storage | Uploaded transcripts under uploads/drafts/{userId}/ (not served as static files). |
| 19 | Cloudinary | Logo, cover and gallery images of universities, uploaded only by Administrator or Moderator through MediaController. |
| 20 | SMTP | Sends OTP and notification e-mail through QueuedEmailSender and SmtpEmailSender. |

## B. Section I.2 Package descriptions: rows to replace or add

Rows not listed here are unchanged.

| No | Package | Description |
| :---- | :---- | :---- |
| 02 | web.account | Account page. View and card components end with View or Card (AccountView, PersonalInfoCard, ChangePasswordCard, ParentLinksCard, AcademicSummaryCard, SentLeadsCard). The administrator user list (AdminUsersView) belongs to web.admin. |
| 08 | StudyAbroad.Api.Controllers | HTTP entry. One {Feature}Controller per use case, subclass of ControllerBase. Student side: AuthController, ParentLinksController, ProfileFlowController, RecommendationsController, AdvisorController, StudyCentersController, LeadsController. Center side: CenterPortalController. Admin side: AdminUsersController, AdminCenterVerificationsController. |
| 10 | StudyAbroad.Api.RateLimiting | Rate-limit filters. Names end with RateLimit or Limiter: AuthRateLimit, TranscriptUploadRateLimit, MediaUploadRateLimit, PerUserUploadLimiter. |
| 17 | StudyAbroad.Application.Recommendations | One recommendation run, history and what-if scenarios. Classes are RecommendationService, RecommendationHistoryService, ScenarioService, HardFilter, GoalSeeker, SchoolGrouper, SchoolNameMatcher and EnglishNotice. Ports are IRecommendationRepository, IRecommendationHistoryRepository, IScenarioRepository and IRecommendationRunner. |
| 19 | StudyAbroad.Application.StudyCenters | Center catalog, center self-service and the verification workflow. Service is StudyCenterService. Ports are IStudyCenterRepository, ICenterVerificationRepository (verification requests and documents) and ICenterReviewRepository (visible reviews). Helper type is CenterReferral. |
| 23 | StudyAbroad.Infrastructure.Persistence | EF Core. Context is AppDbContext. One {Entity}Configuration per entity. One {Feature}Repository per application port (UserRepository, ProfileFlowRepository, RecommendationRepository, StudyCenterRepository, CenterVerificationRepository, CenterReviewRepository, AuditLogRepository). StudyCenterRepository computes the review count and rating from center_reviews on every read. |
| 24 | StudyAbroad.Infrastructure.Auth | JWT, password hash and SMTP. Class names drop the I prefix: TokenService, PasswordHasher, SmtpEmailSender, QueuedEmailSender. EmailDispatchBackgroundService is declared in QueuedEmailSender.cs. |
| 29 | app.services | One module per task. Model clients: ClaudeClient (claude.py), OllamaClient (ollama.py), OpenAiEmbeddingClient (embedding.py). Classes: Explainer, SkillCall, KnowledgeRetriever. Writers: advice_writer, compare_writer, goal_writer, scenario_writer, what_if_writer. Functions are snake_case (mask_personal_info in pii_mask.py). |
| 31 | app.schemas | Pydantic models. Names end with Request or Response (ChatRequest, ExplainRequest, EcRequest, CompareNoteRequest, GoalNoteRequest, ScenarioNoteRequest, TranscriptOcrRequest). CamelModel in common.py emits camelCase JSON. |
| 33 | app.skills | Prompt files. Pattern is skill_{name}.md, including skill_ec, skill_explain, skill_ho_so, skill_tra_loi, skill_danh_gia, skill_bac_hoc, skill_goal_seek and skill_so_sanh. |
| new | web.center | Workspace of a Consulting Center account (routes under app/center: dashboard, profile, verification, leads). Components are CenterDashboardView, CenterStatusBanner, CenterProfileView, CenterContactCard (change of phone and e-mail with an e-mail OTP), CenterVerificationView, CenterNav, CenterLeadsListView and LeadConversationPanel. |
| new | web.admin | Administrator and Moderator screens (routes under app/admin). Components are AdminShell, AdminOverview, AdminUsersView, AdminSection and sub-folders. The centers sub-folder is for the Moderator: AdminCenterListView, AdminCenterDetailView, AdminCenterDetailPanel, AdminVerificationQueueModal, ReviewVerificationModal (approve, reject, request changes or suspend with a reason), CenterActionConfirmDialog, EditCenterButton, CenterLegalFieldsSection. |
| new | StudyAbroad.Application.Admin | Administrator use cases, one sub-package each: AiUsage (AiUsageReportService), ChatReview (ChatReviewService), Settings (GradeScaleSettingService), Universities (AdminUniversityService). Each has its own I...Repository port. |

## C. Section I.3 Database Design: fix the intro and 3.23

1. In the paragraph under "3. Database Design", change "has 42 tables" to "has 51 tables" and add "consulting centers (3.23–3.26 and 3.50–3.51)" to the group list. Tables 3.43 to 3.51 are listed after 3.42 in this report; add 3.43 to 3.51 to the Table of Contents.
2. In 3.23 study_centers, change `verification_status` to VARCHAR(32) and add these columns (all NULL): email_verified_at, phone_verified_at, terms_agreed_at, commitment_agreed_at (TIMESTAMPTZ), registrant_name and registrant_position (VARCHAR(100)), fee_min and fee_max (NUMERIC(14,2)), fee_description (VARCHAR(1000)). The full rows are in db-section3-corrected.docx.
3. In the Notes of `average_rating` and `review_count`, write that the application does not read these columns; the values shown are recalculated from the visible rows of center_reviews.

## D. Section III.2 Performance & Scalability: fix these entries

| Where | Current text | Replace with |
| :---- | :---- | :---- |
| 2.1 table header | Redis Key | Cache Key (store) |
| 2.1 rows | Only the profile metadata is listed as an API cache | Also add: grade-scale configuration (GradeScaleProvider, key grade_scale_10_cache, 10 minutes, Invalidate() when the Administrator saves the setting) and the VND/USD exchange rate (ExchangeRateProvider, key exchange_rate_vnd_per_usd, 1 day, last value kept 1 hour after a failed fetch). All three are IMemoryCache entries of the API process. |
| 2.1 profile metadata row | InvalidateCache() exists but is not called when app_settings changes | Correct as written (InvalidateCache is defined but only the grade-scale provider is invalidated on save). |
| 2.2, "School scoring (advisor service)" | Batch size BATCH_SIZE = 8 in Scorer | Delete this row. The class Scorer and the constant do not exist in the code any more. |
| 2.2, "Before calling Gemini (UC-22)" | Gemini | Remove the word Gemini; the advisor has no Gemini client (cloud model client is ClaudeClient, used only when a key is set). |
| 2.2 and 2.1, "does not call Gemini" | Gemini | Replace with "does not call the model again". |
| 2.2, last row | API→advisor timeout 120s (OCR 600s); Nginx 30 req/min/IP on AI routes | The default in AdvisorOptions is 120 s and 600 s for OCR, but appsettings.json sets TimeoutSeconds to 300. The production Nginx template allows 10 requests per minute per IP on the advisor zone (30 is the local file). Quote the values of the environment you describe. |

## E. Not checked

- The package diagram image and whether its box names match the table.
- The Architecture diagram image (section I.1) itself.
- Section II (Detailed Code Design) is still the template with no feature chosen.
