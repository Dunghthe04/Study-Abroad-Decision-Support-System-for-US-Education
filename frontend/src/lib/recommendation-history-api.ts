// [UC-87] API client cho lịch sử gợi ý: danh sách các lần chạy, xem một lần, so sánh hai lần.
// Học sinh xem của mình; phụ huynh có liên kết truyền studentId của con. Chỉ đọc.

import { apiFetch } from "@/lib/api";
import type { Recommendation } from "@/types/recommendation";
import type { RecommendationRunSummary, RunComparison } from "@/types/recommendation-history";

const BASE = "/api/v1/recommendations/history";

export const recommendationHistoryApi = {
  // Tối đa 50 lần gần nhất, mới nhất trước. Bỏ trống studentId: lịch sử của mình.
  list: (studentId?: string) =>
    apiFetch<RecommendationRunSummary[]>(
      studentId ? `${BASE}?studentId=${encodeURIComponent(studentId)}` : BASE,
      { method: "GET" },
    ),

  // Một lần chạy đầy đủ, cùng khuôn với getLatest nên dùng lại được thẻ trường
  get: (id: string) =>
    apiFetch<Recommendation>(`${BASE}/${encodeURIComponent(id)}`, { method: "GET" }),

  // from là lần trước, to là lần sau. Chỉ trả các trường có thay đổi.
  compare: (fromId: string, toId: string) =>
    apiFetch<RunComparison>(
      `${BASE}/compare?from=${encodeURIComponent(fromId)}&to=${encodeURIComponent(toId)}`,
      { method: "GET" },
    ),
};
