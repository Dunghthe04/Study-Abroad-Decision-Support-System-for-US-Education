import { apiFetch } from "@/lib/api";

/** [UC-110] Một thông báo trong ứng dụng. `linkUrl` là đường dẫn nội bộ tới màn hình liên quan (có thể trống). */
export interface NotificationDto {
  id: string;
  type: string;
  title: string;
  body?: string | null;
  linkUrl?: string | null;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
}

/** [UC-110] Một trang thông báo của người dùng hiện tại; `unreadCount` tính trên toàn bộ thông báo. */
export interface NotificationListDto {
  items: NotificationDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  unreadCount: number;
}

/** [UC-110] Thông báo của chính người đang đăng nhập (backend lấy người dùng từ phiên, không nhận id từ client). */
export const notificationsApi = {
  list: (params?: { unreadOnly?: boolean; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.unreadOnly) sp.set("unreadOnly", "true");
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<NotificationListDto>(`/api/v1/notifications${qs ? `?${qs}` : ""}`);
  },

  unreadCount: () => apiFetch<{ unreadCount: number }>("/api/v1/notifications/unread-count"),

  markRead: (id: string) =>
    apiFetch<NotificationDto>(`/api/v1/notifications/${encodeURIComponent(id)}/read`, { method: "POST" }),

  markAllRead: () => apiFetch<{ updated: number }>("/api/v1/notifications/read-all", { method: "POST" }),
};

/** Sự kiện phát khi số thông báo chưa đọc thay đổi, để chuông trên thanh điều hướng cập nhật ngay. */
export const NOTIFICATIONS_CHANGED_EVENT = "usas:notifications-changed";

export function announceNotificationsChanged() {
  if (typeof window !== "undefined") window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED_EVENT));
}

/** Chỉ mở đường dẫn nội bộ; mọi giá trị khác bị bỏ qua. */
export function safeNotificationLink(linkUrl?: string | null): string | null {
  if (!linkUrl) return null;
  return linkUrl.startsWith("/") && !linkUrl.startsWith("//") && !linkUrl.includes("\\") ? linkUrl : null;
}
