// API client cho gợi ý trường: tạo, đọc lần gần nhất, mô phỏng hồ sơ, tư vấn từng trường.

import { apiFetch, ApiError } from "@/lib/api";
import type {
  GoalTarget,
  Pref,
  Recommendation,
  SchoolExplanation,
  SchoolGoalResult,
  SimulationOverrides,
  SimulationResponse,
} from "@/types/recommendation";

const BASE = "/api/v1/recommendations";

export const recommendationApi = {
  // Chạy cả quy trình: lọc cứng → đọc ngoại khóa → code chấm nhóm và fit score → lưu.
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

  // Trình mô phỏng: tính lại với GPA, SAT, ngân sách... giả định. Không lưu, không gọi AI.
  simulate: (overrides: SimulationOverrides) =>
    apiFetch<SimulationResponse>(`${BASE}/simulate`, {
      method: "POST",
      body: JSON.stringify(overrides),
    }),

  // "Cần gì để vào trường này": mức tối thiểu để trường đạt nhóm đích, kèm tài chính. target bỏ trống: nhóm cao hơn một bậc.
  // Tính từ hồ sơ thật, không lưu, không gọi AI.
  goalSeek: (code: string, target?: GoalTarget | null) =>
    apiFetch<SchoolGoalResult>(`${BASE}/schools/${encodeURIComponent(code)}/goal-seek`, {
      method: "POST",
      body: JSON.stringify({ target: target ?? null }),
    }),

  // Mở chi tiết một trường: Qwen viết lời tư vấn từ số đã lưu.
  explain: (code: string) =>
    apiFetch<SchoolExplanation>(`${BASE}/schools/${encodeURIComponent(code)}/explain`, {
      method: "POST",
    }),
};
