// Chat vừa thử một kịch bản what-if: đưa các giá trị giả định sang trang Gợi ý trường.
// Đang đứng ở trang đó thì cập nhật thanh kéo tại chỗ (không tải lại trang); ở trang khác thì chuyển trang kèm giá trị qua địa chỉ.

import type { Pref, ScholarshipNeed, SimulationOverrides } from "@/types/recommendation";

const NEEDS: ScholarshipNeed[] = ["optional", "preferred", "mandatory"];

// Khoảng hợp lệ của từng giá trị, giống kiểm tra ở backend (RecommendationService.Overlay)
const RANGES = {
  gpa: [0, 4],
  sat: [400, 1600],
  act: [1, 36],
  ielts: [0, 9],
  toefl: [0, 120],
  duolingo: [10, 160],
  budgetUsd: [0, 10_000_000],
} as const;

type NumberKey = keyof typeof RANGES;
const NUMBER_KEYS = Object.keys(RANGES) as NumberKey[];

function inRange(key: NumberKey, value: number): boolean {
  const [min, max] = RANGES[key];
  return Number.isFinite(value) && value >= min && value <= max;
}

function isNeed(value: unknown): value is ScholarshipNeed {
  return typeof value === "string" && NEEDS.includes(value as ScholarshipNeed);
}

// Ba mức ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng), mỗi mức số nguyên 0..4 như thanh kéo. Sai thì bỏ cả bộ
function toPref(value: unknown): Pref | null {
  const list = typeof value === "string" ? value.split(",").map((part) => (part.trim() === "" ? NaN : Number(part))) : value;
  if (!Array.isArray(list) || list.length !== 3) return null;
  return list.every((n) => Number.isInteger(n) && n >= 0 && n <= 4) ? (list as Pref) : null;
}

// Giá trị giả định hợp lệ trong widget của chat. Giá trị sai (không phải số, ngoài khoảng) bị bỏ.
export function overridesFromValues(values: Record<string, unknown> | null | undefined): SimulationOverrides {
  const overrides: SimulationOverrides = {};
  for (const key of NUMBER_KEYS) {
    const value = values?.[key];
    if (typeof value === "number" && inRange(key, value)) overrides[key] = value;
  }
  if (isNeed(values?.scholarshipNeed)) overrides.scholarshipNeed = values.scholarshipNeed;
  const pref = toPref(values?.pref);
  if (pref) overrides.pref = pref;
  return overrides;
}

// Địa chỉ trang Gợi ý trường kèm giá trị giả định của chat. ts đổi mỗi lần bấm để trang dựng lại và đọc lại giá trị.
export function simulationHref(values: Record<string, unknown> | null | undefined): string {
  const query = new URLSearchParams({ ts: String(Date.now()) });
  for (const [key, value] of Object.entries(overridesFromValues(values))) query.set(key, String(value));   // pref là mảng nên thành "2,4,2"
  return `/recommendations?${query.toString()}`;
}

type Params = Record<string, string | string[] | undefined>;

// Đọc ngược các giá trị đó từ địa chỉ trang. Giá trị sai (không phải số, ngoài khoảng) bị bỏ, không làm hỏng trang.
export function overridesFromParams(params: Params): SimulationOverrides {
  const overrides: SimulationOverrides = {};
  for (const key of NUMBER_KEYS) {
    const raw = params[key];
    if (typeof raw !== "string" || raw.trim() === "") continue;
    const value = Number(raw);
    if (inRange(key, value)) overrides[key] = value;
  }
  if (isNeed(params.scholarshipNeed)) overrides.scholarshipNeed = params.scholarshipNeed;
  const pref = toPref(params.pref);
  if (pref) overrides.pref = pref;
  return overrides;
}

// Sự kiện báo trang Gợi ý trường đang mở: đặt lại thanh kéo theo kịch bản chat vừa thử (detail: SimulationOverrides)
export const SIMULATE_EVENT = "usas:simulate";

// Khung chat báo trang Gợi ý trường bỏ hết giá trị giả định (nút "Về hồ sơ thật")
export const RESET_SIMULATION_EVENT = "usas:simulate-reset";

export function resetSimulation(): void {
  window.dispatchEvent(new CustomEvent(RESET_SIMULATION_EVENT));
}

const NUMBER = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });
const NEED_TEXT: Record<string, string> = { optional: "không cần", preferred: "nên có", mandatory: "bắt buộc" };

// Mô tả ngắn các giá trị giả định đang kéo ("GPA 3,6 · SAT 1.500"), để học sinh biết chat đang nói về giả định nào
export function describeSimulation(o: SimulationOverrides | null | undefined): string {
  if (!o) return "";
  const parts: string[] = [];
  if (o.gpa != null) parts.push(`GPA ${NUMBER.format(o.gpa)}`);
  if (o.sat != null) parts.push(`SAT ${NUMBER.format(o.sat)}`);
  if (o.act != null) parts.push(`ACT ${NUMBER.format(o.act)}`);
  if (o.ielts != null) parts.push(`IELTS ${NUMBER.format(o.ielts)}`);
  if (o.toefl != null) parts.push(`TOEFL ${NUMBER.format(o.toefl)}`);
  if (o.duolingo != null) parts.push(`Duolingo ${NUMBER.format(o.duolingo)}`);
  if (o.budgetUsd != null) parts.push(`ngân sách ${NUMBER.format(o.budgetUsd)} USD`);
  if (o.scholarshipNeed != null) parts.push(`học bổng ${NEED_TEXT[o.scholarshipNeed] ?? o.scholarshipNeed}`);
  if (o.pref != null) parts.push(`ưu tiên: chi phí ${o.pref[0]}, học bổng ${o.pref[1]}, danh tiếng ${o.pref[2]}`);
  return parts.join(" · ");
}

export function onRecommendationsPage(): boolean {
  return window.location.pathname.startsWith("/recommendations");
}

// Đang ở trang Gợi ý trường: cập nhật thanh kéo tại chỗ, không tải lại trang. Ở trang khác: chuyển trang kèm giá trị.
export function showSimulation(values: Record<string, unknown> | null | undefined, navigate: (href: string) => void): void {
  if (!onRecommendationsPage()) {
    navigate(simulationHref(values));
    return;
  }
  const overrides = overridesFromValues(values);
  if (Object.keys(overrides).length > 0) window.dispatchEvent(new CustomEvent(SIMULATE_EVENT, { detail: overrides }));
}
