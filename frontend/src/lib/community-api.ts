import { apiFetch } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type { CommunityMessageDto, CommunityReportDto, CommunityRestrictionDto } from "@/types/community";

export interface ListMessagesParams {
  before?: string;
  beforeId?: string;
  after?: string;
  afterId?: string;
  take?: number;
}

export const communityApi = {
  listMessages: (params?: ListMessagesParams | string, take = 50) => {
    const sp = new URLSearchParams();
    if (typeof params === "string") {
      sp.set("before", params);
      sp.set("take", String(take));
    } else if (params) {
      if (params.before) sp.set("before", params.before);
      if (params.beforeId) sp.set("beforeId", params.beforeId);
      if (params.after) sp.set("after", params.after);
      if (params.afterId) sp.set("afterId", params.afterId);
      sp.set("take", String(params.take ?? take));
    } else {
      sp.set("take", String(take));
    }
    return apiFetch<CommunityMessageDto[]>(`/api/v1/community/messages?${sp}`);
  },
  restriction: () => apiFetch<CommunityRestrictionDto>("/api/v1/community/restriction"),
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
  hideMessage: (id: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/messages/${encodeURIComponent(id)}/hide`, { method: "POST" }),
  dismiss: (id: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/reports/${encodeURIComponent(id)}/dismiss`, { method: "POST" }),
  restrict: (userId: string, days: number, reason: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/users/${encodeURIComponent(userId)}/restrict`, {
      method: "POST",
      body: JSON.stringify({ days, reason }),
    }),
  unrestrict: (userId: string) =>
    apiFetch<{ message: string }>(`/api/v1/admin/forum/users/${encodeURIComponent(userId)}/unrestrict`, {
      method: "POST",
    }),
};
