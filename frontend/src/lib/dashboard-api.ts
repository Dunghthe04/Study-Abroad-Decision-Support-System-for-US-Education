// UC-94: Dashboard KPI API client (Student & Parent)

import { apiFetch } from "@/lib/api";
import type { StudentDashboard, ParentChildDashboard } from "@/types/dashboard";

export const dashboardApi = {
  // Lấy dashboard cho học sinh (hoặc phụ huynh đang acting thay cho con managed)
  getMyDashboard: () =>
    apiFetch<StudentDashboard>("/api/v1/dashboard/me", { method: "GET" }),

  // Lấy dashboard cho phụ huynh xem từng người con có liên kết
  getChildDashboard: (childId: string) =>
    apiFetch<ParentChildDashboard>(`/api/v1/dashboard/children/${encodeURIComponent(childId)}`, {
      method: "GET",
    }),
};
