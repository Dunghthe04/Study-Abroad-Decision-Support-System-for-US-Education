import type { BudgetCompare, CostLine } from "@/types/cost";

export const COST_LABELS: Record<string, string> = {
  tuition: "Học phí",
  living: "Sinh hoạt",
  fees: "Phí trường",
  other: "Chi phí khác",
  insurance: "Bảo hiểm y tế",
  visa: "Phí xin visa MRV (một lần)",
  sevis: "Phí SEVIS I-901 (một lần)",
  visa_integrity: "Phí Visa Integrity Fee (một lần)",
};

export const usd = (n: number) => `$${Math.round(n).toLocaleString("en-US")}`;
export const vnd = (n: number, rate: number) => `${Math.round(n * rate).toLocaleString("vi-VN")} ₫`;

/** [USAS-2066] Thiếu học phí thì không tính tổng chi phí của trường (UC-26). */
export const TUITION_MISSING_TEXT = "Chưa đủ dữ liệu học phí nên chưa tính tổng chi phí";
export const tuitionMissing = (school: { missingKeys: string[] }) => school.missingKeys.includes("tuition");

export function budgetSourceLabel(source: BudgetCompare["source"]): string {
  switch (source) {
    case "parent":
      return "của phụ huynh";
    case "input":
      return "tự nhập";
    case "student":
    case "profile":
    default:
      return "của học sinh";
  }
}

/** Số âm là dư, dương là thiếu; viết bằng lời để không phải đọc dấu. */
export function gapText(gap: number | null): string | null {
  if (gap === null) return null;
  return gap > 0 ? `Thiếu ${usd(gap)}` : gap < 0 ? `Dư ${usd(-gap)}` : "Vừa đủ ngân sách";
}

export type CellRank = "best" | "worst" | null;

/**
 * Ô thấp nhất (tốt nhất) và cao nhất của một hàng so sánh; số nhỏ hơn là tốt hơn.
 * null = không đem ra xếp (thiếu số liệu, chưa đủ khoản). Cần ít nhất 2 số khác nhau mới tô màu.
 */
export function rankCells(values: (number | null)[]): CellRank[] {
  const nums = values.filter((v): v is number => v !== null);
  if (nums.length < 2) return values.map(() => null);
  const min = Math.min(...nums);
  const max = Math.max(...nums);
  if (min === max) return values.map(() => null);
  return values.map((v) => (v === null ? null : v === min ? "best" : v === max ? "worst" : null));
}

/** Màu nền ô theo hạng; đi kèm chữ ẩn cho trình đọc màn hình vì màu không tự nói được. */
export const RANK_TINT: Record<"best" | "worst", string> = { best: "bg-ok-wash", worst: "bg-risk-wash" };
export const RANK_LABEL: Record<"best" | "worst", string> = { best: "thấp nhất", worst: "cao nhất" };

export interface ChartSegment {
  key: string;
  label: string;
  value: number;
}

/**
 * Tách riêng các khoản có số liệu thật và các khoản bị thiếu (null), không gộp null thành $0 (UC-26).
 */
export function chartData(lines: CostLine[]): { segments: ChartSegment[]; missingLabels: string[] } {
  const find = (k: string) => lines.find((l) => l.key === k && !l.isOneTime)?.amountUsd ?? null;
  const tuition = find("tuition");
  const living = find("living");
  const insurance = find("insurance");
  const fees = find("fees");
  const other = find("other");

  const segments: ChartSegment[] = [];
  const missingLabels: string[] = [];

  const pushSingle = (key: string, label: string, val: number | null, required: boolean) => {
    if (val === null) {
      if (required) missingLabels.push(label);
    } else if (val > 0) {
      segments.push({ key, label, value: val });
    }
  };

  pushSingle("tuition", COST_LABELS.tuition, tuition, true);
  pushSingle("living", COST_LABELS.living, living, true);
  pushSingle("insurance", COST_LABELS.insurance, insurance, true);

  if (fees !== null || other !== null) {
    const rest = (fees ?? 0) + (other ?? 0);
    if (rest > 0) segments.push({ key: "rest", label: "Phí và khác", value: rest });
  }

  return { segments, missingLabels };
}
