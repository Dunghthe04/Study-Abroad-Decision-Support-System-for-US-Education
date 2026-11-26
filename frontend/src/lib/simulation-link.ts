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

const EC_TIER_RANGE = [1, 5] as const;
const MAX_STATES = 100;   // trần an toàn, không phải giới hạn sản phẩm (backend cũng 100)
const MAX_MAJOR_LENGTH = 200;
const MAX_MAJORS = 5;
const MAX_RATE_PERCENT = 100;
const MAX_RATE_VND = 1_000_000;

// Bang: mã hai chữ cái (chữ thường vẫn nhận, đổi thành hoa), không trùng, tối đa MAX_STATES (trần an toàn). Sai thì bỏ cả bộ
function toStates(value: unknown): string[] | null {
  const list = typeof value === "string" ? value.split(",") : value;
  if (!Array.isArray(list) || list.length === 0) return null;
  const codes = list.map((v) => (typeof v === "string" ? v.trim().toUpperCase() : ""));
  if (codes.length > MAX_STATES || !codes.every((c) => /^[A-Z]{2}$/.test(c))) return null;
  return [...new Set(codes)];
}

function toMajor(value: unknown): string | null {
  return typeof value === "string" && value.trim() !== "" && value.length <= MAX_MAJOR_LENGTH ? value.trim() : null;
}

// Nhiều ngành: danh sách chữ hoặc chuỗi ngăn bằng "|" (tên ngành có thể chứa dấu phẩy nên không dùng dấu phẩy). Sai thì bỏ cả bộ
function toMajors(value: unknown): string[] | null {
  const list = typeof value === "string" ? value.split("|") : value;
  if (!Array.isArray(list) || list.length === 0 || list.length > MAX_MAJORS) return null;
  const names = list.map((v) => (typeof v === "string" ? v.trim() : ""));
  if (!names.every((n) => n !== "" && n.length <= MAX_MAJOR_LENGTH && !n.includes("|"))) return null;
  return [...new Set(names)];
}

// Tỉ giá giả định: số VNĐ mỗi USD hoặc phần trăm tăng. Chỉ kiểm khoảng, "chỉ nhận mức tăng so với hiện nay" do backend kiểm vì cần tỉ giá hiện nay
function addExtras(overrides: SimulationOverrides, get: (key: string) => unknown): void {
  const tier = Number(get("ecTier"));
  if (get("ecTier") != null && String(get("ecTier")).trim() !== "" && Number.isInteger(tier) && tier >= EC_TIER_RANGE[0] && tier <= EC_TIER_RANGE[1])
    overrides.ecTier = tier;
  const states = toStates(get("states"));
  if (states) overrides.states = states;
  const majors = toMajors(get("majors"));
  const major = toMajor(get("major"));
  if (majors) overrides.majors = majors;
  else if (major) overrides.major = major;
  const vnd = Number(get("exchangeRateVnd"));
  const percent = Number(get("exchangeRatePercent"));
  // Nhập cả hai thì lấy số VNĐ (backend cũng từ chối nhập cả hai)
  if (get("exchangeRateVnd") != null && String(get("exchangeRateVnd")).trim() !== "" && vnd > 0 && vnd <= MAX_RATE_VND) overrides.exchangeRateVnd = vnd;
  else if (get("exchangeRatePercent") != null && String(get("exchangeRatePercent")).trim() !== "" && percent > 0 && percent <= MAX_RATE_PERCENT)
    overrides.exchangeRatePercent = percent;
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
  addExtras(overrides, (key) => values?.[key]);
  const pref = toPref(values?.pref);
  if (pref) overrides.pref = pref;
  return overrides;
}

// Địa chỉ trang Gợi ý trường kèm giá trị giả định của chat. ts đổi mỗi lần bấm để trang dựng lại và đọc lại giá trị.
export function simulationHref(values: Record<string, unknown> | null | undefined): string {
  const query = new URLSearchParams({ ts: String(Date.now()) });
  // pref là mảng nên thành "2,4,2"; nhiều ngành nối bằng "|" vì tên ngành có thể chứa dấu phẩy
  for (const [key, value] of Object.entries(overridesFromValues(values)))
    query.set(key, key === "majors" && Array.isArray(value) ? value.join("|") : String(value));
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
  addExtras(overrides, (key) => params[key]);
  const pref = toPref(params.pref);
  if (pref) overrides.pref = pref;
  return overrides;
}

// Sự kiện báo trang Gợi ý trường đang mở: đặt lại thanh kéo theo kịch bản chat vừa thử (detail: SimulationOverrides)
export const SIMULATE_EVENT = "usas:simulate";

// Khung chat báo trang Gợi ý trường bỏ hết giá trị giả định (nút "Về hồ sơ thật")
export const RESET_SIMULATION_EVENT = "usas:simulate-reset";

// Khung chat báo trang Gợi ý trường: hồ sơ chính thức vừa được lưu, danh sách đang xem có thể đã cũ
export const PROFILE_SAVED_EVENT = "usas:profile-saved";
// Học sinh vừa hỏi "cần gì để vào trường X" trong chat: trang Gợi ý trường ghim trường đó (detail là mã trường) để luôn thấy nó
export const GOAL_SCHOOL_EVENT = "usas:goal-school";

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
  if (o.ecTier != null) parts.push(`bậc ngoại khóa ${o.ecTier}`);
  if (o.states != null && o.states.length > 0) parts.push(`bang ${o.states.join(", ")}`);
  if (o.major != null) parts.push(`ngành ${o.major}`);
  if (o.majors != null && o.majors.length > 0) parts.push(`ngành ${o.majors.join(", ")}`);
  if (o.exchangeRateVnd != null) parts.push(`tỉ giá ${NUMBER.format(o.exchangeRateVnd)} VNĐ/USD`);
  if (o.exchangeRatePercent != null) parts.push(`tỉ giá tăng ${NUMBER.format(o.exchangeRatePercent)}%`);
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
