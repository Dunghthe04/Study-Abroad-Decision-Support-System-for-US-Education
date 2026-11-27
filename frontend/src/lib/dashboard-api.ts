// UC-94: Dashboard KPI API client (Student & Parent)

import { apiFetch } from "@/lib/api";
import type { StudentDashboard, ParentChildDashboard, Roadmap } from "@/types/dashboard";

export const dashboardApi = {
  // Lấy dashboard cho học sinh (hoặc phụ huynh đang acting thay cho con managed)
  getMyDashboard: () =>
    apiFetch<StudentDashboard>("/api/v1/dashboard/me", { method: "GET" }),

  // Lấy dashboard cho phụ huynh xem từng người con có liên kết
  getChildDashboard: (childId: string) =>
    apiFetch<ParentChildDashboard>(`/api/v1/dashboard/children/${encodeURIComponent(childId)}`, {
      method: "GET",
    }),

  // Lộ trình theo trường mục tiêu; null (204) khi chưa có hồ sơ
  getMyRoadmap: () =>
    apiFetch<Roadmap | null>("/api/v1/dashboard/roadmap", { method: "GET" }),

  // Phụ huynh chỉ xem lộ trình của con đã liên kết
  getChildRoadmap: (childId: string) =>
    apiFetch<Roadmap | null>(`/api/v1/dashboard/children/${encodeURIComponent(childId)}/roadmap`, { method: "GET" }),

  // Lưu tỉnh/thành để gợi ý trung tâm gần; rỗng = xóa
  setProvince: (province: string | null) =>
    apiFetch<null>("/api/v1/dashboard/province", { method: "PUT", body: JSON.stringify({ province }) }),

  getProvinces: () =>
    apiFetch<string[]>("/api/v1/dashboard/provinces", { method: "GET" }),
};
