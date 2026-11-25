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

// [USAS-2067] Các cột của bộ dữ liệu học bổng
export const KIND_LABELS: Record<string, string> = {
  merit: "Theo thành tích",
  need: "Theo hoàn cảnh tài chính",
  merit_and_need: "Thành tích và hoàn cảnh",
  athletic: "Thể thao",
  program: "Theo chương trình",
  external: "Từ tổ chức bên ngoài",
  other: "Khác",
};

export const AMOUNT_TYPE_LABELS: Record<string, string> = {
  fixed: "Số tiền cố định",
  range: "Theo khoảng",
  percent_tuition: "Phần trăm học phí",
  full_tuition: "Toàn bộ học phí",
  full_ride: "Toàn phần (full ride)",
  varies: "Thay đổi theo hồ sơ",
};

export const APPLICATION_METHOD_LABELS: Record<string, string> = {
  automatic: "Tự động xét khi nộp hồ sơ",
  separate_application: "Nộp hồ sơ riêng",
  nomination: "Được giới thiệu",
  unknown: "Chưa rõ",
};

function toOptions(labels: Record<string, string>): LabeledOption[] {
  return [{ value: "", label: "Chưa rõ" }, ...Object.entries(labels).map(([value, label]) => ({ value, label }))];
}

// UC-29 & UC-103: chỉ học bổng do trường cấp, không cho chọn loại external
export const KIND_OPTIONS = toOptions(
  Object.fromEntries(Object.entries(KIND_LABELS).filter(([k]) => k !== "external")),
);
export const AMOUNT_TYPE_OPTIONS = toOptions(AMOUNT_TYPE_LABELS);
export const APPLICATION_METHOD_OPTIONS = toOptions(APPLICATION_METHOD_LABELS);

/** Ba trạng thái: Có / Không / Chưa rõ (null). */
export const TRI_STATE_OPTIONS: LabeledOption[] = [
  { value: "", label: "Chưa rõ" },
  { value: "true", label: "Có" },
  { value: "false", label: "Không" },
];

export function triStateLabel(value: boolean | null): string {
  return value === null ? "Chưa rõ" : value ? "Có" : "Không";
}
