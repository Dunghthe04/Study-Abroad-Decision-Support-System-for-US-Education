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
}

// Công cụ nhập ngay trong chat; frontend vẽ theo `type`
export interface ChatWidget {
  type: "major" | "review" | "budget" | "scholarship";
  data?: Record<string, unknown> | null;
}

export interface AdvisorChatResponse {
  answer: string;
  citations: Citation[];
  disclaimer: string | null;
  sessionId: string | null;
  quickReplies?: QuickReply[] | null;
  widget?: ChatWidget | null;
  profileChanged?: string[] | null; // khóa hồ sơ chat vừa đổi; form cần nạp lại khi có giá trị
}

// [USAS-362] Kiểu dữ liệu xác thực và người dùng
export type UserRole = "student" | "parent" | "center" | "admin";

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
