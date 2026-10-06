import type {
  AdvisorChatResponse,
  AuthResponse,
  LoginRequest,
  PagedResult,
  RegisterRequest,
  StudyCenter,
  UserDto,
} from "@/types/api";

// Browser code calls same-origin /api (Nginx in prod, Next rewrite in dev).
// Server components run inside the container network and call the API directly via API_INTERNAL_URL.
const baseUrl = typeof window === "undefined" ? (process.env.API_INTERNAL_URL ?? "http://localhost:5080") : "";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    ...init,
    // [Bảo mật HttpOnly Cookie]: credentials: "include" tự động gửi và nhận cookie an toàn
    credentials: "include",
    headers: { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message = body?.detail ?? body?.title ?? body?.message ?? res.statusText;
    throw new ApiError(res.status, message);
  }

  // Handle empty responses (e.g. 204 No Content)
  if (res.status === 204) {
    return {} as T;
  }

  return res.json() as Promise<T>;
}

// [USAS-362] Bộ API Xác thực & Người dùng (Khách: Học sinh / Phụ huynh)
export const authApi = {
  // [AC-1] Đăng ký tài khoản (học sinh hoặc phụ huynh, email/SĐT, mật khẩu)
  register: (data: RegisterRequest) =>
    apiFetch<UserDto>("/api/v1/auth/register", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [AC-2] Đăng nhập: Backend thiết lập HttpOnly Cookie "usas_access_token"
  login: (data: LoginRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [AC-3] Đăng xuất: Thu hồi phiên trên server và xóa HttpOnly Cookie (không cần truyền token thủ công)
  logout: () =>
    apiFetch<{ message: string }>("/api/v1/auth/logout", {
      method: "POST",
    }),

  // [AC-4] Lấy thông tin cá nhân từ phiên HttpOnly Cookie hiện tại (Chống IDOR & Chống XSS)
  getMe: () =>
    apiFetch<UserDto>("/api/v1/auth/me", {
      method: "GET",
    }),
};
