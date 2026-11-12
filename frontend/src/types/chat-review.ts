// Kiểu dữ liệu của API admin: hàng đợi câu cần xem (backend: AdminChatReviewController, StudyAbroad.Application.Admin.ChatReview).
// Danh sách chỉ có mã cố định và số đo, KHÔNG có nội dung chat. Nội dung chỉ có ở chi tiết một câu (mỗi lần xem được ghi nhật ký).

import type { PagedResult } from "@/types/api";

export type { PagedResult };

/** Trạng thái xử lý (cột chat_messages.review_status). */
export type ChatReviewStatus = "open" | "resolved" | "ignored";

/** Kết quả đáng đưa vào hàng đợi (câu trả lời được là "answered" thì không có trong hàng đợi). */
export type ChatReviewOutcome = "no_answer" | "failed";

/** Tin trong hàng đợi: trả lời của trợ lý, hoặc dòng báo lỗi khi advisor sập. */
export type ChatReviewRole = "assistant" | "system";

/**
 * Model hiểu câu hỏi ra sao, chỉ gồm mã cố định (không có đoạn chữ của học sinh).
 * null ở cấp trên khi lượt không qua SLM (ví dụ advisor sập hoặc học sinh bấm nút).
 */
export interface ChatReviewModelInfo {
  confidence: number | null;
  /** SLM hỏng hai lần nên hệ thống dùng kết quả dự phòng. */
  fallback: boolean;
  modelIntentTypes: string[];
  intentTypes: string[];
  /** Loại con của ý ngoài phạm vi: exception, sensitive, farewell, ambiguous, about_system, human_handoff. */
  outOfScope: string | null;
}

export interface ChatReviewItem {
  messageId: string;
  sessionId: string;
  userId: string;
  createdAt: string;
  role: ChatReviewRole;
  outcome: ChatReviewOutcome;
  reviewStatus: ChatReviewStatus;
  /** Tin của học sinh mà dòng này trả lời. */
  questionMessageId: string | null;
  model: ChatReviewModelInfo | null;
  /** Các bước AI hỏng hẳn (tên skill, hoặc nguồn khi không có skill). */
  failedSteps: string[];
  turnMs: number | null;
  /** Tên loại lỗi (không phải thông báo lỗi). */
  error: string | null;
}

/** Số câu theo trạng thái trong khoảng; open chia theo kết quả. Cho các thẻ đầu trang. */
export interface ChatReviewSummary {
  from: string | null;
  to: string | null;
  open: number;
  resolved: number;
  ignored: number;
  openNoAnswer: number;
  openFailed: number;
}

// ----------------------------------------------------------------- chi tiết một câu: CÓ nội dung chat

/** Một ý model đọc ra, gồm cả đoạn chữ và thực thể học sinh viết. */
export interface ChatReviewModelIntent {
  type: string;
  span: string;
  entities: Record<string, unknown> | null;
}

export interface ChatReviewModelDetail {
  confidence: number | null;
  fallback: boolean;
  outOfScope: string | null;
  intentTypes: string[];
  modelIntents: ChatReviewModelIntent[];
}

export interface ChatReviewMessage {
  messageId: string;
  role: string;
  createdAt: string;
  content: string;
}

export interface ChatReviewQuestion {
  messageId: string;
  createdAt: string;
  content: string;
  model: ChatReviewModelDetail | null;
}

export interface ChatReviewDetail {
  messageId: string;
  sessionId: string;
  userId: string;
  createdAt: string;
  role: ChatReviewRole;
  outcome: ChatReviewOutcome;
  reviewStatus: ChatReviewStatus;
  failedSteps: string[];
  turnMs: number | null;
  error: string | null;
  /** null khi không có tin hỏi (ví dụ dòng báo lỗi khi advisor sập). */
  question: ChatReviewQuestion | null;
  reply: ChatReviewMessage;
  /** Vài tin trước câu hỏi, theo thứ tự thời gian. */
  context: ChatReviewMessage[];
}

/** Kết quả đổi trạng thái: reviewStatus === previousStatus nghĩa là không có gì đổi. */
export interface ChatReviewStatusResult {
  messageId: string;
  reviewStatus: ChatReviewStatus;
  previousStatus: ChatReviewStatus;
}

export type ChatReviewPage = PagedResult<ChatReviewItem>;
