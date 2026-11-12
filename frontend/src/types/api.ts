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
  type: "major" | "review" | "budget" | "scholarship" | "form" | "select" | "recommend" | "simulate" | "goal_seek" | "explain" | "school_info" | "school_compare" | "school_search" | "school";
  data?: Record<string, unknown> | null;
}

/**
 * Hành động có cấu trúc từ nút trên giao diện ("Cần gì để vào?", "Vì sao nhóm này?" ở thẻ trường): gửi mã trường thẳng cho backend,
 * không qua SLM hiểu câu nên nhanh và không nhầm tên trường. target chỉ dùng cho goal_seek (bỏ trống thì hệ thống chọn nhóm cao hơn một bậc).
 */
export interface AssistantAction {
  type: "goal_seek" | "explain" | "school_info" | "school_compare";
  schoolCodes?: string[] | null; // school_compare: mã từ hai đến bốn trường (hoặc tên chưa được chọn)
  fields?: string[] | null; // school_compare: mã mục so sánh, giữ nguyên khi chọn lại trường mơ hồ
  schoolCode?: string | null; // goal_seek không kèm trường: hỏi lại bằng ô chọn trường
  question?: string | null; // school_info: câu hỏi gốc, để biết hỏi mục nào (học phí, học bổng...); trống thì tóm tắt về trường
  target?: "match" | "safety" | null;
  suggested?: string[] | null; // ô chọn trường: các trường nên hiện sẵn dạng nút (backend điền, giao diện gửi lại nguyên)
  /** "real": giải thích theo hồ sơ thật dù trang đang xem giả định (nút "Xem theo hồ sơ thật"); bỏ trống: theo cái đang xem */
  basis?: "real" | null;
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
}

// Token chỉ nằm trong HttpOnly cookie, body không chứa token
export interface AuthResponse {
  expiresAt: string;
  user: UserDto;
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
export type ParentLinkStatus = "pending" | "active" | "revoked" | "expired";

export interface ParentInviteInfo {
  studentName: string;
  studentAge: number | null;
  parentEmail: string;
  expiresAt: string;
  policyVersion: string;
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
  isFlagged: boolean;
  flagReason?: string | null;
  status: "visible" | "removed" | string;
  createdAt: string;
}

export interface LeadMessagesThreadDto {
  leadId: string;
  leadStatus: string;
  messages: LeadMessageDto[];
  flaggedCount: number;
}

export interface SendLeadMessageRequest {
  content: string;
}

export interface SendLeadMessageResultDto {
  message: LeadMessageDto;
  leadStatus: string;
}

