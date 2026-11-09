import type {
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResendOtpRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  UserDto,
  AdminUserAccountListQuery,
  PagedResult,
  CreateModeratorAccountRequest,
  CreateCenterAccountRequest,
  LockUserAccountRequest,
  UnlockUserAccountRequest,
  VerifyEmailRequest,
  AcademicProfileResponse,
  ParentInviteInfo,
  ParentLinkDto,
  ParentLinkResponse,
  StudyCenter,
  CreateStudyCenterRequest,
  AddTargetSchoolRequest,
  TargetSchoolDto,
  TargetSchoolSummaryDto,
  UpdateStudyCenterRequest,
  SetStudyCenterActiveRequest,
  CenterReceivedLeadDto,
  CenterLeadsSummaryDto,
} from "@/types/api";

// Browser code calls same-origin /api (Nginx in prod, Next rewrite in dev).
// Server components run inside the container network and call the API directly via API_INTERNAL_URL.
const baseUrl = typeof window === "undefined" ? (process.env.API_INTERNAL_URL ?? "http://localhost:5080") : "";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public body: unknown = null,
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
    let message = body?.detail ?? body?.title ?? body?.error ?? body?.message ?? res.statusText;
    if (body?.errors && typeof body.errors === "object") {
      const errorList = Object.entries(body.errors)
        .map(([field, msgs]) => `${field}: ${Array.isArray(msgs) ? msgs.join("; ") : msgs}`)
        .join(" | ");
      if (errorList) {
        message = `${message} (${errorList})`;
      }
    }
    throw new ApiError(res.status, message, body);
  }

  // Handle empty responses (e.g. 204 No Content)
  if (res.status === 204) {
    return null as unknown as T;
  }

  const text = await res.text();
  if (!text || text.trim() === "") {
    return null as unknown as T;
  }

  return JSON.parse(text) as T;
}

// [USAS-362] Bộ API Xác thực & Người dùng (Khách: Học sinh / Phụ huynh)
export const authApi = {
  // [AC-1] Đăng ký tài khoản (học sinh hoặc phụ huynh, email/SĐT, mật khẩu)
  register: (data: RegisterRequest) =>
    apiFetch<{ message: string }>("/api/v1/auth/register", {
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

  // [UC-06] Đổi mật khẩu khi đã đăng nhập: giữ phiên hiện tại, đăng xuất các thiết bị khác
  changePassword: (data: ChangePasswordRequest) =>
    apiFetch<{ message: string }>("/api/v1/auth/change-password", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // Sửa họ tên và số điện thoại của chính mình (email đăng nhập không đổi được)
  updateMe: (data: UpdateProfileRequest) =>
    apiFetch<{ message: string; user: UserDto }>("/api/v1/auth/me", {
      method: "PUT",
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
};

// [Q11] Liên kết phụ huynh – học sinh. Token lời mời đi trong body để không nằm trong log URL của API.
export const parentLinksApi = {
  lookupInvite: (token: string) =>
    apiFetch<ParentLinkResponse<ParentInviteInfo>>("/api/v1/parent-links/invite/lookup", {
      method: "POST",
      body: JSON.stringify({ token }),
    }),

  acceptInvite: (token: string) =>
    apiFetch<ParentLinkResponse<ParentLinkDto>>("/api/v1/parent-links/invite/accept", {
      method: "POST",
      body: JSON.stringify({ token }),
    }),

  resend: () =>
    apiFetch<ParentLinkResponse<ParentLinkDto>>("/api/v1/parent-links/resend", {
      method: "POST",
    }),

  changeParentEmail: (parentEmail: string) =>
    apiFetch<ParentLinkResponse<ParentLinkDto>>("/api/v1/parent-links/parent-email", {
      method: "PUT",
      body: JSON.stringify({ parentEmail }),
    }),

  revoke: (id: string) =>
    apiFetch<ParentLinkResponse<ParentLinkDto>>(`/api/v1/parent-links/${encodeURIComponent(id)}/revoke`, {
      method: "POST",
    }),

  getMine: () =>
    apiFetch<ParentLinkResponse<ParentLinkDto[]>>("/api/v1/parent-links/me", {
      method: "GET",
    }),
};

// [UC-63 / USAS-362] Quản trị người dùng — chỉ admin (HttpOnly cookie)
export const adminUsersApi = {
  /** [UC-60] Danh sách tài khoản — tìm, lọc, phân trang (trước đây trả UserDto[], giờ PagedResult). */
  list: (params?: AdminUserAccountListQuery) => {
    const qs = new URLSearchParams();
    if (params?.search?.trim()) qs.set("search", params.search.trim());
    if (params?.role) qs.set("role", params.role);
    if (params?.status) qs.set("status", params.status);
    if (params?.page != null) qs.set("page", String(params.page));
    if (params?.pageSize != null) qs.set("pageSize", String(params.pageSize));
    const suffix = qs.toString() ? `?${qs.toString()}` : "";
    return apiFetch<PagedResult<UserDto>>(`/api/v1/admin/users${suffix}`, {
      method: "GET",
    });
  },

  getById: (id: string) =>
    apiFetch<UserDto>(`/api/v1/admin/users/${encodeURIComponent(id)}`, {
      method: "GET",
    }),

  createModerator: (data: CreateModeratorAccountRequest) =>
    apiFetch<UserDto>("/api/v1/admin/users/moderators", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  /** [Q11 / UC-71 Phụ trợ / UC-64] Moderator/Admin tạo tài khoản người dùng cho Trung tâm tư vấn. */
  createCenter: (data: CreateCenterAccountRequest) =>
    apiFetch<UserDto>("/api/v1/study-centers/accounts", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  /** [UC-61] Khóa tài khoản (admin). */
  lock: (id: string, data: LockUserAccountRequest) =>
    apiFetch<UserDto>(`/api/v1/admin/users/${encodeURIComponent(id)}/lock`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  /** [UC-62] Mở khóa tài khoản (admin). */
  unlock: (id: string, data: UnlockUserAccountRequest) =>
    apiFetch<UserDto>(`/api/v1/admin/users/${encodeURIComponent(id)}/unlock`, {
      method: "POST",
      body: JSON.stringify(data),
    }),
};

// [UC-71] API Quản trị danh bạ trung tâm tư vấn (Moderator / Admin)
export const studyCentersApi = {
  create: (data: CreateStudyCenterRequest) =>
    apiFetch<StudyCenter>("/api/v1/study-centers", {
      method: "POST",
      body: JSON.stringify(data),
    }),
  /** [UC-64] Tạo tài khoản trung tâm tư vấn (vai trò role: center) */
  createAccount: (data: CreateCenterAccountRequest) =>
    apiFetch<UserDto>("/api/v1/study-centers/accounts", {
      method: "POST",
      body: JSON.stringify(data),
    }),
  search: (params?: { search?: string; city?: string; service?: string; studyLevel?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.search) sp.set("search", params.search);
    if (params?.city) sp.set("city", params.city);
    if (params?.service) sp.set("service", params.service);
    if (params?.studyLevel) sp.set("studyLevel", params.studyLevel);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<StudyCenter>>(`/api/v1/study-centers${qs ? `?${qs}` : ""}`);
  },
  getById: (id: string) => apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`),
  /** [UC-59] Xác minh hoặc hủy xác minh trung tâm tư vấn (vai trò: Moderator / Admin) */
  verify: (id: string, isVerified: boolean) =>
    apiFetch<StudyCenter>(`/api/v1/study-centers/${encodeURIComponent(id)}/verify`, {
      method: "PUT",
      body: JSON.stringify({ isVerified }),
    }),
  /** [UC-72] Sửa thông tin trung tâm tư vấn (vai trò: Moderator / Admin) */
  update: (id: string, data: UpdateStudyCenterRequest) =>
    apiFetch<StudyCenter>(`/api/v1/study-centers/${encodeURIComponent(id)}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),
  /** [UC-72] Ẩn hoặc hiện trung tâm tư vấn khỏi danh bạ và luồng gợi ý (vai trò: Moderator / Admin) */
  setActiveStatus: (id: string, isActive: boolean) => {
    const payload: SetStudyCenterActiveRequest = { isActive };
    return apiFetch<StudyCenter>(`/api/v1/study-centers/${encodeURIComponent(id)}/status`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },
};

/** [UC-73] Quản lý và xử lý yêu cầu liên hệ nhận được phía trung tâm tư vấn (Leads) */
export const centerLeadsApi = {
  list: (params?: {
    page?: number;
    pageSize?: number;
    status?: string;
    search?: string;
    studyCenterId?: string;
  }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.status && params.status !== "all") sp.set("status", params.status);
    if (params?.search) sp.set("search", params.search);
    if (params?.studyCenterId) sp.set("studyCenterId", params.studyCenterId);
    const qs = sp.toString();
    return apiFetch<CenterLeadsSummaryDto>(`/api/v1/leads/received${qs ? `?${qs}` : ""}`);
  },

  getById: (id: string) =>
    apiFetch<CenterReceivedLeadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}`),

  updateStatus: (id: string, status: string) =>
    apiFetch<CenterReceivedLeadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status }),
    }),
};

// [UC-30, UC-31, UC-33] API Quản lý trường mục tiêu (Student / Parent)
export const targetSchoolsApi = {
  /** [UC-33] Xem danh sách các trường mục tiêu kèm tóm tắt phân nhóm và hạn nộp */
  list: (params?: { category?: string; status?: string }) => {
    const q = new URLSearchParams();
    if (params?.category) q.set("category", params.category);
    if (params?.status) q.set("status", params.status);
    const qs = q.toString();
    return apiFetch<TargetSchoolSummaryDto>(`/api/v1/target-schools${qs ? `?${qs}` : ""}`);
  },
  /** [UC-30] Thêm một trường vào danh sách mục tiêu */
  add: (data: AddTargetSchoolRequest) =>
    apiFetch<TargetSchoolDto>("/api/v1/target-schools", {
      method: "POST",
      body: JSON.stringify(data),
    }),
  /** Lấy chi tiết một trường mục tiêu theo ID */
  getById: (id: string) =>
    apiFetch<TargetSchoolDto>(`/api/v1/target-schools/${encodeURIComponent(id)}`),
  /** [UC-32] Chọn hoặc cập nhật vòng nộp hồ sơ và hạn nộp */
  updateRound: (id: string, data: { applicationRound: string; deadline?: string | null; autoResolveDeadline?: boolean }) =>
    apiFetch<TargetSchoolDto>(`/api/v1/target-schools/${encodeURIComponent(id)}/round`, {
      method: "PATCH",
      body: JSON.stringify(data),
    }),
  /** [UC-31] Xóa một trường khỏi danh sách mục tiêu theo ID mục tiêu */
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/target-schools/${encodeURIComponent(id)}`, {
      method: "DELETE",
    }),
  /** [UC-31] Xóa một trường khỏi danh sách mục tiêu theo Mã trường hoặc University ID */
  removeByUniversity: (universityIdentifier: string) =>
    apiFetch<void>(`/api/v1/target-schools/by-university/${encodeURIComponent(universityIdentifier)}`, {
      method: "DELETE",
    }),
  /** Kiểm tra trạng thái trường mục tiêu của học sinh */
  check: (params: { universityCode?: string; universityId?: string }) => {
    const q = new URLSearchParams();
    if (params.universityCode) q.set("universityCode", params.universityCode);
    if (params.universityId) q.set("universityId", params.universityId);
    return apiFetch<TargetSchoolDto | null>(`/api/v1/target-schools/check?${q.toString()}`);
  },
};
