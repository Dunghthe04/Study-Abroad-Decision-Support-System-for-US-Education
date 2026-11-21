// 1.5b: phụ huynh làm thay con managed (con chưa có tài khoản).
// Khi đang làm thay, cookie phiên là của con nên chỉ API hồ sơ chạy dưới tên con; mọi API khác (kể cả /auth/me) vẫn là phụ huynh.
// Muốn biết đang làm thay ai thì gọi getActing.

import { apiFetch } from "@/lib/api";

export interface ActingState {
  acting: boolean;
  childId?: string;
  childName?: string;
}

export const actingApi = {
  // Vào làm thay: server đổi cookie phiên sang con, cất phiên phụ huynh để thoát thì lấy lại. 404 nếu không phải con managed của mình.
  start: (childId: string) =>
    apiFetch<{ expiresAt: string }>(
      `/api/v1/auth/acting/${encodeURIComponent(childId)}`,
      { method: "POST" },
    ),

  // Thoát làm thay: trả lại phiên phụ huynh. 400 nếu đang không làm thay.
  stop: () =>
    apiFetch<{ expiresAt: string }>("/api/v1/auth/acting", {
      method: "DELETE",
    }),

  get: () => apiFetch<ActingState>("/api/v1/auth/acting", { method: "GET" }),
};
