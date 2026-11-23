import type { CostLine } from "@/types/cost";

export const COST_LABELS: Record<string, string> = {
  tuition: "Học phí",
  living: "Sinh hoạt",
  fees: "Phí trường",
  other: "Chi phí khác",
  insurance: "Bảo hiểm y tế",
  visa: "Phí visa (một lần)",
  sevis: "Phí SEVIS (một lần)",
};

export const usd = (n: number) => `$${Math.round(n).toLocaleString("en-US")}`;
export const vnd = (n: number, rate: number) => `${Math.round(n * rate).toLocaleString("vi-VN")} ₫`;

/** [USAS-2066] Thiếu học phí thì không tính tổng chi phí của trường (UC-26). */
export const TUITION_MISSING_TEXT = "Chưa đủ dữ liệu học phí nên chưa tính tổng chi phí";
export const tuitionMissing = (school: { missingKeys: string[] }) => school.missingKeys.includes("tuition");

/** Số âm là dư, dương là thiếu; viết bằng lời để không phải đọc dấu. */
export function gapText(gap: number | null): string | null {
  if (gap === null) return null;
  return gap > 0 ? `Thiếu ${usd(gap)}` : gap < 0 ? `Dư ${usd(-gap)}` : "Vừa đủ ngân sách";
}

/** Gộp khoản nhỏ để thanh biểu đồ chỉ có 4 phần: học phí, sinh hoạt, bảo hiểm, còn lại. */
export function chartSegments(lines: CostLine[]): { key: string; label: string; value: number }[] {
  const get = (k: string) => lines.find((l) => l.key === k && !l.isOneTime)?.amountUsd ?? 0;
  return [
    { key: "tuition", label: COST_LABELS.tuition, value: get("tuition") },
    { key: "living", label: COST_LABELS.living, value: get("living") },
    { key: "insurance", label: COST_LABELS.insurance, value: get("insurance") },
    { key: "rest", label: "Phí và khác", value: get("fees") + get("other") },
  ];
}
