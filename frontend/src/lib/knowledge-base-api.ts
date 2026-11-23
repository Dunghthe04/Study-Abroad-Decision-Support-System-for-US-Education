import { ApiError, apiFetch } from "@/lib/api";
import type {
  KnowledgeDocType,
  KnowledgeEntry,
  KnowledgeImportPreview,
  KnowledgeImportResult,
  KnowledgeSort,
  PagedResult,
  SaveKnowledgeEntryRequest,
} from "@/types/knowledge-base";

const base = "/api/v1/admin/knowledge-base";
const TEMPLATE_FILE_NAME = "knowledge-base-template.xlsx";

export interface KnowledgeFilters {
  search?: string;
  docType?: KnowledgeDocType;
  label?: string;
  topicCode?: string;
  sort?: KnowledgeSort;
  page?: number;
  pageSize?: number;
}

function query(params: Record<string, string | number | boolean | undefined>): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "" && value !== false) qs.set(key, String(value));
  }
  const text = qs.toString();
  return text ? `?${text}` : "";
}

// ponytail: chép từ slm-sample-api.ts (upload/tải file cần fetch thô, không qua apiFetch). Tách ra lib dùng chung khi có nơi thứ ba cần
async function failureOf(res: Response): Promise<ApiError> {
  const body = await res.json().catch(() => null);
  const message = body?.detail ?? body?.title ?? body?.error ?? body?.message ?? res.statusText;
  return new ApiError(res.status, message, body);
}

async function postXlsx<T>(action: "preview" | "import", file: File, signal?: AbortSignal): Promise<T> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`${base}/${action}`, { method: "POST", credentials: "include", body: form, cache: "no-store", signal });
  if (!res.ok) throw await failureOf(res);
  return (await res.json()) as T;
}

async function download(path: string, fileName: string): Promise<void> {
  const res = await fetch(path, { credentials: "include", cache: "no-store" });
  if (!res.ok) throw await failureOf(res);
  const url = URL.createObjectURL(await res.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export const adminKnowledgeBaseApi = {
  list: (filters: KnowledgeFilters = {}) => apiFetch<PagedResult<KnowledgeEntry>>(`${base}${query({ ...filters })}`),

  get: (id: string) => apiFetch<KnowledgeEntry>(`${base}/${encodeURIComponent(id)}`),

  create: (data: SaveKnowledgeEntryRequest) => apiFetch<KnowledgeEntry>(base, { method: "POST", body: JSON.stringify(data) }),

  update: (id: string, data: SaveKnowledgeEntryRequest) =>
    apiFetch<KnowledgeEntry>(`${base}/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(data) }),

  remove: (id: string) => apiFetch<null>(`${base}/${encodeURIComponent(id)}`, { method: "DELETE" }),

  previewXlsx: (file: File, signal?: AbortSignal) => postXlsx<KnowledgeImportPreview>("preview", file, signal),

  importXlsx: (file: File) => postXlsx<KnowledgeImportResult>("import", file),

  downloadTemplate: () => download(`${base}/template`, TEMPLATE_FILE_NAME),
};

/** existingId của lỗi 409 (mã số trùng), để giao diện mở mục đã có. */
export function duplicateIdOf(error: unknown): string | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null;
  const body = error.body as { existingId?: unknown } | null;
  return typeof body?.existingId === "string" ? body.existingId : null;
}