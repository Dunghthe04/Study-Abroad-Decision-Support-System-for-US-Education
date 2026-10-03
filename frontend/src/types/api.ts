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

// --- Authentication DTOs (API Contract) ---

export type UserRole = "student" | "parent" | "center";

export type UserStatus = "Active" | "PendingApproval" | "Locked" | string;

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  status?: UserStatus;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  role: UserRole;
  parentAcknowledged?: boolean;
}

export interface AuthResponse {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  status?: UserStatus;
}

