// Kiểu dữ liệu của API admin: đợt duyệt mẫu (backend: AdminReviewBatchesController, StudyAbroad.Application.Admin.ReviewBatches).
// Danh sách đợt và danh sách câu KHÔNG có nội dung chat. Nội dung chỉ có ở chi tiết một câu, chỉ người được giao duyệt đợt mới xem được,
// và mỗi lần xem được ghi nhật ký.

import type { ChatReviewMessage, ChatReviewQuestion } from "@/types/chat-review";

/** Người duyệt có đồng ý với nhãn ý SLM gán cho câu hỏi không. */
export type ReviewLabelVerdict = "correct" | "wrong";

/** Đánh giá câu trả lời của hệ thống. */
export type ReviewAnswerVerdict = "correct" | "wrong" | "missing_data" | "out_of_scope";

/** Vì sao câu trả lời sai (chỉ khi câu trả lời sai). */
export type ReviewCause = "misread_intent" | "missing_knowledge" | "hallucination" | "technical";

/** Sáu loại ý SLM gán cho câu hỏi. */
export type SlmIntentType =
  | "collect_profile"
  | "profile_review"
  | "scenario"
  | "knowledge"
  | "center_referral"
  | "out_of_scope";

/** Một nhóm lúc bốc mẫu: có bao nhiêu câu và chọn được bao nhiêu. Cần để tính tỷ lệ chung theo trọng số. */
export interface StratumCount {
  stratum: string;
  population: number;
  picked: number;
}

/**
 * Tạo đợt. sizePerStratum là số câu muốn bốc cho MỖI nhóm skill (1 đến 100). seed bỏ trống thì hệ thống tự sinh (và lưu lại).
 * from, to là ngày UTC dạng yyyy-MM-dd, tính cả hai đầu, bỏ trống là không giới hạn.
 * reviewerIds bỏ trống thì người tạo đợt tự duyệt.
 */
export interface CreateReviewBatchRequest {
  name: string;
  sizePerStratum: number;
  from?: string | null;
  to?: string | null;
  reviewerIds?: string[] | null;
  seed?: string | null;
}

export interface ReviewBatchCreated {
  id: string;
  name: string;
  seed: string;
  sizePerStratum: number;
  sampledFromUtc: string | null;
  sampledToUtcExclusive: string | null;
  totalItems: number;
  strata: StratumCount[];
  reviewerIds: string[];
  createdAt: string;
}

/** Tiến độ của một người duyệt: đã chấm bao nhiêu câu (không có kết luận cụ thể). */
export interface ReviewerProgress {
  userId: string;
  fullName: string;
  graded: number;
}

/** Một đợt trong danh sách của người dùng hiện tại. isReviewer cho biết có được xem nội dung các câu không. */
export interface ReviewBatchListItem {
  id: string;
  name: string;
  seed: string;
  createdAt: string;
  sizePerStratum: number;
  sampledFromUtc: string | null;
  sampledToUtcExclusive: string | null;
  totalItems: number;
  strata: StratumCount[];
  isCreator: boolean;
  isReviewer: boolean;
  /** Số câu CHÍNH người xem đã chấm. */
  myGraded: number;
  reviewers: ReviewerProgress[];
}

/** Một câu trong danh sách của đợt, chưa có nội dung. gradedByMe chỉ nói chính người xem đã chấm chưa. */
export interface ReviewItemRow {
  itemId: string;
  position: number;
  stratum: string;
  gradedByMe: boolean;
}

export interface ReviewItemList {
  batchId: string;
  batchName: string;
  totalItems: number;
  myGraded: number;
  items: ReviewItemRow[];
}

/** Kết luận của chính người xem về một câu. */
export interface ReviewVerdict {
  labelVerdict: ReviewLabelVerdict;
  correctIntent: SlmIntentType | null;
  answerVerdict: ReviewAnswerVerdict;
  cause: ReviewCause | null;
  updatedAt: string;
}

/**
 * Một câu để chấm: CÓ nội dung chat (dữ liệu cá nhân). Không có mã học sinh hay mã phiên.
 * question null khi không có tin hỏi đi kèm. myVerdict null nếu người xem chưa chấm câu này.
 */
export interface ReviewItemDetail {
  itemId: string;
  position: number;
  stratum: string;
  question: ChatReviewQuestion | null;
  reply: ChatReviewMessage;
  /** Vài tin trước câu hỏi, theo thứ tự thời gian. */
  context: ChatReviewMessage[];
  myVerdict: ReviewVerdict | null;
}

export interface SaveReviewVerdictRequest {
  labelVerdict: ReviewLabelVerdict;
  /** Chỉ khi labelVerdict là "wrong"; bỏ trống nếu người duyệt không chắc nhãn đúng là gì. */
  correctIntent?: SlmIntentType | null;
  answerVerdict: ReviewAnswerVerdict;
  /** Bắt buộc khi answerVerdict là "wrong", và chỉ khi đó. */
  cause?: ReviewCause | null;
}
