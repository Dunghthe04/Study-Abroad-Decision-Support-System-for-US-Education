import { apiFetch } from "@/lib/api";
import type { PagedResult } from "@/types/api";

/** [UC-114] Một thay đổi website / fanpage của trung tâm chờ hoặc đã được Moderator kiểm tra. */
export interface CenterProfileChangeDto {
  id: string;
  studyCenterId: string;
  studyCenterName: string;
  field: "website" | "fanpage" | string;
  oldValue?: string | null;
  newValue?: string | null;
  status: "pending" | "passed" | "failed" | "withdrawn" | string;
  createdAt: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  reviewReason?: string | null;
}

export type CenterProfileChangeFilter = "pending" | "passed" | "failed" | "all";

/** [UC-114] Moderator kiểm tra thay đổi website / fanpage do trung tâm tự sửa (UC-92). */
export const centerProfileChangesApi = {
  list: (params?: { status?: CenterProfileChangeFilter; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<CenterProfileChangeDto>>(`/api/v1/admin/center-profile-changes${qs ? `?${qs}` : ""}`);
  },

  /** Đạt hoặc Không đạt. Không đạt bắt buộc có lý do. */
  review: (id: string, passed: boolean, reason?: string) =>
    apiFetch<CenterProfileChangeDto>(`/api/v1/admin/center-profile-changes/${encodeURIComponent(id)}/review`, {
      method: "POST",
      body: JSON.stringify({ passed, reason: reason?.trim() || null }),
    }),
};

/** [UC-92, UC-93] Một yêu cầu xóa tài khoản và dữ liệu trung tâm. */
export interface CenterDeletionRequestDto {
  id: string;
  studyCenterId?: string | null;
  centerName: string;
  contactEmail?: string | null;
  reason?: string | null;
  status: "pending" | "completed" | "rejected" | "cancelled" | string;
  createdAt: string;
  /** Hạn xử lý đã công bố. */
  dueAt: string;
  identityVerified: boolean;
  processedByUserId?: string | null;
  processedAt?: string | null;
  adminNote?: string | null;
}

export type CenterDeletionFilter = "pending" | "completed" | "rejected" | "cancelled" | "all";

/** [UC-92] Trung tâm gửi, xem và rút yêu cầu xóa tài khoản của chính mình. */
export const centerDeletionApi = {
  /** Yêu cầu gần nhất, null nếu chưa từng gửi. */
  getMine: () => apiFetch<CenterDeletionRequestDto | null>("/api/v1/center/deletion-request"),

  create: (reason?: string) =>
    apiFetch<CenterDeletionRequestDto>("/api/v1/center/deletion-request", {
      method: "POST",
      body: JSON.stringify({ reason: reason?.trim() || null }),
    }),

  cancel: () => apiFetch<CenterDeletionRequestDto>("/api/v1/center/deletion-request", { method: "DELETE" }),
};

/** [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm. */
export const adminCenterDeletionApi = {
  list: (params?: { status?: CenterDeletionFilter; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<CenterDeletionRequestDto>>(`/api/v1/admin/center-deletion-requests${qs ? `?${qs}` : ""}`);
  },

  /** Thực hiện xóa. Không hoàn tác được; phải xác nhận đã kiểm tra danh tính. */
  complete: (id: string, identityVerified: boolean, note?: string) =>
    apiFetch<CenterDeletionRequestDto>(`/api/v1/admin/center-deletion-requests/${encodeURIComponent(id)}/complete`, {
      method: "POST",
      body: JSON.stringify({ identityVerified, note: note?.trim() || null }),
    }),

  reject: (id: string, reason: string) =>
    apiFetch<CenterDeletionRequestDto>(`/api/v1/admin/center-deletion-requests/${encodeURIComponent(id)}/reject`, {
      method: "POST",
      body: JSON.stringify({ reason: reason.trim() }),
    }),
};
