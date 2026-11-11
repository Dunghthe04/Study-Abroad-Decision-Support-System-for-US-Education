import { apiFetch } from "@/lib/api";
import type {
  AiPrice,
  AiUsageOverview,
  AiUsagePage,
  AiUsageTurnDetail,
  AiUsageTurnRow,
  AiUsageUserOverview,
  AiUsageUserRank,
} from "@/types/ai-usage";

export interface DateRange {
  from: string; // yyyy-MM-dd (UTC)
  to: string;
}

function query(range: DateRange, extra?: Record<string, number>): string {
  const qs = new URLSearchParams({ from: range.from, to: range.to });
  for (const [key, value] of Object.entries(extra ?? {})) qs.set(key, String(value));
  return `?${qs.toString()}`;
}

const base = "/api/v1/admin/ai-usage";

// Token và chi phí AI cho admin (UC-83). Chỉ admin; không có nội dung chat.
export const adminAiUsageApi = {
  overview: (range: DateRange) => apiFetch<AiUsageOverview>(`${base}/overview${query(range)}`),

  users: (range: DateRange, page: number, pageSize: number) =>
    apiFetch<AiUsagePage<AiUsageUserRank>>(`${base}/users${query(range, { page, pageSize })}`),

  userOverview: (userId: string, range: DateRange) =>
    apiFetch<AiUsageUserOverview>(`${base}/users/${encodeURIComponent(userId)}${query(range)}`),

  userTurns: (userId: string, range: DateRange, page: number, pageSize: number) =>
    apiFetch<AiUsagePage<AiUsageTurnRow>>(`${base}/users/${encodeURIComponent(userId)}/turns${query(range, { page, pageSize })}`),

  turn: (messageId: string) => apiFetch<AiUsageTurnDetail>(`${base}/turns/${encodeURIComponent(messageId)}`),

  pricing: () => apiFetch<AiPrice[]>(`${base}/pricing`),
};
