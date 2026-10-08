import type {
  AuthResponse,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResendOtpRequest,
  ResetPasswordRequest,
  UserDto,
  VerifyEmailRequest,
  AcademicProfileResponse,
  SaveAcademicProfileRequest,
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

  // [USAS-12] Xác thực mã OTP kích hoạt email đăng ký
  verifyEmail: (data: VerifyEmailRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/verify-email", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [USAS-12] Gửi lại mã OTP (verify_email, reset_password, unlock_account)
  resendOtp: (data: ResendOtpRequest) =>
    apiFetch<{ message: string }>("/api/v1/auth/resend-otp", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [USAS-12] Quên mật khẩu - gửi mã OTP qua email
  forgotPassword: (data: ForgotPasswordRequest) =>
    apiFetch<{ message: string }>("/api/v1/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [USAS-12] Đặt lại mật khẩu mới với mã OTP
  resetPassword: (data: ResetPasswordRequest) =>
    apiFetch<{ message: string }>("/api/v1/auth/reset-password", {
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

// [USAS-363] API Quản lý hồ sơ học thuật & Bảng điểm (Học sinh / Phụ huynh)
export const profileApi = {
  // Lấy hồ sơ học thuật của người dùng hiện tại (chống IDOR qua HttpOnly Cookie)
  getAcademicProfile: () =>
    apiFetch<AcademicProfileResponse>("/api/v1/profile/academic", {
      method: "GET",
    }),

  // Lưu hoặc cập nhật hồ sơ học thuật, bảng điểm từng học kỳ và chứng chỉ
  saveAcademicProfile: (data: SaveAcademicProfileRequest) =>
    apiFetch<AcademicProfileResponse>("/api/v1/profile/academic", {
      method: "PUT",
      body: JSON.stringify(data),
    }),
};

