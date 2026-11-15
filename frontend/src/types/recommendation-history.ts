// [UC-87] Kiểu dữ liệu cho lịch sử gợi ý (api/v1/recommendations/history).
// Một lần chạy đầy đủ dùng lại kiểu Recommendation (cùng khuôn với latest).

import type { PickBlockName, Pref } from "@/types/recommendation";

// Bốn nhóm chính được so sánh ("other" không tính)
export type HistoryGroup = Exclude<PickBlockName, "other">;

export interface RunGroupCounts {
  reach: number;
  match: number;
  safety: number;
  aid: number;
}

// Một dòng trong danh sách lịch sử
export interface RecommendationRunSummary {
  id: string;
  createdAt: string;
  pref: Pref | null;
  major: string | null;
  counts: RunGroupCounts;
}

// Một trường thay đổi giữa hai lần chạy.
// fromGroup null = mới vào; toGroup null = rơi ra; cùng nhóm thì là đổi hạng. Hạng tính từ 1 trong nhóm.
export interface RunSchoolChange {
  code: string;
  name: string;
  fromGroup: HistoryGroup | null;
  toGroup: HistoryGroup | null;
  fromRank: number | null;
  toRank: number | null;
}

export interface RunComparison {
  from: RecommendationRunSummary;
  to: RecommendationRunSummary;
  changes: RunSchoolChange[];
}

export type RunChangeKind = "new" | "dropped" | "group" | "rank";

// Loại thay đổi để gắn nhãn Mới vào / Rơi ra / Đổi nhóm / Đổi hạng
export function changeKind(change: RunSchoolChange): RunChangeKind {
  if (change.fromGroup === null) return "new";
  if (change.toGroup === null) return "dropped";
  return change.fromGroup !== change.toGroup ? "group" : "rank";
}
