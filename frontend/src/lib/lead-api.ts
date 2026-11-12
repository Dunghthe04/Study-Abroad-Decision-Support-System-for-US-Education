// [UC-42] API gửi yêu cầu liên hệ trung tâm (học sinh tự gửi hoặc phụ huynh gửi thay con).
// Danh sách con đã liên kết của phụ huynh dùng lại parentLinksApi.getMine() trong lib/api.ts.

import { apiFetch, ApiError } from "@/lib/api";
import type { CreateLeadRequest, LeadDto, LeadRuleCode, LeadRuleError } from "@/types/lead";

export const leadApi = {
  create: (request: CreateLeadRequest) =>
    apiFetch<LeadDto>("/api/v1/leads", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  getById: (id: string) => apiFetch<LeadDto>(`/api/v1/leads/${encodeURIComponent(id)}`),
};

/**
 * Đọc lỗi quy tắc { code, message, retryAt? } từ ApiError của leadApi.create.
 * Trả null nếu không phải lỗi quy tắc (lỗi mạng, 404, lỗi kiểm tra dữ liệu dạng ProblemDetails...).
 */
export function getLeadRuleError(error: unknown): LeadRuleError | null {
  if (!(error instanceof ApiError)) return null;
  const body = error.body as { code?: unknown; message?: unknown; retryAt?: unknown } | null;
  if (!body || typeof body.code !== "string") return null;

  return {
    status: error.status,
    code: body.code as LeadRuleCode,
    message: typeof body.message === "string" ? body.message : error.message,
    retryAt: typeof body.retryAt === "string" ? body.retryAt : null,
  };
}
