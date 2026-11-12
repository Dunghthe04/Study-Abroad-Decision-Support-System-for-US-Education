// Nhãn chữ, tiến độ và quy tắc của biểu mẫu chấm cho đợt duyệt mẫu. Toàn hàm thuần, không đụng React.
// Quy tắc của biểu mẫu lặp lại quy tắc của backend (ReviewBatchService.SaveVerdictAsync) để báo lỗi ngay trên màn hình;
// backend vẫn là nơi quyết định cuối cùng.

import { intentLabel } from "@/lib/chat-review-format";
import type {
  ReviewAnswerVerdict,
  ReviewBatchListItem,
  ReviewCause,
  ReviewLabelVerdict,
  ReviewVerdict,
  SaveReviewVerdictRequest,
  SlmIntentType,
} from "@/types/review-batch";

// ------------------------------------------------------------------ nhãn chữ và thứ tự các lựa chọn trên biểu mẫu
export const LABEL_VERDICT_OPTIONS: { id: ReviewLabelVerdict; label: string }[] = [
  { id: "correct", label: "Nhãn đúng" },
  { id: "wrong", label: "Nhãn sai" },
];

export const ANSWER_VERDICT_OPTIONS: { id: ReviewAnswerVerdict; label: string }[] = [
  { id: "correct", label: "Đúng" },
  { id: "wrong", label: "Sai" },
  { id: "missing_data", label: "Thiếu dữ liệu" },
  { id: "out_of_scope", label: "Ngoài phạm vi" },
];

export const CAUSE_OPTIONS: { id: ReviewCause; label: string }[] = [
  { id: "misread_intent", label: "Hiểu sai ý" },
  { id: "missing_knowledge", label: "Thiếu kiến thức" },
  { id: "hallucination", label: "Bịa thông tin" },
  { id: "technical", label: "Lỗi kỹ thuật" },
];

/** Sáu loại ý để chọn nhãn đúng, theo thứ tự quen dùng. */
export const INTENT_OPTIONS: { id: SlmIntentType; label: string }[] = (
  ["collect_profile", "profile_review", "scenario", "knowledge", "center_referral", "out_of_scope"] as SlmIntentType[]
).map((id) => ({ id, label: intentLabel(id) }));

const labelOf = <T extends string>(options: { id: T; label: string }[], id: string): string =>
  options.find((option) => option.id === id)?.label ?? id;

export const labelVerdictLabel = (id: string) => labelOf(LABEL_VERDICT_OPTIONS, id);
export const answerVerdictLabel = (id: string) => labelOf(ANSWER_VERDICT_OPTIONS, id);
export const causeLabel = (id: string) => labelOf(CAUSE_OPTIONS, id);

// ------------------------------------------------------------------ nhóm (skill) của một câu
export const UNLABELED_STRATUM = "unlabeled";

/**
 * Tên dễ đọc của nhóm lúc bốc mẫu. Nhóm là các skill nối bằng "+" ("knowledge+scenario"); tên skill trùng tên loại ý nên dùng lại nhãn của loại ý.
 * Câu không có skill là "unlabeled". Tên lạ giữ nguyên để không mất thông tin.
 */
export function stratumLabel(stratum: string): string {
  if (stratum === UNLABELED_STRATUM) return "Chưa gắn skill";
  return stratum
    .split("+")
    .map((name) => intentLabel(name))
    .join(" + ");
}

// ------------------------------------------------------------------ tiến độ
export interface Progress {
  graded: number;
  total: number;
  /** 0 đến 100, làm tròn xuống: chỉ là 100 khi đã chấm hết. */
  percent: number;
  done: boolean;
}

export function progressOf(graded: number, total: number): Progress {
  const safeTotal = Math.max(0, total);
  const safeGraded = Math.min(Math.max(0, graded), safeTotal);
  return {
    graded: safeGraded,
    total: safeTotal,
    percent: safeTotal === 0 ? 0 : Math.floor((safeGraded / safeTotal) * 100),
    done: safeTotal > 0 && safeGraded >= safeTotal,
  };
}

export const batchProgress = (batch: Pick<ReviewBatchListItem, "myGraded" | "totalItems">): Progress =>
  progressOf(batch.myGraded, batch.totalItems);

// ------------------------------------------------------------------ biểu mẫu chấm
/** Trạng thái biểu mẫu: null là chưa chọn. Chưa đủ hai lựa chọn bắt buộc thì chưa gửi được. */
export interface VerdictForm {
  label: ReviewLabelVerdict | null;
  correctIntent: SlmIntentType | null;
  answer: ReviewAnswerVerdict | null;
  cause: ReviewCause | null;
}

export const EMPTY_VERDICT_FORM: VerdictForm = { label: null, correctIntent: null, answer: null, cause: null };

export const formFromVerdict = (verdict: ReviewVerdict | null): VerdictForm =>
  verdict
    ? { label: verdict.labelVerdict, correctIntent: verdict.correctIntent, answer: verdict.answerVerdict, cause: verdict.cause }
    : EMPTY_VERDICT_FORM;

/** Ô nhãn đúng chỉ hiện khi nhãn SLM gán là sai. */
export const needsCorrectIntent = (form: VerdictForm) => form.label === "wrong";

/** Ô nguyên nhân chỉ hiện (và bắt buộc) khi câu trả lời sai. */
export const needsCause = (form: VerdictForm) => form.answer === "wrong";

/**
 * Lỗi của biểu mẫu để báo ngay (chưa chọn đủ, thiếu nguyên nhân khi câu trả lời sai), hoặc null nếu gửi được.
 * Ô không còn áp dụng (nhãn đúng khi nhãn SLM đúng, nguyên nhân khi câu trả lời đúng) không phải lỗi: toRequest bỏ chúng đi.
 */
export function validateVerdict(form: VerdictForm): string | null {
  if (form.label === null) return "Chọn nhãn SLM gán là đúng hay sai.";
  if (form.answer === null) return "Chọn đánh giá cho câu trả lời.";
  if (needsCause(form) && form.cause === null) return "Chọn nguyên nhân khi câu trả lời sai.";
  return null;
}

/**
 * Nội dung gửi lên. Bỏ các ô không còn áp dụng: người duyệt chọn "nhãn sai" + một nhãn đúng, rồi đổi ý thành "nhãn đúng",
 * thì nhãn đúng cũ không được gửi đi (backend từ chối tổ hợp đó). Trả null nếu biểu mẫu chưa hợp lệ.
 */
export function toRequest(form: VerdictForm): SaveReviewVerdictRequest | null {
  if (validateVerdict(form) !== null || form.label === null || form.answer === null) return null;
  return {
    labelVerdict: form.label,
    correctIntent: needsCorrectIntent(form) ? form.correctIntent : null,
    answerVerdict: form.answer,
    cause: needsCause(form) ? form.cause : null,
  };
}
