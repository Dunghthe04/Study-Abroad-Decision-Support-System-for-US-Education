import { ApiError, apiFetch } from "@/lib/api";
import { authFetch } from "@/lib/auth-refresh";
import type {
  PagedResult,
  SaveSlmSampleRequest,
  SlmImportPreview,
  SlmImportResult,
  SlmReviewResult,
  SlmReviewStatus,
  SlmSample,
  SlmSampleCreator,
  SlmSampleDetail,
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
  createdBy?: string;
  from?: string;
  to?: string;
  source?: string;
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

const TEMPLATE_FILE_NAME = "slm-training-template.xlsx";

async function postXlsx<T>(action: "preview" | "import", file: File, signal?: AbortSignal): Promise<T> {
  const form = new FormData();
  form.append("file", file);
  const res = await authFetch(`${base}/${action}`, {
    method: "POST",
    credentials: "include",
    body: form,
    cache: "no-store",
    signal,
  });
  if (!res.ok) throw await failureOf(res);
  return (await res.json()) as T;
}

export const adminSlmSampleApi = {
  list: (filters: SlmSampleFilters = {}) =>
    apiFetch<PagedResult<SlmSample>>(`${base}${query({ ...filters })}`),

  summary: () => apiFetch<SlmSampleSummary>(`${base}/summary`),

  get: (id: string) => apiFetch<SlmSampleDetail>(`${base}/${encodeURIComponent(id)}`),

  creators: () => apiFetch<SlmSampleCreator[]>(`${base}/creators`),

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

  previewXlsx: (file: File, signal?: AbortSignal) => postXlsx<SlmImportPreview>("preview", file, signal),

  importXlsx: (file: File) => postXlsx<SlmImportResult>("import", file),

  downloadTemplate: () => download(`${base}/template`, TEMPLATE_FILE_NAME),

  exportApproved: (format: "jsonl" | "xlsx") =>
    download(`${base}/export?format=${format}`, `slm-samples-approved.${format}`),
};

function fileNameOf(res: Response, fallback: string): string {
  const header = res.headers.get("content-disposition") ?? "";
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
  return match ? decodeURIComponent(match[1]) : fallback;
}

async function download(path: string, fallbackName: string): Promise<void> {
  const res = await authFetch(path, {
    credentials: "include",
    cache: "no-store",
  });
  if (!res.ok) throw await failureOf(res);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileNameOf(res, fallbackName);
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
