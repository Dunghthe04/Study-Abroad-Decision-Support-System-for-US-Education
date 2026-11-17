import { formatUsd, type LabeledOption } from "@/lib/admin-university-display";
import type { AdminScholarship } from "@/types/admin-scholarship";

const COVERAGE_LABELS: Record<string, string> = {
  full: "Toàn phần",
  partial: "Một phần",
  tuition: "Học phí",
  stipend: "Sinh hoạt phí",
};

export const COVERAGE_OPTIONS: LabeledOption[] = [
  { value: "", label: "Chưa rõ" },
  ...Object.entries(COVERAGE_LABELS).map(([value, label]) => ({ value, label })),
];

export function coverageLabel(coverage: string | null): string {
  return coverage ? (COVERAGE_LABELS[coverage] ?? coverage) : "—";
}

export function scholarshipValueText(scholarship: AdminScholarship): string {
  const parts = [scholarship.amountUsd === null ? null : formatUsd(scholarship.amountUsd), scholarship.coverageType ? coverageLabel(scholarship.coverageType) : null];
  return parts.filter(Boolean).join(" · ") || "—";
}
