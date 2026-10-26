// API client cho gợi ý trường: tạo, đọc lần gần nhất, đổi nút ưu tiên, tư vấn từng trường.

import { apiFetch, ApiError } from "@/lib/api";
import type { Pref, Recommendation, SchoolExplanation } from "@/types/recommendation";

export interface PrefPreset {
  key: "balanced" | "cost" | "aid" | "prestige";
  label: string;
  pref: Pref;
}

// Bốn nút ưu tiên, giá trị lấy từ skill_pick.md
export const PREF_PRESETS: PrefPreset[] = [
  { key: "balanced", label: "Cân bằng", pref: [2, 2, 2] },
  { key: "cost", label: "Chi phí thấp", pref: [3, 1, 1] },
  { key: "aid", label: "Học bổng nhiều", pref: [1, 3, 1] },
  { key: "prestige", label: "Danh tiếng", pref: [1, 1, 3] },
];

const BASE = "/api/v1/recommendations";

export const recommendationApi = {
  // Chạy cả quy trình: lọc → Qwen → Gemini chấm → Gemini chọn → lưu. Mất khoảng nửa phút, chỉ gọi khi cần.
  create: (pref?: Pref) =>
    apiFetch<Recommendation>(BASE, {
      method: "POST",
      body: JSON.stringify({ pref: pref ?? null }),
    }),

  // Đọc lần gợi ý gần nhất, không gọi AI. Chưa có lần nào thì trả null thay vì ném lỗi.
  getLatest: async (): Promise<Recommendation | null> => {
    try {
      const rec = await apiFetch<Recommendation>(`${BASE}/latest`, { method: "GET" });
      // Lần lưu trước PR #19 có items là mảng, không có scored/reach: coi như chưa có, để người dùng chạy lại
      return Array.isArray(rec?.items?.scored) ? rec : null;
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) return null;
      throw err;
    }
  },

  // Đổi nút ưu tiên: chỉ gọi lại lượt chọn trên số đã lưu, không lọc lại, không gọi Qwen.
  repick: (pref: Pref) =>
    apiFetch<Recommendation>(`${BASE}/repick`, {
      method: "POST",
      body: JSON.stringify({ pref }),
    }),

  // Mở chi tiết một trường: Qwen viết lời tư vấn từ số đã lưu.
  explain: (code: string) =>
    apiFetch<SchoolExplanation>(`${BASE}/schools/${encodeURIComponent(code)}/explain`, {
      method: "POST",
    }),
};
