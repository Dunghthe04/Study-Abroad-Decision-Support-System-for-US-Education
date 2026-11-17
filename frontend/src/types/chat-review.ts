// Kiểu dữ liệu tin hỏi, tin nhắn và nhãn model gán, dùng ở chi tiết đợt duyệt mẫu.

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
