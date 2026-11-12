// Nhãn chữ, tiến độ và quy tắc của biểu mẫu chấm cho đợt duyệt mẫu. Toàn hàm thuần, không đụng React.
// Quy tắc của biểu mẫu lặp lại quy tắc của backend (ReviewBatchService.SaveVerdictAsync) để báo lỗi ngay trên màn hình;
// backend vẫn là nơi quyết định cuối cùng.

import { formatPct } from "@/lib/ai-usage-format";
import { intentLabel } from "@/lib/chat-review-format";
import type {
  RateEstimate,
  ReviewAnswerVerdict,
  ReviewBatchListItem,
  ReviewBatchReport,
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

// ------------------------------------------------------------------ báo cáo
/** Tỷ lệ một con số: 0.2333 → "23,3%". */
export const rateText = (estimate: RateEstimate): string => formatPct(estimate.rate);

/**
 * Khoảng tin cậy dưới dạng chữ: "khoảng 15% đến 50%". Nhóm đã chấm hết (low = high) không phải ước lượng nên ghi "chính xác".
 * Làm tròn ra phía ngoài (thấp xuống, cao lên) để khoảng không bao giờ hẹp hơn thực tế.
 */
export function intervalText(estimate: RateEstimate): string {
  if (estimate.low === estimate.high) return "chính xác, đã chấm hết";
  const low = Math.floor(estimate.low * 100);
  const high = Math.ceil(estimate.high * 100);
  return `khoảng ${low}% đến ${high}%`;
}

/** Số đếm thô kèm mẫu số: "6 / 20 câu". */
export const countText = (estimate: RateEstimate): string => `${estimate.count} / ${estimate.total} câu`;

export const intentOrUnknownLabel = (intent: string): string => (intent === "unknown" ? "Không chắc" : intentLabel(intent));

/** Cảnh báo cần đọc trước khi tin số liệu: chưa chấm hết hoặc tỷ lệ chung chưa phủ hết các nhóm. Rỗng là không có gì đáng lo. */
export function reportNotices(report: ReviewBatchReport): string[] {
  if (report.itemsGraded === 0) return ["Chưa chấm câu nào nên chưa có số liệu."];

  const notices: string[] = [];
  if (report.itemsGraded < report.totalItems) {
    notices.push(`Mới chấm ${report.itemsGraded} / ${report.totalItems} câu của đợt, số liệu sẽ đổi khi chấm tiếp.`);
  }
  const uncovered = report.report.strata.filter((s) => s.graded === 0).map((s) => stratumLabel(s.stratum));
  if (uncovered.length > 0) {
    notices.push(
      `Tỷ lệ chung chỉ nói về ${report.report.coveredPopulation} / ${report.report.population} câu. Nhóm chưa chấm câu nào: ${uncovered.join(", ")}.`,
    );
  }
  return notices;
}
