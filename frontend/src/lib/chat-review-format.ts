// Dịch mã của hàng đợi câu cần xem thành chữ và dựng "vì sao câu này vào hàng đợi". Toàn hàm thuần, không đụng React.
// Lý do KHÔNG lưu trong database: suy ra từ các mã cố định đã có (kết quả, loại ngoài phạm vi, cờ dự phòng, bước lỗi),
// nên không cần mở nội dung chat để biết.

import { purposeLabel } from "@/lib/ai-usage-format";
import type { ChatReviewItem, ChatReviewModelInfo, ChatReviewOutcome, ChatReviewStatus } from "@/types/chat-review";

/** Phần của nhãn model cần để suy ra lý do: dùng được cho cả dòng danh sách lẫn chi tiết một câu. */
export type ReasonModel = Pick<ChatReviewModelInfo, "confidence" | "fallback" | "outOfScope">;

type ReasonInput = Pick<ChatReviewItem, "role" | "outcome" | "failedSteps" | "error"> & { model: ReasonModel | null };

export const STATUS_LABELS: Record<ChatReviewStatus, string> = {
  open: "Đang mở",
  resolved: "Đã xử lý",
  ignored: "Bỏ qua",
};

export const OUTCOME_LABELS: Record<ChatReviewOutcome, string> = {
  no_answer: "Không trả lời được",
  failed: "Lỗi",
};

export const statusLabel = (status: string) => STATUS_LABELS[status as ChatReviewStatus] ?? status;
export const outcomeLabel = (outcome: string) => OUTCOME_LABELS[outcome as ChatReviewOutcome] ?? outcome;

// Loại con của ý ngoài phạm vi (out_of_scope). Chỉ ambiguous và exception đưa câu vào hàng đợi; các loại khác dùng khi xem chi tiết.
const OUT_OF_SCOPE_LABELS: Record<string, string> = {
  ambiguous: "Câu mơ hồ",
  exception: "Ngoại lệ",
  sensitive: "Nội dung nhạy cảm",
  farewell: "Chào tạm biệt",
  about_system: "Hỏi về hệ thống",
  human_handoff: "Muốn gặp người thật",
};

// Sáu loại ý SLM gán cho câu hỏi
const INTENT_LABELS: Record<string, string> = {
  collect_profile: "Nhập thông tin hồ sơ",
  profile_review: "Đánh giá hồ sơ",
  scenario: "Kịch bản what-if",
  knowledge: "Hỏi kiến thức",
  center_referral: "Chuyển trung tâm tư vấn",
  out_of_scope: "Ngoài phạm vi",
};

export const outOfScopeLabel = (subtype: string) => OUT_OF_SCOPE_LABELS[subtype] ?? subtype;
export const intentLabel = (type: string) => INTENT_LABELS[type] ?? type;

/** Bước lỗi là tên skill (đã có nhãn ở trang token) hoặc tên nguồn khi không có skill; tên lạ giữ nguyên để không mất thông tin. */
export function failedStepLabel(step: string): string {
  if (step === "advisor") return "Advisor";
  return purposeLabel(step);
}

export interface QueueReason {
  /** Một dòng ngắn cho cột "Vì sao vào hàng đợi". */
  label: string;
  /** Chi tiết phụ (tên bước lỗi, độ tin cậy...), có thể rỗng. */
  detail: string | null;
}

/** 0.4 → "40%"; không có số thì null. */
export function formatConfidence(confidence: number | null | undefined): string | null {
  if (confidence == null || !Number.isFinite(confidence)) return null;
  return `${Math.round(confidence * 100)}%`;
}

/**
 * Vì sao câu này vào hàng đợi. Thứ tự ưu tiên đi từ nguyên nhân gốc đến hệ quả:
 * advisor sập → bước AI lỗi → SLM hỏng (dự phòng) → loại ngoài phạm vi → không rõ.
 * SLM hỏng phải xét trước loại ngoài phạm vi vì kết quả dự phòng luôn mang loại "ambiguous"; hiện "câu mơ hồ" sẽ sai nguyên nhân.
 */
export function queueReason(item: ReasonInput): QueueReason {
  if (item.role === "system") {
    return { label: "Advisor không phản hồi", detail: item.error };
  }

  if (item.outcome === "failed") {
    const steps = item.failedSteps.map(failedStepLabel).join(", ");
    return { label: "Bước AI bị lỗi", detail: steps || item.error };
  }

  const model = item.model;
  if (model?.fallback) {
    return { label: "Model không phân loại được (dùng dự phòng)", detail: null };
  }

  const confidence = formatConfidence(model?.confidence);
  const detail = confidence ? `Độ tin cậy ${confidence}` : null;
  if (model?.outOfScope) {
    return { label: outOfScopeLabel(model.outOfScope), detail };
  }

  return { label: "Hệ thống không trả lời được", detail };
}

/** Tên cho bộ lọc theo lý do: cùng cách gom với queueReason, để đếm và lọc không lệch nhau. */
export type ReasonKey = "advisor_down" | "step_failed" | "fallback" | "ambiguous" | "exception" | "unknown";

export function reasonKey(item: Pick<ReasonInput, "role" | "outcome" | "model">): ReasonKey {
  if (item.role === "system") return "advisor_down";
  if (item.outcome === "failed") return "step_failed";
  if (item.model?.fallback) return "fallback";
  if (item.model?.outOfScope === "ambiguous") return "ambiguous";
  if (item.model?.outOfScope === "exception") return "exception";
  return "unknown";
}
