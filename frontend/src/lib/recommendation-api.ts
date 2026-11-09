// API client cho gợi ý trường: tạo, đọc lần gần nhất, mô phỏng hồ sơ, tư vấn từng trường.

import { apiFetch, ApiError } from "@/lib/api";
import type {
  Pref,
  Recommendation,
  SchoolOption,
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

  // Danh sách trường cho ô chọn có gợi ý trong chat. Tải một lần, lọc ngay trên máy khi học sinh gõ.
  schoolOptions: () => apiFetch<SchoolOption[]>(`${BASE}/schools/names`, { method: "GET" }),
};
