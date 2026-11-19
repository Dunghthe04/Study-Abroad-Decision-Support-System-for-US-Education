// UC-76: phụ huynh chọn 3 mức ưu tiên + ngân sách VND cho con có liên kết thật, so danh sách theo cha mẹ với danh sách của con.
// Con managed / không liên kết: backend trả 404. Hồ sơ con chưa đủ để tính: 409. pref hoặc số tiền sai: 400.

import { apiFetch } from "@/lib/api";
import type { RecommendationItems } from "@/types/recommendation";

/** [chi phí thấp, học bổng nhiều, danh tiếng], mỗi mức 0..4 */
export type PrefTriple = [number, number, number];

export const DEFAULT_PREF: PrefTriple = [2, 2, 2];
export const MIN_BUDGET_VND = 100_000_000; // 100 triệu
export const MAX_BUDGET_VND = 10_000_000_000; // 10 tỷ

export interface ParentPreferenceDto {
  pref: number[];
  budgetVnd: number | null;
  vndPerUsd: number | null;
  budgetUsd: number | null;
  updatedAt: string;
  hasResult: boolean;
}

/** Nhãn của PreferenceComparer (backend CompareLabels) */
export type CompareLabel =
  | "both"
  | "parent_higher"
  | "parent_lower"
  | "moved_to_aid"
  | "moved_out_of_aid"
  | "group_changed"
  | "parent_drops"
  | "dropped_over_budget"
  | "parent_adds";

export type CompareGroup = "reach" | "match" | "safety" | "aid" | "other";

export interface ComparedSchool {
  code: string;
  name: string | null;
  studentGroup: CompareGroup | null;
  studentRank: number | null;
  parentGroup: CompareGroup | null;
  parentRank: number | null;
  label: CompareLabel;
  reason: string | null;
}

export interface PreferenceComparisonResult {
  schools: ComparedSchool[];
  counts: { both: number; studentOnly: number; parentOnly: number };
  sameResult: boolean;
  kept: number;
  sameNote: string | null;
}

export interface PreferenceCompareDto {
  studentHasRun: boolean;
  studentPref: number[] | null;
  parentPref: number[];
  parentBudgetVnd: number | null;
  parentBudgetUsd: number | null;
  vndPerUsd: number | null;
  parentHasPref: boolean;
  isPreview: boolean;
  profileChangedAfterRun: boolean;
  studentRunAt: string | null;
  savedAt: string | null;
  computedAt: string | null;
  comparison: PreferenceComparisonResult | null;
  message: string | null;
  /** Hai danh sách cùng khuôn trang Gợi ý, chỉ có số liệu các trường được xếp nhóm. API cũ không có. */
  studentItems?: CompareItems | null;
  parentItems?: CompareItems | null;
  /** Ngân sách (USD/năm) và nhu cầu học bổng của con trong lần gợi ý đang so. API cũ không có. */
  studentBudgetUsd?: number | null;
  studentScholarshipNeed?: string | null;
}

export type CompareItems = Omit<RecommendationItems, "failed">;

export interface ChildPreferenceView {
  parents: { id: string; name: string; hasPref: boolean }[];
  selectedParentId: string | null;
  compare: PreferenceCompareDto | null;
}

interface Wrapped<T> {
  message: string | null;
  data: T;
}

const base = (studentId: string) => `/api/v1/parent/children/${encodeURIComponent(studentId)}/preference`;

export const parentPreferenceApi = {
  // Ý đã lưu, null nếu chưa lưu lần nào
  get: async (studentId: string) =>
    (await apiFetch<Wrapped<ParentPreferenceDto | null>>(base(studentId), { method: "GET" })).data,

  // Lưu, chốt tỷ giá hôm nay, tính sẵn danh sách theo cha mẹ và báo cho con
  save: async (studentId: string, pref: PrefTriple, budgetVnd: number | null) =>
    (
      await apiFetch<Wrapped<ParentPreferenceDto>>(base(studentId), {
        method: "PUT",
        body: JSON.stringify({ pref, budgetVnd }),
      })
    ).data,

  // Có preview: xem thử giá trị chưa lưu. Không có: so với ý đã lưu (chưa lưu thì backend xem thử với [2,2,2]).
  compare: async (studentId: string, preview?: { pref: PrefTriple; budgetVnd: number | null }) => {
    const query = preview
      ? `?pref=${preview.pref.join(",")}${preview.budgetVnd != null ? `&budgetVnd=${preview.budgetVnd}` : ""}`
      : "";
    return (await apiFetch<Wrapped<PreferenceCompareDto>>(`${base(studentId)}/compare${query}`, { method: "GET" })).data;
  },

  // Trang của con: chỉ đọc, chọn Bố / Mẹ bằng parentId
  forChild: async (parentId?: string) =>
    (
      await apiFetch<Wrapped<ChildPreferenceView>>(
        parentId ? `/api/v1/parent-preferences?parentId=${encodeURIComponent(parentId)}` : "/api/v1/parent-preferences",
        { method: "GET" },
      )
    ).data,
};
