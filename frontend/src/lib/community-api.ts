import { apiFetch } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type { CommunityMessageDto, CommunityReportDto } from "@/types/community";

export const communityApi = {
  listMessages: (before?: string, take = 50) => {
    const sp = new URLSearchParams();
    if (before) sp.set("before", before);
    sp.set("take", String(take));
    return apiFetch<CommunityMessageDto[]>(`/api/v1/community/messages?${sp}`);
  },
  restriction: () => apiFetch<{ message: string | null }>("/api/v1/community/restriction"),
  send: (content: string) =>
    apiFetch<CommunityMessageDto>("/api/v1/community/messages", {
      method: "POST",
      body: JSON.stringify({ content }),
    }),
  report: (id: string, reason: string, detail?: string) =>
    apiFetch<null>(`/api/v1/community/messages/${encodeURIComponent(id)}/report`, {
      method: "POST",
      body: JSON.stringify({ reason, detail: detail || null }),
    }),
};

export const adminForumApi = {
  reports: (status = "open", page = 1, pageSize = 20) => {
    const sp = new URLSearchParams({ status, page: String(page), pageSize: String(pageSize) });
    return apiFetch<PagedResult<CommunityReportDto>>(`/api/v1/admin/forum/reports?${sp}`);
  },
  hide: (id: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/reports/${encodeURIComponent(id)}/hide`, { method: "POST" }),
  dismiss: (id: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/reports/${encodeURIComponent(id)}/dismiss`, { method: "POST" }),
  restrict: (userId: string, days: number, reason: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/users/${encodeURIComponent(userId)}/restrict`, {
      method: "POST",
      body: JSON.stringify({ days, reason }),
    }),
};
