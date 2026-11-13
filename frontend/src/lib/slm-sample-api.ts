import { ApiError, apiFetch } from "@/lib/api";
import type {
  PagedResult,
  SaveSlmSampleRequest,
  SlmImportResult,
  SlmReviewResult,
  SlmReviewStatus,
  SlmSample,
  SlmSampleSummary,
  SlmSampleType,
  SlmSplit,
} from "@/types/slm-sample";

const base = "/api/v1/admin/slm-samples";

export interface SlmSampleFilters {
  sampleType?: SlmSampleType;
  split?: SlmSplit;
  reviewStatus?: SlmReviewStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

function query(params: Record<string, string | number | undefined>): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") qs.set(key, String(value));
  }
  const text = qs.toString();
  return text ? `?${text}` : "";
}

async function failureOf(res: Response): Promise<ApiError> {
  const body = await res.json().catch(() => null);
  const message = body?.detail ?? body?.title ?? body?.error ?? body?.message ?? res.statusText;
  return new ApiError(res.status, message, body);
}

export const adminSlmSampleApi = {
  list: (filters: SlmSampleFilters = {}) =>
    apiFetch<PagedResult<SlmSample>>(`${base}${query({ ...filters })}`),

  summary: () => apiFetch<SlmSampleSummary>(`${base}/summary`),

  get: (id: string) => apiFetch<SlmSample>(`${base}/${encodeURIComponent(id)}`),

  create: (data: SaveSlmSampleRequest) =>
    apiFetch<SlmSample>(base, { method: "POST", body: JSON.stringify(data) }),

  update: (id: string, data: SaveSlmSampleRequest) =>
    apiFetch<SlmSample>(`${base}/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(data) }),

  remove: (id: string) => apiFetch<null>(`${base}/${encodeURIComponent(id)}`, { method: "DELETE" }),

  review: (ids: string[], status: SlmReviewStatus, note?: string) =>
    apiFetch<SlmReviewResult>(`${base}/review`, {
      method: "POST",
      body: JSON.stringify({ ids, status, note: note || undefined }),
    }),

  importXlsx: async (file: File): Promise<SlmImportResult> => {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch(`${base}/import`, {
      method: "POST",
      credentials: "include",
      body: form,
      cache: "no-store",
    });
    if (!res.ok) throw await failureOf(res);
    return (await res.json()) as SlmImportResult;
  },

  exportJsonl: async (split?: SlmSplit): Promise<void> => {
    const res = await fetch(`${base}/export${query({ split })}`, {
      credentials: "include",
      cache: "no-store",
    });
    if (!res.ok) throw await failureOf(res);
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = split ? `slm-samples-${split}.jsonl` : "slm-samples.jsonl";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },
};
