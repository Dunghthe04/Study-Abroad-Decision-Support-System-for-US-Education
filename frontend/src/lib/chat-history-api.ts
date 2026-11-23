import { apiFetch } from "@/lib/api";
import type { ChatHistoryMessages, ChatSessionPage, PendingQuestion, RenamedChatSession } from "@/types/chat-history";

const base = "/api/v1/advisor/sessions";

function query(params: Record<string, string | number | null | undefined>): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) if (value !== null && value !== undefined && value !== "") qs.set(key, String(value));
  const text = qs.toString();
  return text ? `?${text}` : "";
}

// Lịch sử chat của chính người đang đăng nhập (backend chỉ trả phiên của người đó; phiên người khác là 404).
export const chatHistoryApi = {
  /** Phiên mới trao đổi nhất trước; cursor là nextCursor của lần trước. */
  sessions: (cursor?: string | null, limit = 20) => apiFetch<ChatSessionPage>(`${base}${query({ cursor, limit })}`),

  /** Các tin của một phiên, cũ trước mới sau: mặc định là các tin mới nhất; before là olderCursor để tải tin cũ hơn. */
  messages: (sessionId: string, before?: string | null, limit = 100) =>
    apiFetch<ChatHistoryMessages>(`${base}/${encodeURIComponent(sessionId)}/messages${query({ before, limit })}`),

  /** Câu hồ sơ đang chờ của phiên kèm nút và công cụ dựng lại (chỉ đọc, không tính lượt). */
  pending: (sessionId: string) => apiFetch<PendingQuestion>(`${base}/${encodeURIComponent(sessionId)}/pending`),

  /** Đổi tên phiên (tối đa 100 ký tự, không để trống); trả tên đã chuẩn hóa mà hệ thống lưu. */
  rename: (sessionId: string, title: string) =>
    apiFetch<RenamedChatSession>(`${base}/${encodeURIComponent(sessionId)}`, { method: "PATCH", body: JSON.stringify({ title }) }),

  /** Xóa phiên khỏi lịch sử (xóa mềm): không hiện, không mở lại, không hỏi tiếp được nữa. */
  remove: (sessionId: string) => apiFetch<void>(`${base}/${encodeURIComponent(sessionId)}`, { method: "DELETE" }),
};

/** "vừa xong", "5 phút", "3 giờ", "2 ngày", quá 30 ngày thì ngày đầy đủ. now truyền vào để test được. */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const then = new Date(iso);
  if (Number.isNaN(then.getTime())) return "";
  const minutes = Math.floor((now.getTime() - then.getTime()) / 60_000);
  if (minutes < 1) return "vừa xong";
  if (minutes < 60) return `${minutes} phút`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days} ngày`;
  return then.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
}

/** Bỏ dấu và chữ hoa để tìm "hoc phi" vẫn ra "Học phí". */
export function searchKey(text: string): string {
  return text.normalize("NFD").replace(/\p{Diacritic}/gu, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
}
