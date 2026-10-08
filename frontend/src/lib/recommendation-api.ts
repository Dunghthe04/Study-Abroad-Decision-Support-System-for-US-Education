// Gợi ý trường: POST chạy lọc trường theo hồ sơ (chờ lâu vì AI viết giải thích), GET latest lấy kết quả đã lưu

import { apiFetch, ApiError } from "@/lib/api";
import type { RecommendationResult } from "@/types/recommendation";

const BASE = "/api/v1/recommendations";

/** Kết quả lần lọc gần nhất; chưa lọc lần nào (404) thì trả null. */
export async function getLatestRecommendation(): Promise<RecommendationResult | null> {
  try {
    return await apiFetch<RecommendationResult>(`${BASE}/latest`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

/** Lọc trường theo hồ sơ hiện tại. 404 = chưa có hồ sơ học sinh. */
export function createRecommendation(): Promise<RecommendationResult> {
  return apiFetch<RecommendationResult>(BASE, { method: "POST" });
}
