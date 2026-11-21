import { authFetch } from "@/lib/auth-refresh";
import type {
  AccessLogDto,
  AccessLogFilterQuery,
  AccessLogSummaryDto,
  CenterReviewsDto,
  NotificationDto,
  UnreadCountDto,
  CenterReviewDto,
  CenterReviewEligibilityDto,
  CreateCenterReviewRequest,
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResendOtpRequest,
  ResetPasswordRequest,
  RestoreAccountRequest,
  UpdateProfileRequest,
  UserDto,
  AdminUserAccountListQuery,
  PagedResult,
  CreateModeratorAccountRequest,
  CreateCenterAccountRequest,
  LockUserAccountRequest,
  UnlockUserAccountRequest,
  UpdateUserBirthDateRequest,
  VerifyEmailRequest,
  AcademicProfileResponse,
  ParentInviteInfo,
  ParentRevokeInfo,
  ParentLinkDto,
  ParentLinkResponse,
  ParentProfileOverviewDto,
  MatchManagedChildResult,
  ParentProfileOfferDto,
  StudyCenter,
  CreateStudyCenterRequest,
  AddTargetSchoolRequest,
  TargetSchoolDto,
  TargetSchoolSummaryDto,
  SetStudyCenterActiveRequest,
  CenterReceivedLeadDto,
  CenterLeadsSummaryDto,
  LeadMessageDto,
  LeadMessagesThreadDto,
  AdminLeadMessageReportContextDto,
  AdminLeadMessageReportDto,
  LeadMessageReportFilter,
  CenterProfileChangeDto,
  CenterProfileChangeFilter,
  SendLeadMessageRequest,
  SendLeadMessageResultDto,
  RegisterCenterRequest,
  VerifyCenterEmailRequest,
  VerificationDocumentUploadDto,
  SubmitVerificationDto,
  CenterVerificationDto,
  CenterVerificationStatusDto,
  ReviewCenterVerificationRequest,
  UpdateCenterProfileRequest,
  CenterProfileUpdateResponse,
  AdminCenterDeletionRequestDto,
  CenterDeletionRequestDto,
  CenterDeletionRequestStatus,
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

// Câu báo lỗi dự phòng khi server không kèm lời giải thích riêng (server đã điền sẵn, đây chỉ là lưới an toàn)
const DEFAULT_ERROR_MESSAGES: Record<number, string> = {
  400: "Thông tin gửi lên chưa hợp lệ. Bạn kiểm tra lại rồi thử lại nhé.",
  401: "Phiên đăng nhập đã hết hạn. Bạn đăng nhập lại rồi thử lại nhé.",
  403: "Tài khoản của bạn không được dùng chức năng này. Nếu vừa đổi tài khoản, hãy đăng xuất rồi đăng nhập lại.",
  404: "Không tìm thấy dữ liệu bạn yêu cầu.",
  429: "Bạn thao tác hơi nhanh. Đợi một chút rồi thử lại nhé.",
};

/** Form multipart cho tin nhắn kèm ảnh: field `file` và `content` (tùy chọn). */
export function buildImageForm(file: File, content?: string): FormData {
  const form = new FormData();
  form.append("file", file);
  if (content?.trim()) form.append("content", content.trim());
  return form;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await authFetch(`${baseUrl}${path}`, {
    ...init,
    // [Bảo mật HttpOnly Cookie]: credentials: "include" tự động gửi và nhận cookie an toàn
    credentials: "include",
    // FormData: để trình duyệt tự đặt Content-Type kèm boundary
    headers: init?.body instanceof FormData ? { ...init?.headers } : { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const fallback =
      DEFAULT_ERROR_MESSAGES[res.status] ??
      (res.status >= 500 ? "Máy chủ đang gặp sự cố. Bạn thử lại sau ít phút nhé." : "Đã có lỗi xảy ra. Bạn thử lại nhé.");
    // title mặc định của ASP.NET ("Forbidden", "Not Found"...) luôn có type trỏ về RFC → không hiện cho người dùng
    const isDefaultTitle = typeof body?.type === "string" && body.type.includes("rfc");
    let message = body?.detail ?? (isDefaultTitle ? null : body?.title) ?? body?.error ?? body?.message ?? fallback;
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

  // [UC-64] Trung tâm tự đăng ký tài khoản
  registerCenter: (data: RegisterCenterRequest) =>
    apiFetch<{ message: string; email: string }>("/api/v1/auth/register-center", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // [UC-91] Xác thực email trung tâm qua mã OTP 6 số
  verifyCenterEmail: (data: VerifyCenterEmailRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/verify-center-email", {
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

  // [UC-80] Tự xóa tài khoản (học sinh, phụ huynh): khóa ngay, xóa hẳn sau 30 ngày; server xóa luôn cookie phiên
  deleteMe: () =>
    apiFetch<{ message: string }>("/api/v1/auth/me", {
      method: "DELETE",
    }),

  // [UC-80] Khôi phục tài khoản đang chờ xóa bằng mã nhận lúc đăng nhập; thành công thì server đăng nhập luôn
  restore: (data: RestoreAccountRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/restore", {
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

  // Phụ huynh: hồ sơ các con đã liên kết (chỉ đọc) + con thật chờ ghép với con managed (1.5d)
  getChildrenProfiles: () =>
    apiFetch<ParentLinkResponse<ParentProfileOverviewDto>>("/api/v1/profile/academic/children", {
      method: "GET",
    }),

  // 1.5d: ghép con thật vừa liên kết với con managed đã nhập; managedChildId null = không phải ai trong số này
  matchManagedChild: (linkId: string, managedChildId: string | null) =>
    apiFetch<ParentLinkResponse<MatchManagedChildResult>>("/api/v1/profile/academic/match", {
      method: "POST",
      body: JSON.stringify({ linkId, managedChildId }),
    }),

  // Học sinh: đề nghị dùng hồ sơ phụ huynh khi mình đã có hồ sơ
  getParentOffers: () =>
    apiFetch<ParentLinkResponse<ParentProfileOfferDto[]>>("/api/v1/profile/academic/parent-offers", {
      method: "GET",
    }),

  decideParentOffer: (linkId: string, accept: boolean) =>
    apiFetch<ParentLinkResponse<ParentProfileOfferDto>>(
      `/api/v1/profile/academic/parent-offers/${encodeURIComponent(linkId)}/${accept ? "accept" : "decline"}`,
      { method: "POST" },
    ),
};

// [Q11] Liên kết phụ huynh – học sinh. Token lời mời đi trong body để không nằm trong log URL của API.
export const parentLinksApi = {
  lookupInvite: (token: string) =>
    apiFetch<ParentLinkResponse<ParentInviteInfo>>("/api/v1/parent-links/invite/lookup", {
      method: "POST",
      body: JSON.stringify({ token }),
    }),

  // [UC-115] Không cần đăng nhập: link một lần trong mail là xác thực
  acceptInvite: (token: string, isGuardian: boolean) =>
    apiFetch<ParentLinkResponse<ParentInviteInfo>>("/api/v1/parent-links/invite/accept", {
      method: "POST",
      body: JSON.stringify({ token, isGuardian }),
    }),

  rejectInvite: (token: string) =>
    apiFetch<ParentLinkResponse<ParentInviteInfo>>("/api/v1/parent-links/invite/reject", {
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

  // [UC-117] Phụ huynh rút đồng ý bằng link trong mail, không cần đăng nhập
  lookupRevoke: (token: string) =>
    apiFetch<ParentLinkResponse<ParentRevokeInfo>>("/api/v1/parent-links/revoke/lookup", {
      method: "POST",
      body: JSON.stringify({ token }),
    }),

  revokeByToken: (token: string) =>
    apiFetch<ParentLinkResponse<ParentRevokeInfo>>("/api/v1/parent-links/revoke", {
      method: "POST",
      body: JSON.stringify({ token }),
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

  /** [UC-113] Sửa ngày sinh Học sinh / Phụ huynh theo yêu cầu hỗ trợ (admin, bắt buộc lý do). */
  updateBirthDate: (id: string, data: UpdateUserBirthDateRequest) =>
    apiFetch<UserDto>(`/api/v1/admin/users/${encodeURIComponent(id)}/birth-date`, {
      method: "PUT",
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
  search: (params?: {
    search?: string;
    city?: string;
    service?: string;
    studyLevel?: string;
    verificationStatus?: string;
    centerType?: string;
    /** Chỉ Admin/Moderator: gồm cả trung tâm đã ẩn */
    includeInactive?: boolean;
    /** Chỉ Admin/Moderator: chỉ trung tâm đã ẩn */
    onlyInactive?: boolean;
    page?: number;
    pageSize?: number;
  }) => {
    const sp = new URLSearchParams();
    if (params?.verificationStatus) sp.set("verificationStatus", params.verificationStatus);
    if (params?.centerType) sp.set("centerType", params.centerType);
    if (params?.includeInactive) sp.set("includeInactive", "true");
    if (params?.onlyInactive) sp.set("onlyInactive", "true");
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
  /** Đánh giá thật của trung tâm (UC-41): một trang danh sách + điểm trung bình + tổng số lượng. */
  getReviews: (id: string, page = 1, pageSize = 20) =>
    apiFetch<CenterReviewsDto>(
      `/api/v1/study-centers/${encodeURIComponent(id)}/reviews?page=${page}&pageSize=${pageSize}`,
    ),
  /** [USAS-2079 / UC-57] Moderator gỡ một đánh giá vi phạm (lý do tùy chọn, tối đa 500 ký tự). Đánh giá biến khỏi danh sách và điểm trung bình. */
  removeReview: (centerId: string, reviewId: string, reason: string) =>
    apiFetch<{ reviewId: string; studyCenterId: string; status: string }>(
      `/api/v1/study-centers/${encodeURIComponent(centerId)}/reviews/${encodeURIComponent(reviewId)}/remove`,
      { method: "POST", body: JSON.stringify({ reason: reason.trim() }) },
    ),
  /** [UC-45] Người dùng hiện tại (học sinh / phụ huynh) có được đánh giá trung tâm này không. */
  getReviewEligibility: (id: string) =>
    apiFetch<CenterReviewEligibilityDto>(`/api/v1/study-centers/${encodeURIComponent(id)}/reviews/eligibility`),
  /** [UC-45] Gửi đánh giá 1 đến 5 sao. Lỗi 400: chưa gửi yêu cầu liên hệ, đã đánh giá, số sao hoặc nhận xét không hợp lệ. */
  createReview: (id: string, rating: number, comment?: string) => {
    const payload: CreateCenterReviewRequest = { rating, comment: comment?.trim() || null };
    return apiFetch<CenterReviewDto>(`/api/v1/study-centers/${encodeURIComponent(id)}/reviews`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },
  /** [UC-59] Xác minh hoặc hủy xác minh trung tâm tư vấn (vai trò: Moderator / Admin) */
  verify: (id: string, isVerified: boolean, verificationNote?: string | null) =>
    apiFetch<StudyCenter>(`/api/v1/study-centers/${encodeURIComponent(id)}/verify`, {
      method: "PUT",
      body: JSON.stringify(
        verificationNote !== undefined
          ? { isVerified, verificationNote }
          : { isVerified }
      ),
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

/** [UC-114] Moderator kiểm tra thay đổi website / fanpage do trung tâm tự sửa: Đạt hoặc Không đạt (lý do bắt buộc). */
export const centerProfileChangesApi = {
  list: (params?: { status?: CenterProfileChangeFilter; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<CenterProfileChangeDto>>(`/api/v1/moderator/center-profile-changes${qs ? `?${qs}` : ""}`);
  },

  /** `newValue` là giá trị mới Moderator đang xem; trung tâm sửa tiếp sau đó thì máy chủ từ chối. */
  pass: (id: string, newValue: string | null) =>
    apiFetch<CenterProfileChangeDto>(`/api/v1/moderator/center-profile-changes/${encodeURIComponent(id)}/pass`, {
      method: "POST",
      body: JSON.stringify({ newValue }),
    }),

  fail: (id: string, reason: string, newValue: string | null) =>
    apiFetch<CenterProfileChangeDto>(`/api/v1/moderator/center-profile-changes/${encodeURIComponent(id)}/fail`, {
      method: "POST",
      body: JSON.stringify({ reason, newValue }),
    }),
};

/** [UC-58] Admin xử lý tin nhắn bị báo cáo: bỏ qua báo cáo hoặc khóa tài khoản người gửi (gỡ luôn tin của họ). */
/** [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm. */
export const centerDeletionRequestsApi = {
  list: (params?: { status?: CenterDeletionRequestStatus | "all"; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<AdminCenterDeletionRequestDto>>(`/api/v1/admin/center-deletion-requests${qs ? `?${qs}` : ""}`);
  },

  approve: (id: string, identityCheckNote: string) =>
    apiFetch<AdminCenterDeletionRequestDto>(`/api/v1/admin/center-deletion-requests/${encodeURIComponent(id)}/approve`, {
      method: "POST",
      body: JSON.stringify({ identityCheckNote }),
    }),

  reject: (id: string, reason: string) =>
    apiFetch<AdminCenterDeletionRequestDto>(`/api/v1/admin/center-deletion-requests/${encodeURIComponent(id)}/reject`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),
};

export const leadMessageReportsApi = {
  list: (params?: { status?: LeadMessageReportFilter; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<AdminLeadMessageReportDto>>(`/api/v1/admin/lead-message-reports${qs ? `?${qs}` : ""}`);
  },

  dismiss: (leadId: string, messageId: string) =>
    apiFetch<{ message: string }>(
      `/api/v1/admin/lead-message-reports/${encodeURIComponent(leadId)}/${encodeURIComponent(messageId)}/dismiss`,
      { method: "POST" }
    ),

  /** Tin bị báo cáo kèm vài tin ngữ cảnh liền kề. Mỗi lần gọi được ghi vào nhật ký truy cập. */
  getContext: (leadId: string, messageId: string) =>
    apiFetch<AdminLeadMessageReportContextDto>(
      `/api/v1/admin/lead-message-reports/${encodeURIComponent(leadId)}/${encodeURIComponent(messageId)}/context`,
    ),

  /** Gỡ riêng tin nhắn vi phạm, không khóa tài khoản. */
  remove: (leadId: string, messageId: string, reason: string) =>
    apiFetch<{ message: string }>(
      `/api/v1/admin/lead-message-reports/${encodeURIComponent(leadId)}/${encodeURIComponent(messageId)}/remove`,
      { method: "POST", body: JSON.stringify({ reason: reason.trim() }) }
    ),

  /** Cảnh cáo người gửi, tin vẫn hiển thị. */
  warn: (leadId: string, messageId: string, reason: string) =>
    apiFetch<{ message: string }>(
      `/api/v1/admin/lead-message-reports/${encodeURIComponent(leadId)}/${encodeURIComponent(messageId)}/warn`,
      { method: "POST", body: JSON.stringify({ reason: reason.trim() }) }
    ),

  lockSender: (leadId: string, messageId: string, reason: string) =>
    apiFetch<{ message: string }>(
      `/api/v1/admin/lead-message-reports/${encodeURIComponent(leadId)}/${encodeURIComponent(messageId)}/lock-sender`,
      { method: "POST", body: JSON.stringify({ reason: reason.trim() }) }
    ),
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

  /** [UC-73] Chấp nhận yêu cầu Mới: sang Đã chấp nhận, mở nhắn tin. */
  accept: (id: string) =>
    apiFetch<CenterReceivedLeadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/accept`, { method: "POST" }),

  /** [UC-73] Từ chối yêu cầu Mới: sang Bị từ chối, người gửi nhận thông báo. */
  reject: (id: string) =>
    apiFetch<CenterReceivedLeadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/reject`, { method: "POST" }),

  updateStatus: (id: string, status: string) =>
    apiFetch<CenterReceivedLeadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status }),
    }),

  /** [USAS-2053 / UC-74] Đọc luồng tin nhắn trao đổi của một yêu cầu liên hệ */
  getMessages: (id: string) =>
    apiFetch<LeadMessagesThreadDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/messages`),

  /** [USAS-2053 / UC-74] Trung tâm gửi tin nhắn trả lời trong yêu cầu liên hệ */
  sendMessage: (id: string, content: string) => {
    const payload: SendLeadMessageRequest = { content };
    return apiFetch<SendLeadMessageResultDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/messages`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  /** [UC-58] Trung tâm báo cáo một tin của học sinh / phụ huynh; Admin sẽ nhận thông báo và xem xét. */
  reportMessage: (id: string, messageId: string, reason?: string) =>
    apiFetch<LeadMessageDto>(
      `/api/v1/leads/received/${encodeURIComponent(id)}/messages/${encodeURIComponent(messageId)}/report`,
      { method: "POST", body: JSON.stringify({ reason: reason?.trim() || null }) }
    ),

  /** Gửi tin kèm ảnh (jpg/png/webp, tối đa 5MB), `content` không bắt buộc. */
  sendImageMessage: (id: string, file: File, content?: string) =>
    apiFetch<SendLeadMessageResultDto>(`/api/v1/leads/received/${encodeURIComponent(id)}/messages/image`, {
      method: "POST",
      body: buildImageForm(file, content),
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

// [UC-71, UC-91, UC-92] Cổng Trung tâm tư vấn
export const centerPortalApi = {
  getProfile: () => apiFetch<StudyCenter>("/api/v1/center/profile"),
  /** [UC-92] Lưu hồ sơ; kết quả cho biết trường nào lưu ngay, trường nào chờ Moderator kiểm tra, có về Chờ duyệt không. */
  updateProfile: (data: UpdateCenterProfileRequest) =>
    apiFetch<CenterProfileUpdateResponse>("/api/v1/center/profile", {
      method: "PUT",
      body: JSON.stringify(data),
    }),
  /** [UC-92] Yêu cầu xóa tài khoản gần nhất (null nếu chưa gửi). */
  getDeletionRequest: () => apiFetch<CenterDeletionRequestDto | null>("/api/v1/center/deletion-request"),
  /** [UC-92] Gửi yêu cầu xóa tài khoản và dữ liệu trung tâm (Administrator xử lý ở UC-93). */
  requestDeletion: (reason: string) =>
    apiFetch<CenterDeletionRequestDto>("/api/v1/center/deletion-request", {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),
  getVerificationStatus: () =>
    apiFetch<CenterVerificationStatusDto>("/api/v1/center/verification/status"),
  /** [UC-91] Đổi số điện thoại liên lạc, lưu thẳng, không xác thực SMS OTP. */
  changePhone: (newValue: string) =>
    apiFetch<{ message: string }>("/api/v1/center/contact/phone", {
      method: "POST",
      body: JSON.stringify({ newValue }),
    }),
  /** Đổi email liên lạc: gửi OTP tới email mới rồi xác nhận bằng mã. */
  requestEmailChange: (newValue: string) =>
    apiFetch<{ message: string }>("/api/v1/center/contact/email/request", {
      method: "POST",
      body: JSON.stringify({ newValue }),
    }),
  confirmEmailChange: (newValue: string, code: string) =>
    apiFetch<{ message: string }>("/api/v1/center/contact/email/confirm", {
      method: "POST",
      body: JSON.stringify({ newValue, code }),
    }),
  uploadDocument: async (file: File, documentType: string): Promise<VerificationDocumentUploadDto> => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("documentType", documentType);

    const res = await authFetch(`${baseUrl}/api/v1/center/verification/upload-document`, {
      method: "POST",
      credentials: "include",
      body: formData,
    });
    if (!res.ok) {
      const err = await res.json().catch(() => null);
      throw new ApiError(res.status, err?.detail || err?.title || "Tải tài liệu thất bại", err);
    }
    return res.json();
  },
  submitVerification: (data: SubmitVerificationDto) =>
    apiFetch<StudyCenter>("/api/v1/center/verification", {
      method: "POST",
      body: JSON.stringify(data),
    }),
};

// [UC-59] Moderator duyệt yêu cầu xác minh pháp lý của trung tâm
export const adminCenterVerificationsApi = {
  list: (params?: { status?: string; search?: string; page?: number; pageSize?: number }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set("status", params.status);
    if (params?.search?.trim()) qs.set("search", params.search.trim());
    if (params?.page) qs.set("page", String(params.page));
    if (params?.pageSize) qs.set("pageSize", String(params.pageSize));
    const queryStr = qs.toString();
    return apiFetch<PagedResult<CenterVerificationDto>>(`/api/v1/admin/center-verifications${queryStr ? `?${queryStr}` : ""}`);
  },
  getById: (id: string) =>
    apiFetch<CenterVerificationDto>(`/api/v1/admin/center-verifications/${encodeURIComponent(id)}`),
  review: (id: string, data: ReviewCenterVerificationRequest) =>
    apiFetch<StudyCenter>(`/api/v1/admin/center-verifications/${encodeURIComponent(id)}/review`, {
      method: "POST",
      body: JSON.stringify(data),
    }),
};

// [UC-110] Thông báo của người đăng nhập
export const notificationsApi = {
  list: (params?: { page?: number; pageSize?: number; unreadOnly?: boolean }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.unreadOnly) sp.set("unreadOnly", "true");
    const qs = sp.toString();
    return apiFetch<PagedResult<NotificationDto>>(`/api/v1/notifications${qs ? `?${qs}` : ""}`);
  },
  unreadCount: () => apiFetch<UnreadCountDto>("/api/v1/notifications/unread-count"),
  markRead: (id: string) =>
    apiFetch<null>(`/api/v1/notifications/${encodeURIComponent(id)}/read`, { method: "POST" }),
  markAllRead: () => apiFetch<{ updated: number }>("/api/v1/notifications/read-all", { method: "POST" }),
};

// [UC-97] Admin xem và lọc nhật ký truy cập dữ liệu nhạy cảm
export const accessLogsApi = {
  list: (params?: AccessLogFilterQuery) => {
    const sp = new URLSearchParams();
    if (params?.category) sp.set("category", params.category);
    if (params?.action) sp.set("action", params.action);
    if (params?.search?.trim()) sp.set("search", params.search.trim());
    if (params?.actorUserId) sp.set("actorUserId", params.actorUserId);
    if (params?.actorRole) sp.set("actorRole", params.actorRole);
    if (params?.from) sp.set("from", params.from);
    if (params?.to) sp.set("to", params.to);
    if (params?.page != null) sp.set("page", String(params.page));
    if (params?.pageSize != null) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<AccessLogDto>>(`/api/v1/admin/access-logs${qs ? `?${qs}` : ""}`);
  },

  summary: (params?: { from?: string; to?: string }) => {
    const sp = new URLSearchParams();
    if (params?.from) sp.set("from", params.from);
    if (params?.to) sp.set("to", params.to);
    const qs = sp.toString();
    return apiFetch<AccessLogSummaryDto>(`/api/v1/admin/access-logs/summary${qs ? `?${qs}` : ""}`);
  },
};

// [UC-94] Dashboard KPI học sinh và phụ huynh
export { dashboardApi } from "@/lib/dashboard-api";

