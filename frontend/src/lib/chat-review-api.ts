import { apiFetch } from "@/lib/api";
import type {
  ChatReviewDetail,
  ChatReviewOutcome,
  ChatReviewPage,
  ChatReviewStatus,
  ChatReviewStatusResult,
  ChatReviewSummary,
} from "@/types/chat-review";

const base = "/api/v1/admin/chat-review";

export interface ChatReviewFilters {
  /** Bỏ trống là "open" (backend mặc định). */
  status?: ChatReviewStatus;
  /** Bỏ trống là cả no_answer lẫn failed. */
  outcome?: ChatReviewOutcome;
  /** yyyy-MM-dd (UTC), tính cả hai đầu; bỏ trống là không giới hạn. */
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

function query(params: Record<string, string | number | undefined>): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") qs.set(key, String(value));
  }
  const text = qs.toString();
  return text ? `?${text}` : "";
}

// Hàng đợi câu cần xem cho admin. Danh sách và thống kê không có nội dung chat.
export const adminChatReviewApi = {
  queue: (filters: ChatReviewFilters = {}) =>
    apiFetch<ChatReviewPage>(`${base}/queue${query({ ...filters })}`),

  summary: (range: { from?: string; to?: string } = {}) =>
    apiFetch<ChatReviewSummary>(`${base}/summary${query(range)}`),

  /**
   * Chi tiết một câu, CÓ nội dung chat. Mỗi lần gọi được ghi nhật ký người xem, nên chỉ gọi khi admin chủ động mở:
   * không tải trước, không gọi lại khi không cần.
   */
  detail: (messageId: string) => apiFetch<ChatReviewDetail>(`${base}/items/${encodeURIComponent(messageId)}`),

  /** Đổi trạng thái xử lý; đặt lại đúng trạng thái đang có vẫn thành công. */
  setStatus: (messageId: string, status: ChatReviewStatus) =>
    apiFetch<ChatReviewStatusResult>(`${base}/items/${encodeURIComponent(messageId)}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status }),
    }),
};
