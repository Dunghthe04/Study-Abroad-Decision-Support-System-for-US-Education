import { apiFetch } from "@/lib/api";
import type { DateRange } from "@/lib/ai-usage-api";
import type { AiQualityReport, AiQualitySamplePage, SampleOutcome } from "@/types/ai-quality";

const base = "/api/v1/admin/ai-quality";

function query(range: DateRange, extra?: Record<string, string | number>): string {
  const qs = new URLSearchParams({ from: range.from, to: range.to });
  for (const [key, value] of Object.entries(extra ?? {})) qs.set(key, String(value));
  return `?${qs.toString()}`;
}

// Chất lượng AI cho admin (UC-90). Chỉ admin. Báo cáo chỉ là số; mẫu hội thoại là dữ liệu cá nhân đã che,
// mỗi lần xem được backend ghi nhật ký.
export const adminAiQualityApi = {
  report: (range: DateRange) => apiFetch<AiQualityReport>(`${base}${query(range)}`),

  samples: (range: DateRange, outcome: SampleOutcome, page: number, pageSize: number) =>
    apiFetch<AiQualitySamplePage>(`${base}/samples${query(range, { outcome, page, pageSize })}`),
};
