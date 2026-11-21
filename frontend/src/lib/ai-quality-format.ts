// Nhãn, màu và gom dữ liệu cho trang Chất lượng AI (UC-90). Toàn hàm thuần, không đụng React.

import { intentLabel } from "@/lib/chat-review-format";
import { outcomeLabel, purposeLabel } from "@/lib/ai-usage-format";
import type { AiQualityDay, AiQualityReport, OutcomeCounts, SampleOutcome } from "@/types/ai-quality";

export type OutcomeKey = "answered" | "noAnswer" | "refused" | "failed";

/** Thứ tự và màu cố định của bốn loại kết quả, dùng chung cho mọi biểu đồ để màu luôn cùng một nghĩa. */
export const OUTCOMES: { key: OutcomeKey; code: string; color: string }[] = [
  { key: "answered", code: "answered", color: "var(--viz-1)" },
  { key: "noAnswer", code: "no_answer", color: "var(--viz-2)" },
  { key: "refused", code: "refused", color: "var(--viz-3)" },
  { key: "failed", code: "failed", color: "var(--viz-4)" },
];

export const outcomeName = (key: OutcomeKey): string => outcomeLabel(OUTCOMES.find((o) => o.key === key)!.code);

/** Ba loại xem được nội dung, theo thứ tự cần chú ý. Màu và khóa đếm khớp với bốn loại ở OUTCOMES. */
export const SAMPLE_OUTCOMES: { id: SampleOutcome; key: OutcomeKey; label: string; hint: string; color: string }[] = [
  { id: "no_answer", key: "noAnswer", label: outcomeLabel("no_answer"), color: "var(--viz-2)", hint: "Hệ thống muốn trả lời nhưng không thể: không hiểu câu hỏi, hoặc kho kiến thức chưa có thông tin." },
  { id: "failed", key: "failed", label: outcomeLabel("failed"), color: "var(--viz-4)", hint: "Có bước AI hoặc dịch vụ bị lỗi trong lượt: hết thời gian, bị từ chối, trả sai khuôn." },
  { id: "refused", key: "refused", label: outcomeLabel("refused"), color: "var(--viz-3)", hint: "Hệ thống hiểu câu hỏi và cố ý không trả lời: ngoài chuyện du học hoặc chủ đề nhạy cảm." },
];

export const OTHER_FEATURE = "other";
/** Khớp AiQualityMetaReader.SlmFallbackFeature ở backend: SLM hỏng hai lần nên hệ thống dùng nhãn dự phòng. */
export const SLM_FALLBACK_FEATURE = "slm_fallback";

/**
 * Loại ý SLM đầu tiên của lượt; "other" là lượt không rõ (ví dụ bấm nút). Lượt SLM hỏng là nhóm riêng: nhãn "ngoài phạm vi" của
 * lượt đó do hệ thống tự gán chứ không phải model kết luận, và câu có thể hoàn toàn hợp lệ.
 */
export function featureLabel(feature: string): string {
  if (feature === OTHER_FEATURE) return "Không rõ";
  if (feature === SLM_FALLBACK_FEATURE) return "SLM không đọc được (dùng dự phòng)";
  return intentLabel(feature);
}

/** Bước AI hỏng: tên skill (slm_intents...), hoặc "advisor" khi advisor sập hẳn. */
export function stepLabel(step: string): string {
  return step === "advisor" ? "Advisor không phản hồi" : purposeLabel(step);
}

export interface QualityDayPoint {
  date: string;
  total: number;
  answered: number;
  noAnswer: number;
  refused: number;
  failed: number;
  unclassified: number;
  p50: number | null;
  p95: number | null;
}

/** Một dòng mỗi ngày cho biểu đồ cột chồng và đường thời gian (đã có đủ mọi ngày trong khoảng từ backend). */
export function dayPoints(days: AiQualityDay[]): QualityDayPoint[] {
  return days.map((day) => ({
    date: day.date,
    total: day.counts.total,
    answered: day.counts.answered,
    noAnswer: day.counts.noAnswer,
    refused: day.counts.refused,
    failed: day.counts.failed,
    unclassified: day.counts.unclassified,
    p50: day.latency.p50Ms,
    p95: day.latency.p95Ms,
  }));
}

export function countOf(counts: OutcomeCounts, key: OutcomeKey): number {
  return counts[key];
}

export function rateOf(counts: OutcomeCounts, key: OutcomeKey): number {
  return counts[`${key}Rate` as const];
}

/** Có lượt nào chưa phân loại (tin cũ chưa có kết quả) để báo rõ thay vì để tổng lệch. */
export function hasUnclassified(report: AiQualityReport | null): boolean {
  return (report?.total.unclassified ?? 0) > 0;
}

/** Tỉ lệ lượt phải dùng đường dự phòng trên tổng lượt (0 khi chưa có lượt nào). */
export function fallbackRate(turns: number, total: number): number {
  return total > 0 ? turns / total : 0;
}
