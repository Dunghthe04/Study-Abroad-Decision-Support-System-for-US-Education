// Lịch sử chat của chính người dùng. Khớp các DTO của backend (Application/Advisor/History), JSON camelCase.

import type { ChatMedia, ChatWidget, Citation, QuickReply } from "@/types/api";

/** Một phiên trong danh sách lịch sử: chỉ tóm tắt, không có nội dung tin. */
export interface ChatSessionSummary {
  id: string;
  title: string;
  createdAt: string;
  /** Lần trao đổi gần nhất: danh sách xếp theo đây, mới nhất trước. */
  lastActivityAt: string;
  messageCount: number;
}

export interface ChatSessionPage {
  items: ChatSessionSummary[];
  /** Đưa vào lần gọi sau để lấy các phiên cũ hơn; null là đã hết. */
  nextCursor: string | null;
}

/** Một tin trong lịch sử: chỉ có chữ (nút, công cụ, thẻ số liệu không được lưu). system = lượt hệ thống không phản hồi. */
export interface ChatHistoryMessage {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: string;
  citations: Citation[];
  /** Ảnh minh họa của câu trả lời (link https), rỗng nếu không có. Tin cũ từ trước khi có ảnh cũng rỗng. */
  media?: ChatMedia[];
}

/** Các tin của một phiên, cũ trước mới sau. olderCursor: tải các tin cũ hơn nữa; null là đã tới tin đầu tiên. */
export interface ChatHistoryMessages {
  sessionId: string;
  title: string;
  items: ChatHistoryMessage[];
  olderCursor: string | null;
}

/** Câu hồ sơ đang chờ của phiên, kèm nút và công cụ dựng lại. pending = false là không có câu nào. */
export interface PendingQuestion {
  pending: boolean;
  questionId: string | null;
  question: string;
  quickReplies: QuickReply[];
  widget: ChatWidget | null;
}

/** Kết quả đổi tên phiên: tên đã chuẩn hóa mà hệ thống lưu. */
export interface RenamedChatSession {
  id: string;
  title: string;
}
