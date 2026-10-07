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

export interface AdvisorChatResponse {
  answer: string;
  citations: Citation[];
  disclaimer: string | null;
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
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  role: "student" | "parent" | "center";
  parentAcknowledged?: boolean;
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

// [USAS-363] Hồ sơ học thuật (Academic Profile & Transcripts)
export type GradeScaleType = "10" | "100" | "4" | "letter";
export type EducationSystemType = "standard" | "specialized" | "dual_degree" | "international" | "private" | "other";

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

export interface SaveAcademicProfileRequest {
  targetLevel: string;
  currentSchool?: string | null;
  educationSystem?: string | null;
  graduationYear?: number | null;
  currentGrade?: string | null;
  gradeScale: GradeScaleType | string;
  intendedMajor?: string | null;
  ielts?: number | null;
  toefl?: number | null;
  duolingo?: number | null;
  sat?: number | null;
  act?: number | null;
  terms: TranscriptTerm[];
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
  terms: TranscriptTerm[];
  createdAt: string;
  updatedAt?: string | null;
}
