// [UC-86] API client cho kịch bản what-if đã lưu: lưu, xem danh sách, mở lại, đổi tên, xóa.
// Học sinh làm được tất cả; phụ huynh có liên kết chỉ gọi list(studentId) và get.

import { apiFetch } from "@/lib/api";
import type { SavedScenario, SimulationOverrides } from "@/types/recommendation";

const BASE = "/api/v1/scenarios";

export const scenarioApi = {
  // Lưu đúng các giá trị đang giả định (kể cả pref). Backend tự tính lại bản chụp kết quả.
  save: (title: string, overrides: SimulationOverrides) =>
    apiFetch<SavedScenario>(BASE, {
      method: "POST",
      body: JSON.stringify({ title, ...overrides }),
    }),

  // Bỏ trống studentId: kịch bản của mình. Phụ huynh truyền id của con đã liên kết.
  list: (studentId?: string) =>
    apiFetch<SavedScenario[]>(
      studentId ? `${BASE}?studentId=${encodeURIComponent(studentId)}` : BASE,
      { method: "GET" },
    ),

  get: (id: string) => apiFetch<SavedScenario>(`${BASE}/${id}`, { method: "GET" }),

  rename: (id: string, title: string) =>
    apiFetch<SavedScenario>(`${BASE}/${id}`, {
      method: "PATCH",
      body: JSON.stringify({ title }),
    }),

  remove: (id: string) => apiFetch<void>(`${BASE}/${id}`, { method: "DELETE" }),
};
