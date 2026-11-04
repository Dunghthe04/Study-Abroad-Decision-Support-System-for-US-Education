// Chat vừa thử một kịch bản what-if: đưa các giá trị giả định sang trang Gợi ý trường.
// Đang đứng ở trang đó thì cập nhật thanh kéo tại chỗ (không tải lại trang); ở trang khác thì chuyển trang kèm giá trị qua địa chỉ.

import type { GoalTarget, ScholarshipNeed, SimulationOverrides } from "@/types/recommendation";

const NEEDS: ScholarshipNeed[] = ["optional", "preferred", "mandatory"];

// Khoảng hợp lệ của từng giá trị, giống kiểm tra ở backend (RecommendationService.Overlay)
const RANGES = {
  gpa: [0, 4],
  sat: [400, 1600],
  act: [1, 36],
  ielts: [0, 9],
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

// Giá trị giả định hợp lệ trong widget của chat. Giá trị sai (không phải số, ngoài khoảng) bị bỏ.
export function overridesFromValues(values: Record<string, unknown> | null | undefined): SimulationOverrides {
  const overrides: SimulationOverrides = {};
  for (const key of NUMBER_KEYS) {
    const value = values?.[key];
    if (typeof value === "number" && inRange(key, value)) overrides[key] = value;
  }
  if (isNeed(values?.scholarshipNeed)) overrides.scholarshipNeed = values.scholarshipNeed;
  return overrides;
}

// Địa chỉ trang Gợi ý trường kèm giá trị giả định của chat. ts đổi mỗi lần bấm để trang dựng lại và đọc lại giá trị.
export function simulationHref(values: Record<string, unknown> | null | undefined): string {
  const query = new URLSearchParams({ ts: String(Date.now()) });
  for (const [key, value] of Object.entries(overridesFromValues(values))) query.set(key, String(value));
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
  return overrides;
}

// Sự kiện báo trang Gợi ý trường đang mở: đặt lại thanh kéo theo kịch bản chat vừa thử (detail: SimulationOverrides)
export const SIMULATE_EVENT = "usas:simulate";

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

// ---- "Cần gì để vào trường X" hỏi trong chat: mở khung kết quả goal-seeking trên trang Gợi ý trường ----

export interface GoalRequest {
  code: string;
  target: GoalTarget | null; // null: nhóm cao hơn một bậc
}

const TARGETS: GoalTarget[] = ["reach", "match", "safety"];

function goalFrom(code: unknown, target: unknown): GoalRequest | null {
  if (typeof code !== "string" || !/^[\w-]{1,64}$/.test(code)) return null;
  return { code, target: typeof target === "string" && TARGETS.includes(target as GoalTarget) ? (target as GoalTarget) : null };
}

// Widget của chat (mã trường và nhóm đích). Giá trị sai thì bỏ, không làm hỏng trang.
export function goalFromValues(values: Record<string, unknown> | null | undefined): GoalRequest | null {
  return goalFrom(values?.code, values?.target);
}

export function goalFromParams(params: Params): GoalRequest | null {
  return goalFrom(params.goal, params.target);
}

export function goalHref(goal: GoalRequest): string {
  const query = new URLSearchParams({ ts: String(Date.now()), goal: goal.code });
  if (goal.target) query.set("target", goal.target);
  return `/recommendations?${query.toString()}`;
}

// Sự kiện báo trang Gợi ý trường đang mở: mở khung goal-seeking của trường (detail: GoalRequest)
export const GOAL_EVENT = "usas:goal";

// Đang ở trang Gợi ý trường thì mở khung tại chỗ, ở trang khác thì chuyển trang kèm mã trường
export function showGoal(values: Record<string, unknown> | null | undefined, navigate: (href: string) => void): void {
  const goal = goalFromValues(values);
  if (!goal) return;
  if (!onRecommendationsPage()) {
    navigate(goalHref(goal));
    return;
  }
  window.dispatchEvent(new CustomEvent(GOAL_EVENT, { detail: goal }));
}
