import type { ProfileFlowDraft } from "@/types/profile-flow";

// Mirrors the DTOs of the ASP.NET Core API (StudyAbroad.Application).

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface StudyCenter {
  id: string;
  code: string;
  name: string;
  website: string | null;
  address: string | null;
  city: string | null;
  studyLevels: string[];
  services: string[];
  surveyedAt: string | null;
  phone?: string | null;
  email?: string | null;
  description?: string | null;
  verificationStatus?: string;
  isActive?: boolean;
  centerType?: string;
  logoUrl?: string | null;
  coverImageUrl?: string | null;
  galleryImages?: string[];
  district?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  staffCount?: number | null;
  establishedYear?: number | null;
  licenseNumber?: string | null;
  accreditations?: string[];
  visaSuccessRate?: number | null;
  averageRating?: number;
  reviewCount?: number;
  verifiedAt?: string | null;
  verifiedByUserId?: string | null;
  taxCode?: string | null;
  legalRepresentative?: string | null;
  licenseIssuedBy?: string | null;
  licenseIssuedDate?: string | null;
  legalComplianceMode?: string;
  trainingLicenseNumber?: string | null;
  certifiedCounselorCount?: number | null;
  legalDocumentUrls?: string[];
  verificationNote?: string | null;
  emailVerifiedAt?: string | null;
  phoneVerifiedAt?: string | null;
  feeMin?: number | null;
  feeMax?: number | null;
  feeDescription?: string | null;
  registrantName?: string | null;
  registrantPosition?: string | null;
  /** [UC-92] Fanpage chính thức; đổi thì Moderator kiểm tra (UC-114). */
  fanpage?: string | null;
  /** [UC-92] Khu vực phục vụ và chi nhánh do trung tâm công bố. */
  serviceAreas?: string[] | null;
  branches?: string[] | null;
}

export interface RegisterCenterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  centerName: string;
  phone: string;
  registrantName: string;
  registrantPosition?: string;
  agreeTerms: boolean;
  agreeTruthfulnessCommitment: boolean;
}

export interface VerifyCenterEmailRequest {
  email: string;
  code: string;
}

export interface VerifyPhoneOtpRequest {
  code: string;
}

export interface VerificationDocumentUploadDto {
  documentType: string;
  fileUrl: string;
  fileName: string;
  fileSize: number;
  contentType: string;
}

export interface CenterVerificationDocumentDto {
  id: string;
  requestId: string;
  documentType: string;
  fileUrl: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: string;
}

export interface SubmitVerificationDto {
  legalName: string;
  taxCode: string;
  address: string;
  legalRepresentative: string;
  website?: string | null;
  documents?: VerificationDocumentUploadDto[];
}

export interface CenterVerificationDto {
  id: string;
  centerId: string;
  requestedByUserId: string;
  legalName: string;
  taxCode: string;
  address: string;
  legalRepresentative: string;
  website?: string | null;
  status: string;
  submittedAt: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  rejectionReason?: string | null;
  reviewNotes?: string | null;
  isNationalRegistryMatched: boolean;
  isBusinessOperating: boolean;
  isRepresentativeValid: boolean;
  documents: CenterVerificationDocumentDto[];
  centerName?: string | null;
  centerCode?: string | null;
  centerEmail?: string | null;
  centerPhone?: string | null;
  emailVerifiedAt?: string | null;
  phoneVerifiedAt?: string | null;
}

export interface CenterVerificationStatusDto {
  verificationStatus: string;
  isEmailVerified: boolean;
  emailVerifiedAt?: string | null;
  isPhoneVerified: boolean;
  phoneVerifiedAt?: string | null;
  currentRequest?: CenterVerificationDto | null;
  rejectionReason?: string | null;
}

export interface ReviewCenterVerificationRequest {
  action: "approve" | "reject" | "changes_requested" | "revoke";
  isNationalRegistryMatched?: boolean;
  isBusinessOperating?: boolean;
  isRepresentativeValid?: boolean;
  reviewNotes?: string | null;
  rejectionReason?: string | null;
}

export interface UpdateCenterProfileRequest {
  name?: string;
  description?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  district?: string | null;
  services?: string[];
  studyLevels?: string[];
  centerType?: string;
  establishedYear?: number | null;
  staffCount?: number | null;
  feeMin?: number | null;
  feeMax?: number | null;
  feeDescription?: string | null;
  logoUrl?: string | null;
  coverImageUrl?: string | null;
  /** Thông tin pháp lý: chỉ gửi khi trung tâm chưa được xác minh. */
  taxCode?: string | null;
  legalRepresentative?: string | null;
  licenseNumber?: string | null;
  licenseIssuedBy?: string | null;
  licenseIssuedDate?: string | null;
  legalComplianceMode?: string | null;
  trainingLicenseNumber?: string | null;
  certifiedCounselorCount?: number | null;
  /** [UC-92] Lưu ngay nhưng Moderator kiểm tra (UC-114). */
  fanpage?: string | null;
  serviceAreas?: string[];
  branches?: string[];
}

/** [UC-92] Phân loại thay đổi sau khi trung tâm tự sửa hồ sơ. */
export interface CenterProfileUpdateOutcome {
  /** Lưu và hiển thị ngay. */
  savedFields: string[];
  /** Lưu ngay nhưng chờ Moderator kiểm tra (website, fanpage). */
  reviewFields: string[];
  /** Thông tin pháp lý đã đổi. */
  legalFields: string[];
  /** Trung tâm đã xác minh đổi thông tin pháp lý: hồ sơ về Chờ duyệt, tạm ẩn dấu xác minh. */
  verificationReset: boolean;
  message: string;
}

export interface CenterProfileUpdateResponse {
  center: StudyCenter;
  outcome: CenterProfileUpdateOutcome;
}

/** [UC-93] Trạng thái yêu cầu xóa trung tâm: chờ xử lý, đã duyệt (chờ tới hạn xóa hẳn), đã xóa, bị từ chối. */
export type CenterDeletionRequestStatus = "pending" | "approved" | "completed" | "rejected";

/** [UC-93] Yêu cầu xóa trong danh sách của Administrator, kèm thông tin kiểm tra danh tính người gửi. */
export interface AdminCenterDeletionRequestDto {
  id: string;
  studyCenterId: string;
  centerName: string;
  centerEmail: string | null;
  centerPhone: string | null;
  centerVerificationStatus: string;
  requestedByUserId: string | null;
  requesterName: string | null;
  requesterEmail: string | null;
  requesterIsOwner: boolean;
  reason: string;
  status: CenterDeletionRequestStatus;
  createdAt: string;
  handledByUserId: string | null;
  handledAt: string | null;
  decisionNote: string | null;
  scheduledPurgeAt: string | null;
  completedAt: string | null;
}

/** [UC-92 / UC-93] Yêu cầu xóa tài khoản trung tâm. status: pending | approved | completed | rejected. */
export interface CenterDeletionRequestDto {
  id: string;
  studyCenterId: string;
  reason: string;
  status: string;
  createdAt: string;
  handledAt: string | null;
}

export interface VerifyStudyCenterRequest {
  isVerified: boolean;
  verificationNote?: string | null;
}

export interface CreateStudyCenterRequest {
  code: string;
  name: string;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  studyLevels?: string[];
  services?: string[];
  phone?: string | null;
  email?: string | null;
  description?: string | null;
  surveyedAt?: string | null;
  centerType?: string;
  logoUrl?: string | null;
  coverImageUrl?: string | null;
  galleryImages?: string[];
  district?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  staffCount?: number | null;
  establishedYear?: number | null;
  licenseNumber?: string | null;
  accreditations?: string[];
  visaSuccessRate?: number | null;
  taxCode?: string | null;
  legalRepresentative?: string | null;
  licenseIssuedBy?: string | null;
  licenseIssuedDate?: string | null;
  legalComplianceMode?: string | null;
  trainingLicenseNumber?: string | null;
  certifiedCounselorCount?: number | null;
  legalDocumentUrls?: string[];
  verificationNote?: string | null;
}

export interface UpdateStudyCenterRequest {
  name: string;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  studyLevels?: string[];
  services?: string[];
  phone?: string | null;
  email?: string | null;
  description?: string | null;
  centerType?: string;
  logoUrl?: string | null;
  coverImageUrl?: string | null;
  galleryImages?: string[];
  district?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  staffCount?: number | null;
  establishedYear?: number | null;
  licenseNumber?: string | null;
  accreditations?: string[];
  visaSuccessRate?: number | null;
  taxCode?: string | null;
  legalRepresentative?: string | null;
  licenseIssuedBy?: string | null;
  licenseIssuedDate?: string | null;
  legalComplianceMode?: string | null;
  trainingLicenseNumber?: string | null;
  certifiedCounselorCount?: number | null;
  legalDocumentUrls?: string[];
  verificationNote?: string | null;
}

export interface SetStudyCenterActiveRequest {
  isActive: boolean;
}

export type ChatRole = "user" | "assistant";

export interface ChatMessage {
  role: ChatRole;
  content: string;
}

export interface Citation {
  title: string;
  url: string | null;
  snippet: string | null;
  /** Ngày thu thập thông tin của nguồn, dạng yyyy-MM-dd. Null là kho chưa ghi ngày. */
  retrievedAt?: string | null;
}
// Nút gợi ý: bấm thì gửi `value` như tin nhắn của học sinh
export interface QuickReply {
  label: string;
  value: string;
  action?: AssistantAction | null; // có thì nút chạy thẳng hành động theo mã trường (giải thích, goal-seeking), không qua SLM
  href?: string | null; // có thì nút chuyển sang trang đó (ví dụ "Sửa hồ sơ"), không gửi tin nhắn
}

// Công cụ nhập ngay trong chat; frontend vẽ theo `type`
export interface ChatWidget {
  type: "major" | "review" | "budget" | "scholarship" | "form" | "select" | "recommend" | "simulate" | "goal_seek" | "explain" | "school_info" | "school_compare" | "school_search" | "school" | "centers";
  data?: Record<string, unknown> | null;
}

/**
 * Hành động có cấu trúc từ nút trên giao diện ("Cần gì để vào?", "Vì sao nhóm này?" ở thẻ trường): gửi mã trường thẳng cho backend,
 * không qua SLM hiểu câu nên nhanh và không nhầm tên trường. target chỉ dùng cho goal_seek (bỏ trống thì hệ thống chọn nhóm cao hơn một bậc).
 */
export interface AssistantAction {
  type: "goal_seek" | "explain" | "school_info" | "school_compare" | "school_search";
  schoolCodes?: string[] | null; // school_compare: mã từ hai đến bốn trường (hoặc tên chưa được chọn)
  fields?: string[] | null; // school_compare: mã mục so sánh, giữ nguyên khi chọn lại trường mơ hồ
  schoolCode?: string | null; // goal_seek không kèm trường: hỏi lại bằng ô chọn trường
  question?: string | null; // school_info: câu hỏi gốc, để biết hỏi mục nào (học phí, học bổng...); trống thì tóm tắt về trường
  target?: "match" | "safety" | null;
  suggested?: string[] | null; // ô chọn trường: các trường nên hiện sẵn dạng nút (backend điền, giao diện gửi lại nguyên)
  /** "real": giải thích theo hồ sơ thật dù trang đang xem giả định (nút "Xem theo hồ sơ thật"); bỏ trống: theo cái đang xem */
  basis?: "real" | null;
  /** school_search: bộ điều kiện tìm dựng sẵn (nút "Chỉ top 20", "Bỏ điều kiện bang"...), backend điền, giao diện gửi lại nguyên */
  search?: Record<string, unknown> | null;
}

export interface AdvisorChatResponse {
  answer: string;
  citations: Citation[];
  disclaimer: string | null;
  sessionId: string | null;
  quickReplies?: QuickReply[] | null;
  widget?: ChatWidget | null;
  profileChanged?: string[] | null; // khóa hồ sơ chat vừa đổi; form cần nạp lại khi có giá trị
  profileSaved?: boolean | null; // lượt này vừa lưu hồ sơ chính thức (bấm "Đúng rồi" ở bảng rà soát)
  formStep?: number | null; // trang form (1–6) của câu chat đang hỏi; form chuyển theo
  formGoals?: string[] | null; // việc học sinh đã hỏi khi điền hồ sơ trong chat (danh_gia, lo_trinh, goi_y_truong); form tích sẵn
}

/** Một dòng của POST /api/v1/advisor/chat/stream (mỗi dòng một JSON): trả lời theo từng ý khi xong. */
export type AdvisorStreamEvent =
  | { type: "progress"; answers: number } // sắp trả lời N câu hỏi
  | { type: "partial"; text: string } // câu trả lời của một ý, đúng thứ tự hỏi
  | { type: "card"; widget: ChatWidget } // thẻ kết quả do code dựng, tới trước lời Gemini để học sinh thấy đáp án ngay
  | { type: "final"; response: AdvisorChatResponse } // câu cuối cùng (kèm sessionId, nút, widget): hiện câu này
  | { type: "error"; message: string };

// [USAS-362] Kiểu dữ liệu xác thực và người dùng
export type UserRole = "student" | "parent" | "center" | "moderator" | "admin";

/** [UC-60] Tìm, lọc, phân trang danh sách tài khoản (admin). */
export interface AdminUserAccountListQuery {
  search?: string;
  role?: UserRole | "";
  status?: string;
  page?: number;
  pageSize?: number;
}

/** [UC-63] Admin tạo tài khoản moderator (khách không tự đăng ký). */
export interface CreateModeratorAccountRequest {
  email: string;
  password: string;
  fullName: string;
  phone?: string | null;
}

/** [Q11 / UC-71 Phụ trợ] Admin tạo tài khoản trung tâm tư vấn. */
export interface CreateCenterAccountRequest {
  email: string;
  password: string;
  fullName?: string | null;
  phone?: string | null;
}

/** [UC-61] Admin khóa tài khoản kèm lý do. */
export interface LockUserAccountRequest {
  reason: string;
}

/** [UC-62] Admin mở khóa tài khoản kèm lý do. */
/** [UC-113] Admin sửa ngày sinh Học sinh / Phụ huynh theo yêu cầu hỗ trợ. birthDate dạng yyyy-MM-dd, lý do bắt buộc. */
export interface UpdateUserBirthDateRequest {
  birthDate: string;
  reason: string;
}

export interface UnlockUserAccountRequest {
  reason: string;
}

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  status: string;
  phone?: string | null;
  // [Q11] Server tính theo ngày sinh + đồng ý của phụ huynh; null/không có = không áp giới hạn
  ageGroup?: "child" | "minor" | "adult" | null;
  parentConsentStatus?: "granted" | "pending" | "none" | "not_required" | null;
  blockedFeatures?: string[] | null;
  lockReason?: string | null;
  lockoutEnd?: string | null;
  /** [UC-111] Ngày sinh (yyyy-MM-dd), chỉ trả về cho chính chủ hoặc Admin */
  dateOfBirth?: string | null;
  /** [UC-113] Tuổi tròn theo giờ VN, tính từ ngày sinh (có khi xem chi tiết hoặc sau khi Admin sửa ngày sinh) */
  age?: number | null;
}

// Token chỉ nằm trong HttpOnly cookie, body không chứa token
export interface AuthResponse {
  expiresAt: string;
  user: UserDto;
}

// [UC-80] Khôi phục tài khoản đang chờ xóa
export interface RestoreAccountRequest {
  email: string;
  restoreToken: string;
}

/** [UC-80] Phần mở rộng của lỗi 403 khi đăng nhập vào tài khoản đang chờ xóa */
export interface PendingDeletionProblem {
  code: "account_pending_deletion";
  deletionDate: string;
  restoreToken: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  // Trung tâm không tự đăng ký (tài khoản do admin tạo)
  role: "student" | "parent";
  /** yyyy-MM-dd; bắt buộc */
  dateOfBirth: string;
  /** Bắt buộc với học sinh dưới 16, không bắt buộc với 16–17 */
  parentEmail?: string | null;
  phone?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

// [USAS-12] Kiểu dữ liệu xác thực OTP & Quên mật khẩu & Mở khóa tài khoản
export interface VerifyEmailRequest {
  email: string;
  otpCode: string;
}

export interface ResendOtpRequest {
  email: string;
  purpose: "verify_email" | "reset_password" | "unlock_account";
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  otpCode: string;
  newPassword: string;
}

// [UC-06] Đổi mật khẩu khi đã đăng nhập
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// Sửa thông tin cá nhân: email đăng nhập không đổi được. phone rỗng = xóa số
export interface UpdateProfileRequest {
  fullName: string;
  phone: string | null;
}

// [USAS-363] Hồ sơ học thuật (Academic Profile & Transcripts)
export interface TranscriptScoreItem {
  subject: string;
  rawScore?: string | null;
  score: number;
  credits?: number | null;
}

export interface TranscriptTerm {
  termName: string;
  termOrder: number;
  scores: TranscriptScoreItem[];
}

export interface AcademicProfileResponse {
  id: string;
  userId: string;
  targetLevel: string;
  currentSchool?: string | null;
  educationSystem?: string | null;
  graduationYear?: number | null;
  currentGrade?: string | null;
  gradeScale: string;
  overallGpa?: number | null;
  intendedMajor?: string | null;
  ielts?: number | null;
  toefl?: number | null;
  duolingo?: number | null;
  sat?: number | null;
  act?: number | null;
  gre?: number | null;
  gmat?: number | null;
  otherTestsJson?: string | null;
  terms: TranscriptTerm[];
  createdAt: string;
  updatedAt?: string | null;
  // Đã gửi hồ sơ 6 bước (hoặc có dữ liệu cũ)
  isComplete: boolean;
  // Đủ 5 bước của hồ sơ đã gửi (giống bước 6), null khi chưa gửi
  details?: ProfileFlowDraft | null;
}

export interface TargetSchoolSummaryDto {
  items: TargetSchoolDto[];
  totalCount: number;
  reachCount: number;
  matchCount: number;
  safetyCount: number;
  undecidedCategoryCount: number;
  nextDeadline?: string | null;
}

export interface UpdateApplicationRoundRequest {
  applicationRound: string;
  deadline?: string | null;
  autoResolveDeadline?: boolean;
}

// [Q11] Liên kết phụ huynh – học sinh (đồng ý xử lý dữ liệu trẻ em)
export type ParentLinkStatus = "pending" | "active" | "revoked" | "expired" | "rejected";

/** [UC-115] Trạng thái link mời: pending = chưa trả lời; accepted / rejected = đã dùng, chỉ hiện kết quả */
export type ParentInviteState = "pending" | "accepted" | "rejected";

export interface ParentInviteInfo {
  state: ParentInviteState;
  studentName: string;
  studentAge: number | null;
  parentEmail: string;
  /** Chỉ có khi còn chờ trả lời */
  expiresAt: string | null;
  /** Lúc phụ huynh bấm Đồng ý / Từ chối */
  decidedAt: string | null;
  policyVersion: string;
  /** Email đã là tài khoản học sinh / trung tâm / quản trị: không đồng ý được, chỉ hiện nút Từ chối */
  blockedReason?: string | null;
  hasParentAccount?: boolean;
}

/** [UC-117] Trang rút đồng ý từ link trong mail phụ huynh */
export interface ParentRevokeInfo {
  studentName: string;
  acceptedAt: string | null;
}

export interface ParentLinkDto {
  id: string;
  status: ParentLinkStatus;
  parentEmail: string;
  parentName: string | null;
  studentName: string;
  studentAge: number | null;
  /** [UC-42] Phụ huynh gửi id này khi gửi yêu cầu liên hệ trung tâm thay con */
  studentUserId: string;
  acceptedAt: string | null;
  lastInviteSentAt: string | null;
  tokenExpiresAt: string | null;
  /** Server tính theo luật rút đồng ý (học sinh dưới 16 không tự rút liên kết đã đồng ý) */
  canRevoke: boolean;
}

export interface ParentLinkResponse<T> {
  message: string | null;
  data: T;
}

// [UC-30] Trường mục tiêu của học sinh (Target Schools)
export interface AddTargetSchoolRequest {
  universityId?: string;
  universityCode?: string;
  category?: "reach" | "match" | "safety" | string | null;
  applicationRound?: string | null;
  deadline?: string | null;
  notes?: string | null;
}

export interface TargetSchoolDto {
  id: string;
  userId: string;
  universityId: string;
  universityCode: string;
  universityName: string;
  city?: string | null;
  state?: string | null;
  website?: string | null;
  control?: string | null;
  acceptanceRate?: number | null;
  category?: "reach" | "match" | "safety" | string | null;
  status: "considering" | "applying" | "submitted" | "admitted" | "rejected" | string;
  priority: number;
  notes?: string | null;
  applicationRound?: string | null;
  deadline?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface TargetSchoolSummaryDto {
  items: TargetSchoolDto[];
  totalCount: number;
  reachCount: number;
  matchCount: number;
  safetyCount: number;
  undecidedCategoryCount: number;
  nextDeadline?: string | null;
}

export interface UpdateApplicationRoundRequest {
  applicationRound: string;
  deadline?: string | null;
  autoResolveDeadline?: boolean;
}
// [UC-73] Quản lý yêu cầu liên hệ nhận được phía Trung tâm tư vấn (Leads)
export interface SharedProfileSummaryDto {
  overallGpa?: number | null;
  targetLevel?: string | null;
  intendedMajor?: string | null;
  educationSystem?: string | null;
  currentGrade?: string | null;
  englishScore?: number | null;
  englishTestType?: string | null;
  satScore?: number | null;
  actScore?: number | null;
  extracurricularTier?: number | null;
  budgetTier?: string | null;
}

export interface CenterReceivedLeadDto {
  id: string;
  studyCenterId: string;
  studyCenterName: string;
  studyLevel?: string | null;
  contactPhone?: string | null;
  message?: string | null;
  sharedProfile: boolean;
  status: "new" | "contacted" | "closed" | string;
  createdAt: string;
  studentName?: string | null;
  studentEmail?: string | null;
  profileSummary?: SharedProfileSummaryDto | null;
}

export interface CenterLeadsSummaryDto {
  items: CenterReceivedLeadDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  newCount: number;
  contactedCount: number;
  closedCount: number;
}

export interface UpdateLeadStatusRequest {
  status: string;
}

// [USAS-2053 / UC-74] Tin nhắn trao đổi trong yêu cầu liên hệ
export interface LeadMessageDto {
  id: string;
  leadId: string;
  senderUserId: string;
  senderName: string;
  senderRole: string;
  isFromCenter: boolean;
  content: string;
  /** Ảnh đính kèm (URL an toàn), null nếu tin chỉ có chữ. */
  attachmentUrl?: string | null;
  status: "visible" | "removed" | string;
  createdAt: string;
}

export interface LeadMessagesThreadDto {
  leadId: string;
  leadStatus: string;
  messages: LeadMessageDto[];
}

export interface SendLeadMessageRequest {
  content: string;
}

export interface SendLeadMessageResultDto {
  message: LeadMessageDto;
  leadStatus: string;
}

export type LeadMessageReportFilter = "pending" | "resolved" | "all";

/** [UC-58] Tin nhắn bị báo cáo (học sinh / phụ huynh hoặc trung tâm báo cáo), chỉ Admin thấy. `content` là bản gốc. */
export interface AdminLeadMessageReportDto {
  id: string;
  leadId: string;
  studyCenterId: string;
  studyCenterName: string;
  senderUserId: string;
  senderName: string;
  senderEmail?: string | null;
  senderRole: string;
  /** Trạng thái tài khoản người gửi (active, locked...). */
  senderStatus: string;
  isFromCenter: boolean;
  content: string;
  attachmentUrl?: string | null;
  status: "visible" | "removed" | string;
  reportedByUserId?: string | null;
  reportedAt?: string | null;
  reportReason?: string | null;
  createdAt: string;
  /** Kết quả Admin xử lý: removed (gỡ tin), warned (cảnh cáo), locked (khóa tài khoản). Trống là chưa xử lý. */
  reportResolution?: "removed" | "warned" | "locked" | string | null;
  reportResolvedAt?: string | null;
}

/** [UC-58] Một tin trong phần ngữ cảnh quanh tin bị báo cáo. */
export interface AdminReportContextMessageDto {
  id: string;
  senderUserId: string;
  senderName: string;
  isFromCenter: boolean;
  content: string;
  attachmentUrl?: string | null;
  status: "visible" | "removed" | string;
  createdAt: string;
  /** Chính là tin bị báo cáo. */
  isReported: boolean;
}

/** [UC-58] Tin bị báo cáo kèm tối đa contextSize tin liền trước và liền sau. Admin không đọc cả cuộc trò chuyện. */
export interface AdminLeadMessageReportContextDto {
  leadId: string;
  messageId: string;
  contextSize: number;
  messages: AdminReportContextMessageDto[];
}

/** Đánh giá công khai của trung tâm (tên người đánh giá đã được rút gọn ở backend). */
export interface CenterReviewDto {
  id: string;
  reviewerName: string;
  reviewerRole: "student" | "parent";
  isVerified: boolean;
  createdAt: string;
  rating: number;
  comment?: string | null;
}

/** [USAS-2070 / UC-45] Gửi đánh giá trung tâm: số sao 1 đến 5, nhận xét tùy chọn (tối đa 2000 ký tự). */
export interface CreateCenterReviewRequest {
  rating: number;
  comment?: string | null;
}

/** [UC-45] Người dùng hiện tại có được đánh giá trung tâm không; `reason` giải thích khi không được. */
export interface CenterReviewEligibilityDto {
  canReview: boolean;
  hasReviewed: boolean;
  reason?: string | null;
}

/** Đánh giá hiển thị + số liệu tổng hợp tính từ chính các đánh giá đó. */
export interface CenterReviewsDto {
  totalCount: number;
  averageRating: number | null;
  items: CenterReviewDto[];
}

// Hồ sơ học thuật giữa phụ huynh và con (api/v1/profile/academic/children, parent-offers)
export type ProfileTransferStatus = "offered" | "copied" | "declined" | "skipped";

export interface ParentChildProfileDto {
  linkId: string;
  studentUserId: string;
  studentName: string;
  profileTransferStatus: ProfileTransferStatus | null;
  profile: AcademicProfileResponse;
}

export interface ParentProfileOverviewDto {
  hasOwnProfile: boolean;
  /** Phụ huynh có hồ sơ tự nhập, có từ 2 con và chưa chọn con nào nhận */
  hasPendingOwnProfile: boolean;
  children: ParentChildProfileDto[];
}

export interface ShareParentProfileResult {
  status: ProfileTransferStatus;
  studentName: string | null;
}

export interface ParentProfileOfferDto {
  linkId: string;
  parentName: string;
  offeredAt: string;
  parentProfile: AcademicProfileResponse;
}

// [UC-110] Thông báo trong hệ thống (chỉ của người đăng nhập)
export interface NotificationDto {
  id: string;
  type: string;
  title: string;
  body: string | null;
  linkUrl: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

export interface UnreadCountDto {
  count: number;
}

/** [UC-114] Bộ lọc danh sách thay đổi trung tâm chờ Moderator kiểm tra. */
export type CenterProfileChangeFilter = "pending" | "passed" | "failed" | "all";

/** [UC-114] Một thay đổi website / fanpage do trung tâm tự sửa (UC-92). */
export interface CenterProfileChangeDto {
  id: string;
  studyCenterId: string;
  studyCenterName: string;
  centerVerificationStatus: string;
  centerIsActive: boolean;
  field: "website" | "fanpage" | string;
  oldValue?: string | null;
  newValue?: string | null;
  status: "pending" | "passed" | "failed" | string;
  createdAt: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  reviewReason?: string | null;
}
