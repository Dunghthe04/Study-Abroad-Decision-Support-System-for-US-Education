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
